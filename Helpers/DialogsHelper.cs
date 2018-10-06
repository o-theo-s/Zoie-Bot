using Autofac;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Connector;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Zoie.Apis;
using Zoie.Apis.Models;
using Zoie.Resources.DialogReplies;
using Newtonsoft.Json;
using static Zoie.Helpers.GeneralHelper;

namespace Zoie.Helpers
{
    public static class DialogsHelper
    {
        public static async Task ResetConversationAsync(IMessageActivity activity, bool deletePrivateConversationData = false)
        {
            using (var scope = DialogModule.BeginLifetimeScope(Conversation.Container, activity))
            {
                var botData = scope.Resolve<IBotData>();
                await botData.LoadAsync(new System.Threading.CancellationToken());

                var stack = scope.Resolve<IDialogStack>();
                stack.Reset();

                botData.ConversationData.Clear();
                if (deletePrivateConversationData)
                    botData.PrivateConversationData.Clear();

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

        public static async Task<List<CardAction>> GetOccasionSuggestedActionsAsync()
        {
            var occasionsApi = new ApiCaller<OccasionsRoot>();
            OccasionsRoot occasionsRoot = await occasionsApi.CallAsync();

            List<CardAction> suggestedActions = new List<CardAction>(occasionsRoot.Occasions.Count);

            foreach (var occasion in occasionsRoot.Occasions)
            {
                TryGetResourceValue<Emojis>(occasion.Name, out string emoji, null);
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

        public static string GetResourceValue<ResourceType>(string resourceName, string locale, params string[] replacements)
        {
            if (resourceName.Contains('.'))
                resourceName = resourceName.Split(new char[1] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last();

            string resourceValue = (typeof(ResourceType).GetProperty("ResourceManager").GetValue(null) as System.Resources.ResourceManager)
                .GetString(resourceName, new System.Globalization.CultureInfo(locale?.Replace('_', '-') ?? "en-US"));

            if (resourceValue.Contains("|||"))
            {
                string[] replies = resourceValue.Split(new string[1] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                resourceValue = replies.ElementAt(new Random().Next(replies.Length));
            }

            var regex = new System.Text.RegularExpressions.Regex(@"\{(.*?)\}");
            foreach (var replacement in replacements)
                resourceValue = regex.Replace(resourceValue, replacement, 1);

            return resourceValue;
        }

        public static bool TryGetResourceValue<ResourceType>(string resourceName, out string resourceValue, string locale, params string[] replacements)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                resourceValue = string.Empty;
                return false;
            }

            bool tore;
            try
            {
                resourceValue = GetResourceValue<ResourceType>(resourceName, locale, replacements);
                tore = true;
            }
            catch
            {
                resourceValue = string.Empty;
                tore = false;
            }

            return tore;
        }

        public static void EventToMessageActivity(ref Activity activity, ref IAwaitable<object> result)
        {
            if (activity.GetActivityType() == ActivityTypes.Event && activity.Value is Activity)
            {
                activity = activity.Value as Activity;
                result = new AwaitableFromItem<object>(activity);
            }
        }
    }
}