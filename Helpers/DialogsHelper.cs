using Autofac;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Connector;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;
using Apis;
using Apis.Models;
using Zoie.Resources.DialogReplies;
using Newtonsoft.Json;

namespace Zoie.Helpers
{
    public static class DialogsHelper
    {
        public static async Task ResetConversationAsync(IMessageActivity activity)
        {
            using (var scope = DialogModule.BeginLifetimeScope(Conversation.Container, activity))
            {
                var botData = scope.Resolve<IBotData>();
                await botData.LoadAsync(new System.Threading.CancellationToken());

                var stack = scope.Resolve<IDialogStack>();
                stack.Reset();

                botData.ConversationData.Clear();
                await botData.FlushAsync(new System.Threading.CancellationToken());
            }
        }

        public static async Task DeleteConversationAndUserDataAsync(IMessageActivity activity)
        {
            using (var scope = DialogModule.BeginLifetimeScope(Conversation.Container, activity))
            {
                var botData = scope.Resolve<IBotData>();
                await botData.LoadAsync(new System.Threading.CancellationToken());

                botData.ConversationData.Clear();
                botData.UserData.Clear();
                botData.PrivateConversationData.Clear();

                await botData.FlushAsync(new System.Threading.CancellationToken());
            }
        }

        public static async Task SetValueInPrivateConversationDataAsync<T>(IMessageActivity activity, string valueKey, T value)
        {
            using (var scope = DialogModule.BeginLifetimeScope(Conversation.Container, activity))
            {
                var botData = scope.Resolve<IBotData>();
                await botData.LoadAsync(new System.Threading.CancellationToken());

                botData.PrivateConversationData.SetValue(valueKey, value);
                await botData.FlushAsync(new System.Threading.CancellationToken());
            }
        }


        public static class ShoppingDialogHelper
        {
            public static void SearchMessageBuilder(ref StringBuilder stringBuilder, string value, string startWith = null, string endWith = null)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    stringBuilder.Append(startWith);
                    stringBuilder.Append($" {value}");
                    stringBuilder.Append(endWith);
                }
            }
        }

        public static bool GetReplyForOccasion(string occasionType, ref Activity reply)
        {
            string[] replies;
            Random replySelector = new Random();
            switch (occasionType.ToLower())
            {
                case "work":
                    replies = OccasionReplies.Work.Split(new string[1] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                    reply.Text = replies.ElementAt(replySelector.Next(replies.Length));
                    return true;
                case "wedding":
                    replies = OccasionReplies.Wedding.Split(new string[1] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                    reply.Text = replies.ElementAt(replySelector.Next(replies.Length));
                    return true;
                case "university":
                    replies = OccasionReplies.University.Split(new string[1] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                    reply.Text = replies.ElementAt(replySelector.Next(replies.Length));
                    return true;
                case "party":
                    replies = OccasionReplies.Party.Split(new string[1] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                    reply.Text = replies.ElementAt(replySelector.Next(replies.Length));
                    return true;
                case "outdoor":
                    replies = OccasionReplies.Outdoor.Split(new string[1] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                    reply.Text = replies.ElementAt(replySelector.Next(replies.Length));
                    return true;
                case "interview":
                    replies = OccasionReplies.Interview.Split(new string[1] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                    reply.Text = replies.ElementAt(replySelector.Next(replies.Length));
                    return true;
                case "gym":
                    replies = OccasionReplies.Gym.Split(new string[1] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                    reply.Text = replies.ElementAt(replySelector.Next(replies.Length));
                    return true;
                case "cocktail":
                    replies = OccasionReplies.Cocktail.Split(new string[1] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                    reply.Text = replies.ElementAt(replySelector.Next(replies.Length));
                    return true;
                default:
                    replies = OccasionReplies.UnknownOccasion.Split(new string[1] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                    reply.Text = replies.ElementAt(replySelector.Next(replies.Length));

                    reply.SuggestedActions = new SuggestedActions()
                    {
                        Actions = DialogsHelper.GetOccasionSuggestedActionsAsync().Result
                    };
                    return false;
            }
        }

        public static async Task<List<CardAction>> GetOccasionSuggestedActionsAsync()
        {
            var occasionsApi = new API<OccasionsRoot>();
            OccasionsRoot occasionsRoot = await occasionsApi.CallAsync();

            List<CardAction> suggestedActions = new List<CardAction>(occasionsRoot.Occasions.Count);

            foreach (var occasion in occasionsRoot.Occasions)
            {
                DialogsHelper.TryGetResourceValue<Emojis>(occasion.Name, out string emoji);
                suggestedActions.Add(
                    new CardAction()
                    {
                        Title = occasion.Name + " " + emoji,
                        Type = ActionTypes.PostBack,
                        Value = $"__occasion_{JsonConvert.SerializeObject(occasion)}"
                    });
            }

            return suggestedActions;
        }

        public static string GetResourceValue<ResourceType>(string resourceName, IMessageActivity activity = null)
        {
            string resourceValue = typeof(ResourceType).GetProperty(resourceName).GetValue(null) as string;

            if (activity != null)
            {
                string firstName = activity.From?.Name.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
                string daytime = GeneralHelper.GetDaytime(activity.LocalTimestamp ?? activity.Timestamp);
                resourceValue = resourceValue.Replace("{timeOfDay}", daytime).Replace("{name}", firstName);
            }

            string[] replies = resourceValue.Split(new string[1] { "|||" }, StringSplitOptions.RemoveEmptyEntries);

            return replies.ElementAt(new Random().Next(replies.Length));
        }

        public static bool TryGetResourceValue<ResourceType>(string resourceName, out string reply, IMessageActivity activity = null)
        {
            bool tore;
            try
            {
                reply = GetResourceValue<ResourceType>(resourceName, activity);
                tore = true;
            }
            catch
            {
                reply = string.Empty;
                tore = false;
            }

            return tore;
        }
    }
}