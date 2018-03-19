using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using ZoieBot.Models;
using System.Net.Http;
using System.Configuration;
using Newtonsoft.Json;

namespace ZoieBot.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            context.UserData.TryGetValue("IsHandshaked", out bool isHandshaked);
            if (activity.Text == "__get_started__" || !isHandshaked)
            {
                await FirstMessageHandshakeAsync(context, activity);
                return;
            }

            if (activity.Text.StartsWith("__menu") && activity.Text.EndsWith("__"))
            {
                await ContinueWithMenuActionAsync(context, result);
                return;
            }

            activity.Text = "__forward_luis__" + activity.Text;
            await context.Forward(new ShoppingDialog(), MessageReceivedAsync, activity);
        }

        private async Task ContinueWithMenuActionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            switch (activity.Text)
            {
                case "__menu_new_search__":
                case "__menu_top_products__":
                    await context.Forward(new ShoppingDialog(), MessageReceivedAsync, activity);
                    return;
                case "__menu_help__":
                    activity.Text = "__forward_luis__What is Zoie?";
                    await context.Forward(new ShoppingDialog(), MessageReceivedAsync, activity);
                    return;
                case "__menu_exit_store__":
                    context.PrivateConversationData.TryGetValue("StoreFilter", out string tempStore);
                    context.PrivateConversationData.RemoveValue("StoreFilter");

                    replyMessage.Text = tempStore == null ? "You are not inside any store." : $"Exited from {tempStore} store.";
                    await context.PostAsync(replyMessage);

                    await context.Forward(new ShoppingDialog(), MessageReceivedAsync, activity);
                    return;
                case "__menu_options_wishlist__":
                    //Show wishlist items
                    break;
                case "__menu_options_purchased_items__":
                    //Show purchased items
                    break;
                case "__menu_options_delete_personal_data__":
                    context.UserData.Clear();
                    context.ConversationData.Clear();
                    context.PrivateConversationData.Clear();

                    replyMessage.Text = "Allright! I no longer have any of your personal information.";
                    await context.PostAsync(replyMessage);
                    break;
            }

            context.Wait(MessageReceivedAsync);
        }

        private async Task ContinueWithGenderAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();
            string messageLowercase = activity.Text.ToLower();

            if (messageLowercase.Contains("girl") || messageLowercase.Contains("woman") || messageLowercase.Contains("female"))
            {
                context.UserData.SetValue("Gender", "woman");
                replyMessage.Text = "Allright!";
            }
            else if (messageLowercase.Contains("boy") || messageLowercase.Contains("man") || messageLowercase.Contains("male"))
            {
                context.UserData.SetValue("Gender", "man");
                replyMessage.Text = "Allright!";
            }
            else if (messageLowercase == "__skipped")
            {
                context.UserData.SetValue("Gender", "not_set");
                replyMessage.Text = "If you change your mind you can edit your info in your profile.";
            }
            else
            {
                replyMessage.Text = "Please choose one of the above";
                replyMessage.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Girl", Type = ActionTypes.PostBack, Value = "__girl" },
                        new CardAction(){ Title = "Boy", Type = ActionTypes.PostBack, Value = "__boy" },
                        new CardAction(){ Title = "Skip", Type = ActionTypes.PostBack, Value = "__skipped" }
                    }
                };
                await context.PostAsync(replyMessage);

                context.Wait(ContinueWithGenderAsync);
                return;
            }

            await context.PostAsync(replyMessage);

            activity.Text = "__";
            await context.Forward(new ShoppingDialog(), MessageReceivedAsync, activity);
        }

        /*
         * =====================================================================================================================
         */

        private async Task FirstMessageHandshakeAsync(IDialogContext context, Activity activity)
        {
            var replyMessage = activity.CreateReply();

            replyMessage.Text = (activity.ChannelId == "webchat") ? string.Empty : $"Hello {activity.From.Name?.Split(' ').FirstOrDefault()}! ";
            replyMessage.Text += "I am Zoie, your virtual assistant in the world of fashion! " +
                $"Search for anything you want, like 'blue shoes', or upload a picture and I'll find the best matches for you! Let's start :D";
            await context.PostAsync(replyMessage);

            context.UserData.SetValue("Name", activity.From.Name ?? "you");
            context.UserData.SetValue("IsHandshaked", true);
            context.UserData.SetValue("Locale", activity.Locale ?? "en_US");
            context.UserData.SetValue("SubscribedAt", DateTime.UtcNow);
            context.UserData.SetValue("ChannelsIds", new Dictionary<string, string>() { { activity.ChannelId, activity.From.Id } });

            if (activity.ChannelId == "facebook")
            {
                try
                {
                    HttpClient client = new HttpClient
                    {
                        BaseAddress = new Uri($"https://graph.facebook.com/v2.6/{activity.From.Id}")
                    };
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    HttpResponseMessage response = await client.GetAsync($"?fields=locale,gender&access_token={ConfigurationManager.AppSettings["FacebookPageAccessToken"]}");
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        var fbInfo = JsonConvert.DeserializeObject<FacebookUserInfo>(json);

                        context.UserData.SetValue("Locale", fbInfo.Locale);

                        if (fbInfo.Genger == "female")
                            context.UserData.SetValue("Gender", "woman");
                        else if (fbInfo.Genger == "male")
                            context.UserData.SetValue("Gender", "man");
                    }
                }
                catch { }
            }
            else
            {
                replyMessage.Text = $"First things first! Are you a boy or a girl?";
                replyMessage.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Girl", Type = ActionTypes.PostBack, Value = "__girl" },
                        new CardAction(){ Title = "Boy", Type = ActionTypes.PostBack, Value = "__boy" },
                        new CardAction(){ Title = "Skip", Type = ActionTypes.PostBack, Value = "__skipped" }
                    }
                };
                await context.PostAsync(replyMessage);

                context.Wait(ContinueWithGenderAsync);
                return;
            }

            activity.Text = "__";
            await context.Forward(new ShoppingDialog(), MessageReceivedAsync, activity);
        }
    }
}