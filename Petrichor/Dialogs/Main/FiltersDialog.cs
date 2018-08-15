using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Zoie.Helpers;
using Zoie.Resources.DialogReplies;

namespace Zoie.Petrichor.Dialogs.Main
{
    [Serializable]
    public class FiltersDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            string[] filterNames = Enum.GetNames(typeof(Filters));
            var filters = new Dictionary<string, string>(filterNames.Length);
            for (int i = 0; i < filterNames.Length - 1; i++)
            {
                if (filterNames[i] == Filters.PriceRange.ToString())
                    continue;
                filters.Add(filterNames[i].ToLower(), null);
            }
            filters.Add("max_price", null);
            filters.Add("min_price", null);

            context.ConversationData.SetValue("FilterStep", default(Filters));
            context.PrivateConversationData.SetValue("Filters", filters);

            context.Wait(AskForFilterAsync);
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            DialogsHelper.EventToMessageActivity(ref activity, ref result);

            var filterStep = context.ConversationData.GetValue<Filters>("FilterStep");

            switch (activity.Text)
            {
                case "__filters_skip":
                    context.ConversationData.SetValue("FilterStep", ++filterStep);
                    await this.AskForFilterAsync(context, result);
                    return;
                case "__filters_done":
                    await this.EndAsync(context, result);
                    return;
                case string text when text.StartsWith("__filters_"):
                FilterInput:
                    string[] inputs = activity.Text.Split(new string[1] { "_" }, StringSplitOptions.RemoveEmptyEntries);

                    context.PrivateConversationData.TryGetValue("Filters", out Dictionary<string, string> filters);
                    if (inputs[1] == "pricerange")
                    {
                        string[] priceBoundaries = inputs[2].Split('-');
                        filters["min_price"] = priceBoundaries[0];
                        filters["max_price"] = priceBoundaries[1];
                    }
                    else
                        filters[inputs[1]] = inputs[2];
                    context.PrivateConversationData.SetValue("Filters", filters);

                    context.ConversationData.SetValue("FilterStep", ++filterStep);
                    await this.AskForFilterAsync(context, result);
                    return;
                case "__continue":
                    await this.AfterPersonalityDialogAsync(context, result);
                    return;
                default:
                    JObject witEntities = (activity.ChannelData as dynamic)?.message.nlp.entities;
                    if (witEntities != null && witEntities.HasValues)
                    {
                        if (filterStep == Filters.PriceRange && witEntities.ContainsKey("amount_of_money"))
                        {
                            dynamic amountOfMoney = (witEntities as dynamic).amount_of_money[0];
                            activity.Text = $"__filters_pricerange_{amountOfMoney.from?.value}-{amountOfMoney.to?.value}";
                        }
                        else if (filterStep == Filters.Size && witEntities.ContainsKey("number"))
                            activity.Text = $"__filters_size_{(witEntities as dynamic).number[0].value}";
                        else
                            activity.Text = (witEntities.GetValue("apparel_" + filterStep.ToString().ToLower()) as dynamic)?[0].metadata.ToString();

                        goto FilterInput;
                    }
                    else
                    {
                        await context.Forward(new PersonalityDialog(), AfterPersonalityDialogAsync, activity);
                        return;
                    }
            }
        }

        private async Task AskForFilterAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();

            context.PrivateConversationData.SetValue("LastFiltersSubdialog", GeneralHelper.GetActualAsyncMethodName());

            var filterStep = context.ConversationData.GetValue<Filters>("FilterStep");
            var quickReplies = new List<CardAction>();
            switch (filterStep)
            {
                case Filters.Completed:
                    await this.EndAsync(context, result);
                    return;
                case Filters.Color:
                    quickReplies = new List<CardAction>
                    {
                        new CardAction() { Title = "White", Type = ActionTypes.PostBack, Value = "__filters_color_white", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/white.jpg" },
                        new CardAction() { Title = "Pink", Type = ActionTypes.PostBack, Value = "__filters_color_pink", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/pink.jpg" },
                        new CardAction() { Title = "Blue", Type = ActionTypes.PostBack, Value = "__filters_color_blue", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/blue.jpg" },
                        new CardAction() { Title = "Brown", Type = ActionTypes.PostBack, Value = "__filters_color_brown", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/brown.jpg" },
                        new CardAction() { Title = "Green", Type = ActionTypes.PostBack, Value = "__filters_color_green", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/green.jpg" },
                        new CardAction() { Title = "Orange", Type = ActionTypes.PostBack, Value = "__filters_color_orange", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/orange.jpg" },
                        new CardAction() { Title = "Purple", Type = ActionTypes.PostBack, Value = "__filters_color_purple", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/purple.jpg" },
                        new CardAction() { Title = "Red", Type = ActionTypes.PostBack, Value = "__filters_color_red", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/red.jpg" },
                        new CardAction() { Title = "Black", Type = ActionTypes.PostBack, Value = "__filters_color_black", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/black.jpg" },
                        new CardAction() { Title = "Skip", Type = ActionTypes.PostBack, Value = "__filters_skip"},
                        new CardAction() { Title = "Done", Type = ActionTypes.PostBack, Value = "__filters_done"}
                    };
                    break;
                //Last step (before Completed)
                case Filters step when step == Enum.GetValues(typeof(Filters)).Cast<Filters>().Last((f) => f != Filters.Completed):
                    quickReplies = new List<CardAction>
                    {
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
            reply.Text = DialogsHelper.GetResourceValue<FilterReplies>("Ask" + filterStep);
            reply.SuggestedActions = new SuggestedActions() { Actions = quickReplies };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task EndAsync(IDialogContext context, IAwaitable<object> result)
        {
            context.ConversationData.RemoveValue("FilterStep");

            await context.PostAsync("Ok, let's find the best fit for you!");
            context.Done(await result);
        }

        private async Task AfterPersonalityDialogAsync(IDialogContext context, IAwaitable<object> result)
        {
            var lastSubdialog = context.PrivateConversationData.GetValue<string>("LastFiltersSubdialog");
            MethodInfo reshowLastSubdialog = this.GetType().GetMethod(lastSubdialog, BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task) reshowLastSubdialog.Invoke(this, new object[] { context, new AwaitableFromItem<IActivity>(context.Activity) });
        }

        public enum Filters
        {
            Type,
            Color,
            Manufacturer,
            PriceRange,
            Size,
            Completed
        }
    }
}