using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using Zoie.Helpers;
using Zoie.Petrichor.Models.Entities;
using Zoie.Resources.DialogReplies;

namespace Zoie.Petrichor.Dialogs.Main
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

            reply.Text = DialogsHelper.GetResourceValue<HandshakeReplies>("Entry", activity) + " " 
                + DialogsHelper.GetResourceValue<HandshakeReplies>("FollowUps", activity);
            await context.PostAsync(reply);


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

            try
            {
                await TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData).ExecuteAsync(TableOperation.Insert(userData));
            }
            catch (Exception)
            {
                await context.PostAsync(activity.CreateReply("Sorry, looks like there is something wrong with my database :( Please try again later."));
                context.Wait(MessageReceivedAsync);
                return;
            }

            context.Done(activity);
        }
    }
}