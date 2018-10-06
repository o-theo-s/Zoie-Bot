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
using Zoie.Helpers;
using Zoie.Helpers.Channels.Facebook.Library;
using Zoie.Resources.DialogReplies;
using System.Configuration;
using static Zoie.Helpers.DialogsHelper;
using static Zoie.Helpers.GeneralHelper;
using static Zoie.Resources.DialogReplies.OccasionReplies;
using static Zoie.Resources.DialogReplies.GeneralReplies;
using Zoie.Petrichor.Dialogs.NLU;
using Zoie.Petrichor.Dialogs.Main.Prefatory;

#pragma warning disable CS0642 // Possible mistaken empty statement

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
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastOccasionSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            reply.Text = GetResourceValue<OccasionReplies>(nameof(OccasionSelect), locale, await GetDaytimeAsync(activity));
            reply.SuggestedActions = new SuggestedActions() { Actions = await GetOccasionSuggestedActionsAsync() };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            EventToMessageActivity(ref activity, ref result);
            var reply = activity.CreateReply();
            context.UserData.TryGetValue("Locale", out string locale);

            switch (activity.Text)
            {
                case string text when text.StartsWith("__occasion"):
                    Occasion occasion = JsonConvert.DeserializeObject<Occasion>(activity.Text.Remove(0, "__occasion_".Length));
                    context.PrivateConversationData.SetValue("OccasionSelected", occasion);

                    string occasionPhrase = GetResourceValue<OccasionReplies>(CapitalizeFirstLetter(occasion.Name), locale);

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
                case string text when text.StartsWith("__feedback_"):
                    await this.FeedbackAsync(context, result);
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
                    await context.Forward(new WitOccasionDialog(), MessageReceivedAsync, activity);
                    return;
            }
        }

        private async Task CollectionsForOccasionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastOccasionSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            Occasion occasion = context.PrivateConversationData.GetValue<Occasion>("OccasionSelected");
            string gender = context.UserData.GetValue<string>("Gender");
            context.ConversationData.TryGetValue("CollectionsNextPage", out int currentPage);

            var collectionsApi = new ApiCaller<CollectionsRoot>();
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
                reply.Text = GetResourceValue<OccasionReplies>(nameof(OccasionReplies.Error), locale, occasion.Name.ToLower());
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = GetResourceValue<OccasionReplies>(nameof(OccasionBtn), locale),
                            Type = ActionTypes.PostBack, Value = "__reselect_occasion" }
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
                        Title = CapitalizeFirstLetter(occasion.Name) + " "
                            + GetResourceValue<GeneralReplies>(nameof(ForGender), locale,
                                GetResourceValue<GeneralReplies>((gender == "Male") ? nameof(Men) : nameof(Women), locale)),
                        Subtitle = GetResourceValue<OccasionReplies>(nameof(CollectionSubtitle), locale, occasion.Name.ToLower()),
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
                            Title = CapitalizeFirstLetter(collection.Title),
                            Subtitle = $"By {collection.StoreName}",
                            Buttons = new[] { new FacebookPostbackButton(
                                title: GetResourceValue<OccasionReplies>(nameof(ViewItemsBtn), locale),
                                payload: $"__view_collection_{collection.Id}") },
                            ImageUrl = collection.ImageUrl ??
                                $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Occasions/{occasion.Name}/{gender}/{i+1}.jpg"
                        });
                }

                FacebookPostbackButton bottomPageButton = null;
                if (!(currentPage == 0 && collectionsRoot.RemainingPages == 0) && collectionsRoot.RemainingPages > 0)
                    bottomPageButton = new FacebookPostbackButton(title: GetResourceValue<GeneralReplies>(nameof(SeeMore), locale), payload: "__more_collections");

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
                    new CardAction(){ Title = GetResourceValue<OccasionReplies>(nameof(OccasionBtn), locale), Type = ActionTypes.PostBack, Value = "__reselect_occasion" }
                }
            };

            await context.PostAsync(reply);
            context.Wait(MessageReceivedAsync);
        }

        private async Task ApparelsForCollectionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastOccasionSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            string gender = context.UserData.GetValue<string>("Gender");
            string collectionId = null;
            if (activity.Text.StartsWith("__view_collection_"))
                collectionId = activity.Text.Remove(0, "__view_collection_".Length);
            if (!string.IsNullOrWhiteSpace(collectionId))
                context.PrivateConversationData.SetValue("LastOccasionCollectionViewed", collectionId);
            else if (!context.PrivateConversationData.TryGetValue("LastOccasionCollectionViewed", out collectionId))
                await this.CollectionsForOccasionAsync(context, result);

            var collectionApparelsApi = new ApiCaller<CollectionApparelsRoot>();
            var collectionApparelsRoot = await collectionApparelsApi.CallAsync(new Dictionary<string, string>(1) { { "collection_id", collectionId } });

            reply.Text = GetResourceValue<OccasionReplies>(nameof(ShowOccasionItems), locale);
            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            foreach (var apparel in collectionApparelsRoot.Items)
            {
                reply.Attachments.Add(
                    new HeroCard()
                    {
                        Title = CapitalizeFirstLetter(apparel.Name),
                        Subtitle = apparel.PriceString + "€",
                        Images = new List<CardImage> { new CardImage { Url = apparel.ImageUrl } },
                        Buttons = new List<CardAction>
                        {
                            new CardAction { Title = "👍", Type = ActionTypes.PostBack, Value = $"__feedback_{apparel.Id}_-2" },
                            new CardAction { Title = "👎", Type = ActionTypes.PostBack, Value = $"__feedback_{apparel.Id}_-1" },
                            new CardAction { Title = GetResourceValue<GeneralReplies>(nameof(DetailsAndBuy), locale), Type = ActionTypes.OpenUrl, Value = apparel.Link }
                        },
                        Tap = new CardAction { Type = ActionTypes.OpenUrl, Value = apparel.Link }
                    }.ToAttachment());
            }

            reply.Attachments.Add(
                new HeroCard()
                {
                    Title = GetResourceValue<GeneralReplies>(nameof(FeedbackQ), locale),
                    Images = new List<CardImage> { new CardImage { Url = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Occasions/Feedback/feedback.jpg" } },
                    Buttons = new List<CardAction>
                    {
                        new CardAction { Title = GetResourceValue<GeneralReplies>(nameof(FeedbackA3), locale), Type = ActionTypes.PostBack, Value = $"__feedback_{collectionId}_3"},
                        new CardAction { Title = GetResourceValue<GeneralReplies>(nameof(FeedbackA2), locale), Type = ActionTypes.PostBack, Value = $"__feedback_{collectionId}_2"},
                        new CardAction { Title = GetResourceValue<GeneralReplies>(nameof(FeedbackA1), locale), Type = ActionTypes.PostBack, Value = $"__feedback_{collectionId}_1"}
                    }
                }.ToAttachment());

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(SeeMore), locale), Type = ActionTypes.PostBack, Value = "__more_collections" },
                    new CardAction(){ Title = GetResourceValue<OccasionReplies>(nameof(OccasionBtn), locale), Type = ActionTypes.PostBack, Value = "__reselect_occasion" }
                }
            };

            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task FeedbackAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            //context.PrivateConversationData.SetValue("LastOccasionSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            string[] feedbackData = activity.Text.Remove(0, "__feedback_".Length).Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries);

            int id = int.Parse(feedbackData[0]);
            int rate = int.Parse(feedbackData[1]);
            if (rate > 0)
                ;   //TODO: Feedback for collection
            else
                ;   //TODO: Feedback for apparel

            reply.Text = GetResourceValue<GeneralReplies>(nameof(FeedbackThanks), locale);
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(SeeMore), locale), Type = ActionTypes.PostBack, Value = "__more_collections" },
                    new CardAction(){ Title = GetResourceValue<OccasionReplies>(nameof(OccasionBtn), locale), Type = ActionTypes.PostBack, Value = "__reselect_occasion" }
                }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task UnimplementedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            context.UserData.TryGetValue("Locale", out string locale);

            await context.PostAsync(GetResourceValue<GeneralReplies>(nameof(Unimplemented), locale, activity.ChannelId));

            await this.SelectOccasionAsync(context, result);
        }
    }
}