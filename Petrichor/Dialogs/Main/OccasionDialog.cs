using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Zoie.Apis;
using Zoie.Apis.Models;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Zoie.Petrichor.Dialogs.LUIS;
using Zoie.Helpers;
using Zoie.Helpers.Channels.Facebook.Library;
using Zoie.Resources.DialogReplies;
using System.Configuration;

namespace Zoie.Petrichor.Dialogs.Main
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
            context.PrivateConversationData.SetValue("LastOccasionSubdialog", GeneralHelper.GetActualAsyncMethodName());

            reply.SuggestedActions = new SuggestedActions() { Actions = await DialogsHelper.GetOccasionSuggestedActionsAsync() };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            DialogsHelper.EventToMessageActivity(ref activity, ref result);
            var reply = activity.CreateReply();

            switch (activity.Text)
            {
                case string text when text.StartsWith("__occasion"):
                    Occasion occasion = JsonConvert.DeserializeObject<Occasion>(activity.Text.Remove(0, "__occasion_".Length));
                    context.PrivateConversationData.SetValue("OccasionSelected", occasion);

                    string resourceName = GeneralHelper.CapitalizeFirstLetter(occasion.Name);
                    string occasionPhrase = DialogsHelper.GetResourceValue<OccasionReplies>(resourceName, activity);

                    reply.Text = occasionPhrase;
                    await context.PostAsync(reply);

                    if (context.UserData.ContainsKey("HasPersonalized") && context.UserData.GetValue<bool>("HasPersonalized"))
                        await this.CollectionsForOccasionAsync(context, result);
                    else
                        await context.Forward(new PersonalizationDialog(), CollectionsForOccasionAsync, activity);
                    return;
                case string text when text.StartsWith("__view_collection"):
                    await this.ApparelsForCollectionAsync(context, result);
                    return;
                case "__more_collections":
                    await this.CollectionsForOccasionAsync(context, result);
                    return;
                case "__reselect_occasion":
                    context.ConversationData.Clear();
                    await this.SelectOccasionAsync(context, result);
                    return;
                case string text when text.StartsWith("__feedback_rate"):
                    await this.FeedbackRateAsync(context, result);
                    return;
                case "__personality_answer":
                case "__continue":
                    var lastSubdialog = context.PrivateConversationData.GetValue<string>("LastOccasionSubdialog");
                    MethodInfo reshowLastSubdialog = this.GetType().GetMethod(lastSubdialog, BindingFlags.NonPublic | BindingFlags.Instance);

                    if ( reshowLastSubdialog.Name == nameof(this.CollectionsForOccasionAsync)
                            && context.ConversationData.TryGetValue("CollectionsNextPage", out int currentPage) )
                        context.ConversationData.SetValue("CollectionsNextPage", currentPage - 1);

                    await (Task) reshowLastSubdialog.Invoke(this, new object[] { context, result });
                    return;
                default:
                    await context.Forward(new OccasionLuisDialog(), MessageReceivedAsync, activity);
                    return;
            }
        }

        private async Task CollectionsForOccasionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastOccasionSubdialog", GeneralHelper.GetActualAsyncMethodName());

            Occasion occasion = context.PrivateConversationData.GetValue<Occasion>("OccasionSelected");
            string gender = context.UserData.GetValue<string>("Gender");
            context.ConversationData.TryGetValue("CollectionsNextPage", out int currentPage);

            var collectionsApi = new API<CollectionsRoot>();
            var apiParams = new Dictionary<string, string>(3)
            {
                { "occasion_id", occasion.Id },
                { "page", currentPage.ToString() },
                { "gender", gender == "Female" ? "0" : "1" }
            };
            var collectionsRoot = await collectionsApi.CallAsync(apiParams)
                ?? await collectionsApi.CallAsync(apiParams.ToDictionary(kvp => kvp.Key, kvp => kvp.Key == "page" ? (currentPage = 0).ToString() : kvp.Value));
            context.ConversationData.SetValue("CollectionsNextPage", currentPage + 1);

            if (collectionsRoot == null)
            {
                reply.Text = $"Sorry, no collections found for {occasion.Name.ToLower()} :/";
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Reselect occasion", Type = ActionTypes.PostBack, Value = "__reselect_occasion" }
                    }
                };
                await context.PostAsync(reply);
                context.Wait(MessageReceivedAsync);
                return;
            }

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
                            Subtitle = $"By {collection.StoreName}",
                            Buttons = new[] { new FacebookPostbackButton(title: "View items", payload: $"__view_collection_{collection.Id}") },
                            ImageUrl = collection.ImageUrl ??
                                $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Occasions/{occasion.Name}/{gender}/{i+1}.jpg"
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
                    new CardAction(){ Title = "Reselect occasion", Type = ActionTypes.PostBack, Value = "__reselect_occasion" }
                }
            };

            await context.PostAsync(reply);
            context.Wait(MessageReceivedAsync);
        }

        private async Task ApparelsForCollectionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastOccasionSubdialog", GeneralHelper.GetActualAsyncMethodName());

            string gender = context.UserData.GetValue<string>("Gender");
            string collectionId = null;
            if (activity.Text.StartsWith("__view_collection_"))
                collectionId = activity.Text.Remove(0, "__view_collection_".Length);
            if (!string.IsNullOrWhiteSpace(collectionId))
                context.PrivateConversationData.SetValue("LastOccasionCollectionViewed", collectionId);
            else if (!context.PrivateConversationData.TryGetValue("LastOccasionCollectionViewed", out collectionId))
                await this.CollectionsForOccasionAsync(context, result);

            var collectionApparelsApi = new API<CollectionApparelsRoot>();
            var collectionApparelsRoot = await collectionApparelsApi.CallAsync(new Dictionary<string, string>(1) { { "collection_id", collectionId } });

            reply.Text = "Here you are!";
            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            foreach (var apparel in collectionApparelsRoot.Items)
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
                    new CardAction(){ Title = "Reselect occasion", Type = ActionTypes.PostBack, Value = "__reselect_occasion" }
                }
            };

            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task FeedbackRateAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply("Thank your for your feedback!");
            context.PrivateConversationData.SetValue("LastOccasionSubdialog", GeneralHelper.GetActualAsyncMethodName());

            string[] feedbackData = null;
            if (activity.Text.StartsWith("__feedback_rate_"))
                feedbackData = activity.Text.Remove(0, "__feedback_rate_".Length).Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries);

            if (feedbackData != null)
            {
                int collectionId = int.Parse(feedbackData[0]);
                int rate = int.Parse(feedbackData[1]);

                //TODO: Store rate for collection
            }

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "More Collections", Type = ActionTypes.PostBack, Value = "__more_collections" },
                    new CardAction(){ Title = "Reselect occasion", Type = ActionTypes.PostBack, Value = "__reselect_occasion" }
                }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
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