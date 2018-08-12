using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Zoie.Helpers;
using Zoie.Resources.DialogReplies;

namespace Zoie.Petrichor.Dialogs.Main
{
    [Serializable]
    public class FiltersDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.ConversationData.SetValue("FilterStep", default(Filters));

            context.Wait(AskForFilterAsync);
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            var step = context.ConversationData.GetValue<Filters>("FilterStep");
            context.ConversationData.SetValue("FilterStep", ++step);

            await this.AskForFilterAsync(context, result);
        }

        private async Task AskForFilterAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();

            var filterStep = context.ConversationData.GetValue<Filters>("FilterStep");
            reply.Text = DialogsHelper.GetResourceValue<FilterReplies>("Ask" + filterStep);
            var quickReplies = new List<CardAction>();
            switch (filterStep)
            {
                case Filters.Color:
                    quickReplies = new List<CardAction>
                    {
                        new CardAction() { Title = "White", Type = ActionTypes.PostBack, Value = "white", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/white.jpg" },
                        new CardAction() { Title = "Pink", Type = ActionTypes.PostBack, Value = "pink", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/pink.jpg" },
                        new CardAction() { Title = "Blue", Type = ActionTypes.PostBack, Value = "blue", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/blue.jpg" },
                        new CardAction() { Title = "Brown", Type = ActionTypes.PostBack, Value = "brown", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/brown.jpg" },
                        new CardAction() { Title = "Green", Type = ActionTypes.PostBack, Value = "green", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/green.jpg" },
                        new CardAction() { Title = "Orange", Type = ActionTypes.PostBack, Value = "orange", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/orange.jpg" },
                        new CardAction() { Title = "Purple", Type = ActionTypes.PostBack, Value = "purple", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/purple.jpg" },
                        new CardAction() { Title = "Red", Type = ActionTypes.PostBack, Value = "red", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/red.jpg" },
                        new CardAction() { Title = "Black", Type = ActionTypes.PostBack, Value = "black", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/black.jpg" },
                        new CardAction() { Title = "Skip", Type = ActionTypes.PostBack, Value = "__filters_skip"},
                        new CardAction() { Title = "Done", Type = ActionTypes.PostBack, Value = "__filters_done"}
                    };
                    break;
                default:
                    quickReplies = new List<CardAction>
                    {
                        new CardAction() { Title = "Skip", Type = ActionTypes.PostBack, Value = "__filters_skip"},
                        new CardAction() { Title = "Done", Type = ActionTypes.PostBack, Value = "__filters_done"}
                    };
                    break;
            }
            reply.SuggestedActions = new SuggestedActions() { Actions = quickReplies };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        public enum Filters
        {
            Type,
            Color,
            Manufacturer,
            PriceRange,
            Size
        }
    }
}