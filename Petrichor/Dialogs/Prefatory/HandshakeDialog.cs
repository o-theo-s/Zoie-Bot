using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using Zoie.Helpers;
using Zoie.Petrichor.Models.Entities;
using Zoie.Resources.DialogReplies;
using static Zoie.Helpers.DialogsHelper;
using static Zoie.Helpers.TablesHelper;

namespace Zoie.Petrichor.Dialogs.Prefatory
{
    [Serializable]
    public class HandshakeDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();

            UserData userData = new UserData(activity.From.Name ?? "User", activity.From.Id)
            {
                Locale = activity.Locale ?? "en_US",
                Channel = activity.ChannelId
            };

            if (activity.ChannelId == "facebook")
            {
                var localeAndGender = await ChannelsHelper.Facebook.GetUserDataAsync(userData.RowKey);
                if (localeAndGender != null)
                {
                    userData.Locale = localeAndGender.Item1;
                    userData.Gender = localeAndGender.Item2;
                }
            }

            if (!string.IsNullOrWhiteSpace(userData.Gender))
                context.UserData.SetValue("Gender", userData.Gender);
            if (!string.IsNullOrWhiteSpace(userData.Locale))
                context.UserData.SetValue("Locale", userData.Locale);

            try
            {
                await GetTableReference(TableNames.UsersData).ExecuteAsync(TableOperation.Insert(userData));
            }
            catch (Exception)
            {
                reply.Text = GetResourceValue<HandshakeReplies>(nameof(HandshakeReplies.Error), userData.Locale);
                await context.PostAsync(reply);
                context.Wait(MessageReceivedAsync);
                return;
            }

            reply.Text = GetResourceValue<HandshakeReplies>(nameof(HandshakeReplies.Entry), userData.Locale, activity.From.Name.Split(' ').FirstOrDefault());
            await context.PostAsync(reply);
            reply.Text = GetResourceValue<HandshakeReplies>(nameof(HandshakeReplies.FollowUps), userData.Locale);
            await context.PostAsync(reply);

            context.Done(activity);
        }
    }
}