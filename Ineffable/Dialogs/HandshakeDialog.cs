using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Zoie.Helpers;
using Zoie.Resources.DialogReplies;

namespace Zoie.Ineffable.Dialogs
{
    [Serializable]
    public class HandshakeDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.UserData.SetValue("HasHandshaked", true);

            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();

            reply.Text = DialogsHelper.GetResourceValue<HandshakeReplies>("EntryIneffable", activity);
            await context.PostAsync(reply);
            reply.Text = DialogsHelper.GetResourceValue<HandshakeReplies>("FollowUpsIneffable", activity);
            await context.PostAsync(reply);

            if (activity.ChannelId == "facebook")
            {
                var localeAndGender = await ChannelsHelper.Facebook.GetUserDataAsync(activity.From.Id);
                if (localeAndGender != null)
                {
                    context.UserData.SetValue("Locale", localeAndGender.Item1);
                    context.UserData.SetValue("Gender", localeAndGender.Item2);
                }
            }

            context.Done(activity);
        }
    }
}