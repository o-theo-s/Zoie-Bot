using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using ZoieBot.Models;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Autofac;

namespace ZoieBot.Dialogs
{
    [Serializable]
    public class HandshakeDialog : IDialog<object>
    {
        private ZoieUser User;

        public HandshakeDialog(ZoieUser user)
        {
            this.User = user;
        }

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            replyMessage.Text = $"Hello {User.Name.Split(' ').First()}! I am Zoie, your virtual fashion assistant and together we will explore the fashion world.";
            await context.PostAsync(replyMessage);

            //replyMessage.Text = "You can talk to me just like you would to an assistant in a real shopping mall. Ask me for anything from shoes to watches and I " +
            //    "will find the best fit for your style.";
            //await context.PostAsync(replyMessage);

            if (User.Gender == null)
            {
                replyMessage.Text = "Let's get to know each other! Are you a girl or a boy?";
                replyMessage.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Girl", Type = ActionTypes.ImBack, Value = "Girl" },
                        new CardAction(){ Title = "Boy", Type = ActionTypes.ImBack, Value = "Boy" }
                    }
                };
                await context.PostAsync(replyMessage);

                context.Wait(ContinueWithGenderAsync);
            }
            else
            {
                replyMessage.Text = "If you have any further questions don't hesitate to ask! Let's start! What is that you're looking for?";
                replyMessage.SuggestedActions = new SuggestedActions()
                {
                    ///TODO: Here will be shown the top products
                    Actions = new List<CardAction>()
                    {
                        new CardAction() { Title = "Complex search", Type = ActionTypes.ImBack, Value = "Complex search"},
                        new CardAction() { Title = "Black T-Shirts", Type = ActionTypes.ImBack, Value = "Nike shoes"},
                        new CardAction() { Title = "Lacoste watches", Type = ActionTypes.ImBack, Value = "Lacoste T-shirts"}
                    }
                };
                await context.PostAsync(replyMessage);

                context.Wait(ContinueWithShoppingAsync);
            }
        }

        private async Task ContinueWithGenderAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            if (activity.Text.ToLower().Contains("girl") || activity.Text.ToLower().Contains("woman"))
                User.Gender = Genders.Female;
            else if (activity.Text.ToLower().Contains("boy") || activity.Text.ToLower().Contains("man"))
                User.Gender = Genders.Male;
            else
            {
                replyMessage.Text = "I'm sorry, I didn't understand. Please say it again.";
                replyMessage.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Girl", Type = ActionTypes.ImBack, Value = "Girl" },
                        new CardAction(){ Title = "Boy", Type = ActionTypes.ImBack, Value = "Boy" }
                    }
                };
                await context.PostAsync(replyMessage);

                context.Wait(ContinueWithGenderAsync);
                return;
            }

            context.UserData.SetValue("ZoieUser", User);

            replyMessage.Text = "Nice! If you have any further questions don't hesitate to ask! Let's start! What is that you're looking for?";
            replyMessage.SuggestedActions = new SuggestedActions()
            {
                ///TODO: Here will be shown the top products
                Actions = new List<CardAction>()
                    {
                        new CardAction() { Title = "Complex search", Type = ActionTypes.ImBack, Value = "Complex search"},
                        new CardAction() { Title = "Nike shoes", Type = ActionTypes.ImBack, Value = "Nike shoes"},
                        new CardAction() { Title = "Lacoste T-shirts", Type = ActionTypes.ImBack, Value = "Lacoste T-shirts"}
                    }
            };
            await context.PostAsync(replyMessage);

            context.Wait(ContinueWithShoppingAsync);
        }

        private async Task ContinueWithShoppingAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            if (activity.Text.ToLower().Contains("complex search"))
            {
                //TODO: show complex search card
                replyMessage.Text = "Complex search is not yet completed.. Ask me to find you a product.";
                await context.PostAsync(replyMessage);

                context.Wait(MessageReceivedAsync);
            }
            else
            {
                await context.Forward(new Dialogs.RootLuisDialog(), HandshakeAfterAsync, activity);
            }
        }

        private async Task HandshakeAfterAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            User.IsHandshaked = true;

            context.UserData.SetValue("ZoieUser", User);

            //context.Wait(MessageReceivedAsync);
            context.Done(activity);
        }
    }
}