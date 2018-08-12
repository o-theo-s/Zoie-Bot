using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Zoie.Ineffable.Dialogs
{
    [Serializable]
    public class HotelDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.ConversationData.SetValue("NextReservationStep", ReservationSteps.Location);

            context.Wait(PromptAsync);

            return Task.CompletedTask;
        }

        private async Task PromptAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();

            var nextReservationStep = context.ConversationData.GetValue<ReservationSteps>("NextReservationStep");
            switch (nextReservationStep)
            {
                case ReservationSteps.End:
                default:
                    context.ConversationData.RemoveValue("Location");
                    goto case default(ReservationSteps);
                case ReservationSteps.Location:
                    reply.Text = "What city are you thinking for your vacations?";
                    break;
                case ReservationSteps.Dates:
                    string location = context.ConversationData.GetValue<string>("Location");
                    reply.Text = $"When would you like to travel? Please specify the dates of your check in and check out in your hotel in {location}.";
                    break;
            }
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            var nextReservationStep = context.ConversationData.GetValue<ReservationSteps>("NextReservationStep");
            

            switch (activity.Text)
            {
                case string text when text.StartsWith("__confirmed_"):
                    bool confirmed = bool.Parse(text.Remove(0, "__confirmed_".Length));
                    var kvp = context.ConversationData.GetValue<KeyValuePair<string, string>>("ValueToConfirm");

                    if (confirmed)
                    {
                        context.ConversationData.SetValue(kvp.Key, kvp.Value);
                        context.ConversationData.RemoveValue("ValueToConfirm");
                        await context.PostAsync($"{kvp.Key} changed successfully.");
                    }
                    else
                    {
                        await context.PostAsync($"{kvp.Key} was not changed.");
                    }

                    await this.PromptAsync(context, result);
                    return;
                default:
                    dynamic entities = (activity.ChannelData as dynamic).message.nlp?.entities;
                    switch (entities)
                    {
                        case JObject jEntities when jEntities.ContainsKey("location"):
                            string location = (jEntities as dynamic).location[0].value.ToString();
                            if (nextReservationStep == ReservationSteps.Location)
                            {
                                context.ConversationData.SetValue("Location", location);
                                context.ConversationData.SetValue("NextReservationStep", ++nextReservationStep);
                                await this.PromptAsync(context, result);
                            }
                            else
                            {
                                context.ConversationData.SetValue("ValueToConfirm", new KeyValuePair<string, string>("Location", location));
                                PromptDialog.Confirm(
                                    context,
                                    resume: AfterPromptAsync,
                                    prompt: $"So you want to change the location to \"{location}\"?",
                                    retry: "Please reply with \"yes\" or \"no\".",
                                    attempts: 2,
                                    options: new string[] { "Yes", "No" });
                            }
                            return;
                        case JObject jEntities when jEntities.ContainsKey("datetime"):
                            List<DateTime> dates = new List<DateTime>(2);
                            List<dynamic> stringDatetimes = new List<dynamic>(collection: (jEntities as dynamic).datetime);

                            if (stringDatetimes.Any((d) => d.type.ToString() == "interval"))
                            {
                                dynamic datetimeInterval = stringDatetimes.First((d) => d.type.ToString() == "interval");
                                dates.Add(DateTime.Parse(datetimeInterval.to.value.ToString()));
                                dates.Add(DateTime.Parse(datetimeInterval.from.value.ToString()));
                            }
                            else
                            {
                                foreach (dynamic date in stringDatetimes)
                                    dates.Add(DateTime.Parse(date.value.ToString()));
                                dates.OrderBy((d) => d.Date);
                            }

                            if (nextReservationStep == ReservationSteps.Dates)
                            {
                                context.ConversationData.SetValue("Dates", dates);
                                context.ConversationData.SetValue("NextReservationStep", ++nextReservationStep);
                                await this.PromptAsync(context, result);
                            }
                            else
                            {
                                context.ConversationData.SetValue("ValueToConfirm", new KeyValuePair<string, List<DateTime>>("Dates", dates));
                                string promptMsg = "Are you sure want to change the reservation date";
                                int startPromptMsgLenght = promptMsg.Length;

                                if (dates.Count >= 2)
                                    promptMsg += $"s to {dates.First().Date} and {dates.Last().Date}?";
                                else if (dates.Count == 1)
                                {
                                    string text = activity.Text.ToLowerInvariant();
                                    if (text.Contains("check"))
                                    {
                                        var currentDates = context.ConversationData.GetValue<List<DateTime>>("Dates");
                                        string[] textWords = text.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                        string check = textWords.SkipWhile((s) => s != "check").First();
                                        if (check == "in" || check == "out")
                                            promptMsg += $" for check {check}?";
                                    }
                                }

                                if (startPromptMsgLenght == promptMsg.Length)
                                    PromptDialog.Choice(
                                        context,
                                        resume: AfterPromptAsync,
                                        options: new string[2] { "Check in", "Check out" },
                                        prompt: $"What is the selected date ({dates.First().Date}) for?",
                                        retry: $"Please select \"Check in\" or \"Check out\"",
                                        attempts: 2);
                                else
                                    PromptDialog.Confirm(
                                        context,
                                        resume: AfterPromptAsync,
                                        prompt: promptMsg,
                                        options: new string[2] { "Yes", "No" },
                                        attempts: 2);
                            }
                            return;
                        default:
                            //Call personality dialog
                            //After personality:
                            await this.PromptAsync(context, result);
                            return;
                    }
            }
        }

        private async Task AfterPromptAsync<T>(IDialogContext context, IAwaitable<T> result)
        {
            T res = await result;
            var activity = context.Activity as Activity;

            switch (res)
            {
                case bool confirm:
                    activity.Text = $"__confirmed_{confirm}";
                    break;
                case string choice:
                    activity.Text = $"__choice_{choice}";
                    break;
            }

            await this.MessageReceivedAsync(context, new AwaitableFromItem<IMessageActivity>(activity.AsMessageActivity()));
        }

        private enum ReservationSteps
        {
            Location, Dates, End
        }
    }
}