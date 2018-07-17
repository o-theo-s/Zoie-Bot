using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Apis;
using Apis.Models;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Zoie.Bot.Dialogs.LUIS;
using Zoie.Bot.Models;
using Zoie.Helpers;
using Zoie.Helpers.Channels.Facebook;
using Zoie.Helpers.Channels.Facebook.Library;
using Zoie.Resources.DialogReplies;
using static Zoie.Bot.Dialogs.Main.PersonalizationDialog;

namespace Zoie.Bot.Dialogs.Main
{
    [Serializable]
    public class OccasionDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(SelectOccasionAsync);

            return Task.CompletedTask;
        }

        private async Task SelectOccasionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply(DialogsHelper.GetResourceValue<OccasionReplies>("OccasionSelect", activity));

            reply.SuggestedActions = new SuggestedActions() { Actions = await DialogsHelper.GetOccasionSuggestedActionsAsync() };
            await context.PostAsync(reply);

            context.Wait(OccasionSelectedAsync);
        }

        private async Task OccasionSelectedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();

            if (activity.Text.StartsWith("__occasion"))
            {
                Occasion occasion = JsonConvert.DeserializeObject<Occasion>(activity.Text.Remove(0, "__occasion_".Length));
                context.ConversationData.SetValue("OccasionSelected", occasion);

                string resourceName = GeneralHelper.CapitalizeFirstLetter(occasion.Name);
                bool occasionOK = DialogsHelper.TryGetResourceValue<OccasionReplies>(resourceName, out string occasionPhrase, activity);

                if (occasionOK)
                {
                    reply.Text = occasionPhrase;
                    await context.PostAsync(reply);

                    if (context.UserData.ContainsKey("HasPersonalized") && context.UserData.GetValue<bool>("HasPersonalized"))
                        await this.ShowCollectionsForOccasionAsync(context, result);
                    else
                        await context.Forward(new PersonalizationDialog(), ShowCollectionsForOccasionAsync, activity);
                }
                else
                {
                    reply.Text = OccasionReplies.UnknownOccasion;
                    await context.PostAsync(reply);

                    await this.SelectOccasionAsync(context, result);
                }
            }
            else if (activity.Text == "__personality_answer")
            {
                await this.SelectOccasionAsync(context, result);
            }
            else
            {
                await context.Forward(new OccasionLuisDialog(), OccasionSelectedAsync, activity);
            }

            return;
        }

        private async Task ShowCollectionsForOccasionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();

            Occasion occasion = context.ConversationData.GetValue<Occasion>("OccasionSelected");
            string gender = context.UserData.GetValue<string>("Gender");
            context.ConversationData.TryGetValue("CollectionsNextPage", out int currentPage);

            var collectionsApi = new API<CollectionsRoot>();
            var collectionsRoot = await collectionsApi.CallAsync(new Dictionary<string, string>(2) { { "occasion_id", occasion.Id }, { "page", currentPage.ToString() } });

            if (collectionsRoot == null)
            {
                reply.Text = $"Sorry, no collections found for {occasion.Name.ToLower()} :/";
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Reselect occasion", Type = ActionTypes.PostBack, Value = "__reselect_occasion" },
                        new CardAction(){ Title = "New search", Type = ActionTypes.PostBack, Value = "__menu_new_search" }
                    }
                };
                await context.PostAsync(reply);
                context.Wait(AfterShowCollectionsForOccasionAsync);
                return;
            }

            if (collectionsRoot.RemainingPages > 0)
                context.ConversationData.SetValue("CollectionsNextPage", currentPage + 1);
            else
                context.ConversationData.RemoveValue("CollectionsNextPage");


            if (activity.ChannelId == "facebook")
            {
                var contentsForCurrentPage = new List<FacebookGenericTemplateContent>(4)
                {
                    new FacebookGenericTemplateContent()
                    {
                        Title = GeneralHelper.CapitalizeFirstLetter(occasion.Name) + " for " + ((gender == "Male") ? "men" : "women"),
                        Subtitle = $"Fashion suggestions for {occasion.Name.ToLower()} - Page {currentPage + 1}",
                        ImageUrl = occasion.ImageUrl
                    }
                };

                Collection collection;
                for (int i = 0; i < 3 && i < collectionsRoot.Collections.Count; i++)
                {
                    collection = collectionsRoot.Collections[i];
                    contentsForCurrentPage.Add(
                        new FacebookGenericTemplateContent()
                        {
                            Title = GeneralHelper.CapitalizeFirstLetter(collection.Title),
                            Subtitle = "By {store name}",
                            Buttons = new[] { new FacebookPostbackButton(title: "View items", payload: $"__view_collection_{collection.Id}") },
                            ImageUrl = collection.ImageUrl
                        });
                }

                FacebookPostbackButton bottomPageButton = null;
                if (!(currentPage == 0 && collectionsRoot.RemainingPages == 0))
                    bottomPageButton = new FacebookPostbackButton(title: collectionsRoot.RemainingPages > 0 ? "Next page" : "First page", payload: "__more_collections");

                reply.ChannelData = ChannelsHelper.Facebook.Templates.CreateListTemplate(contentsForCurrentPage.ToArray(), bottomPageButton);
            }
            else
            {
                reply.AttachmentLayout = AttachmentLayoutTypes.List;

                context.Wait(UnimplementedAsync);
                return;
            }


            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "Reselect occasion", Type = ActionTypes.PostBack, Value = "__reselect_occasion" },
                    new CardAction(){ Title = "New search", Type = ActionTypes.PostBack, Value = "__menu_new_search" }
                }
            };

            await context.PostAsync(reply);
            context.Wait(AfterShowCollectionsForOccasionAsync);
        }

        private async Task AfterShowCollectionsForOccasionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            if (activity.Text.StartsWith("__"))
            {
                if (activity.Text.StartsWith("__view_collection"))
                {
                    await this.ShowApparelsForCollectionAsync(context, result);
                }
                else if (activity.Text.StartsWith("__more_collections"))
                {
                    await this.ShowCollectionsForOccasionAsync(context, result);
                }
                else if (activity.Text.Equals("__reselect_occasion"))
                {
                    context.ConversationData.RemoveValue("CollectionsNextPage");
                    await this.SelectOccasionAsync(context, result);
                }
                //TODO: Remove else if - It will work from persistent menu
                else if (activity.Text == "__menu_new_search")
                {
                    context.ConversationData.RemoveValue("CollectionsNextPage");
                    await this.EndAsync(context, result);
                }
            }
            else
            {
                await context.Forward(new GlobalLuisDialog<object>(), ShowCollectionsForOccasionAsync, activity);
            }
        }

        private async Task ShowApparelsForCollectionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();

            Occasion occasion = context.ConversationData.GetValue<Occasion>("OccasionSelected");
            string gender = context.UserData.GetValue<string>("Gender");
            string collectionId = activity.Text.Remove(0, "__view_collection_".Length);

            /*CloudTable occasionSetsTable = TablesHelper.GetTableReference(TablesHelper.TableNames.OccasionSets);
            TableQuery<CollectionApparel> rangeQuery = new TableQuery<CollectionApparel>().Where(
                TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, occasionType + "__" + gender.ToLower()),
                    TableOperators.And,
                    TableQuery.CombineFilters(
                        TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThanOrEqual, collectionId.ToString()),
                        TableOperators.And,
                        TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.LessThan, (collectionId + 1).ToString()))));
            var apparels = occasionSetsTable.ExecuteQuery(rangeQuery);*/

            var collectionApparelsApi = new API<CollectionApparelsRoot>();
            var collectionApparelsRoot = await collectionApparelsApi.CallAsync(new Dictionary<string, string>(1) { { "collection_id", collectionId } });

            reply.Text = "Here you are!";
            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            foreach (var apparel in collectionApparelsRoot.Apparels)
            {
                reply.Attachments.Add(
                    new HeroCard()
                    {
                        Title = GeneralHelper.CapitalizeFirstLetter(apparel.Name),
                        Subtitle = apparel.PriceString + "€",
                        Images = new List<CardImage> { new CardImage { Url = apparel.ImageUrl } },
                        Buttons = new List<CardAction> { new CardAction { Title = "Buy", Type = ActionTypes.OpenUrl, Value = apparel.Link } },
                        Tap = new CardAction { Type = ActionTypes.OpenUrl, Value = apparel.Link }
                    }.ToAttachment());
            }

            reply.Attachments.Add(
                new HeroCard()
                {
                    Title = "Collection rate",
                    Subtitle = "Did you like that set?",
                    Images = new List<CardImage> { new CardImage { Url = "http://zoie.io/images/Brand-icon.png" } },
                    Buttons = new List<CardAction>
                    {
                        new CardAction { Title = "😍 Very much", Type = ActionTypes.PostBack, Value = $"__feedback_rate_{collectionId}_3"},
                        new CardAction { Title = "😐 So and so", Type = ActionTypes.PostBack, Value = $"__feedback_rate_{collectionId}_2"},
                        new CardAction { Title = "😒 Not at all", Type = ActionTypes.PostBack, Value = $"__feedback_rate_{collectionId}_1"},
                    }
                }.ToAttachment());

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "More Collections", Type = ActionTypes.PostBack, Value = "__more_collections" },
                    new CardAction(){ Title = "Reselect occasion", Type = ActionTypes.PostBack, Value = "__reselect_occasion" },
                    new CardAction(){ Title = "New search", Type = ActionTypes.PostBack, Value = "__menu_new_search" }
                }
            };

            await context.PostAsync(reply);

            context.Wait(AfterShowApparelsForCollectionAsync);
        }

        private async Task AfterShowApparelsForCollectionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            if (activity.Text.StartsWith("__"))
            {
                if (activity.Text.StartsWith("__feedback_rate"))
                {
                    string[] feedbackData = activity.Text.Remove(0, "__feedback_rate_".Length).Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                    int collectionId = int.Parse(feedbackData[0]);
                    int rate = int.Parse(feedbackData[1]);
                    //TODO: Store rate for collection

                    await context.PostAsync("Thank your for your feedback!");
                    context.Done(activity);
                }
                else
                {
                    await this.AfterShowCollectionsForOccasionAsync(context, result);
                }
            }
            else
            {
                await context.Forward(new GlobalLuisDialog<object>(), EndAsync, activity);
            }
        }

        private async Task UnimplementedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            await context.PostAsync("Feature not available yet in " + activity.ChannelId);

            await this.SelectOccasionAsync(context, result);
        }

        private async Task EndAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result;

            await context.PostAsync("Hope you liked what I showed you! ☺");
            context.Done(activity);
        }
    }
}