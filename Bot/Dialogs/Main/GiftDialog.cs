using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Zoie.Helpers;

namespace Zoie.Bot.Dialogs.Main
{
    [Serializable]
    public class GiftDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as IMessageActivity;

            // TODO: Put logic for handling user message here

            //context.Wait(MessageReceivedAsync);
            await this.UnimplementedAsync(context, result);
        }

        private async Task UnimplementedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            await context.PostAsync("Feature not available yet in " + GeneralHelper.CapitalizeFirstLetter(activity.ChannelId));

            await this.EndAsync(context, result);
        }

        private async Task EndAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result;

            //await context.PostAsync("Hope you found the best for that special person! ☺");
            context.Done(activity);
        }
    }
}