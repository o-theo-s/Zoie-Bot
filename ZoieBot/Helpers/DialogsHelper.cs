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

namespace ZoieBot.Helpers
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
    }
}