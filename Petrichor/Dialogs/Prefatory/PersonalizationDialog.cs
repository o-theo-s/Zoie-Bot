using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using Zoie.Petrichor.Models.Entities;
using Zoie.Helpers;
using Zoie.Resources.DialogReplies;
using static Zoie.Helpers.DialogsHelper;
using static Zoie.Resources.DialogReplies.PersonalizationReplies;
using Zoie.Petrichor.Dialogs.NLU;
using static Zoie.Helpers.NLUHelper;

namespace Zoie.Petrichor.Dialogs.Main.Prefatory
{
    [Serializable]
    public class PersonalizationDialog : IDialog<object>
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
            context.UserData.TryGetValue("Locale", out string locale);

            PersonalizationSteps startStep = context.UserData.ContainsKey("Gender") ? PersonalizationSteps.Location : PersonalizationSteps.Gender;

            reply.Text = GetResourceValue<PersonalizationReplies>(nameof(Intro), locale);
            await context.PostAsync(reply);

            context.ConversationData.SetValue("PersonalizationStep", startStep);
            await this.AskPersonalizationQuestionAsync(context, result);
        }

        private async Task AskPersonalizationQuestionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.UserData.TryGetValue("Locale", out string locale);

            var step = context.ConversationData.GetValue<PersonalizationSteps>("PersonalizationStep");
            if (step == PersonalizationSteps.Location && context.Activity.ChannelId != "facebook")
                step += 1;

            switch (step)
            {
                case PersonalizationSteps.Gender:
                    reply.Text = GetResourceValue<PersonalizationReplies>(nameof(GenderQ), locale);
                    reply.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>
                        {
                            new CardAction() { Title = GetResourceValue<PersonalizationReplies>(nameof(Girl), locale), Type = ActionTypes.PostBack, Value = "__personalization_Gender_Female" },
                            new CardAction() { Title = GetResourceValue<PersonalizationReplies>(nameof(Boy), locale), Type = ActionTypes.PostBack, Value = "__personalization_Gender_Male" },
                            new CardAction() { Title = GetResourceValue<PersonalizationReplies>(nameof(Skip), locale), Type = ActionTypes.PostBack, Value = "__personalization_Skip_Gender" }
                        }
                    };
                    break;
                case PersonalizationSteps.Location:
                    reply.Text = GetResourceValue<PersonalizationReplies>(nameof(LocationQ), locale);
                    reply.ChannelData = ChannelsHelper.Facebook.AddLocationButton(reply.ChannelData);
                    reply.ChannelData = ChannelsHelper.Facebook.AddQuickReplyButton(reply.ChannelData, PersonalizationReplies.Skip, "__personalization_Skip_Location");
                    break;
                case PersonalizationSteps.Age:
                    reply.Text = GetResourceValue<PersonalizationReplies>(nameof(AgeQ), locale);
                    reply.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>
                        {
                            new CardAction() { Title = GetResourceValue<PersonalizationReplies>(nameof(Below16), locale), Type = ActionTypes.PostBack, Value = "__personalization_Age_<16" },
                            new CardAction() { Title = "16-22", Type = ActionTypes.PostBack, Value = "__personalization_Age_16-22" },
                            new CardAction() { Title = "23-29", Type = ActionTypes.PostBack, Value = "__personalization_Age_23-29" },
                            new CardAction() { Title = "30-36", Type = ActionTypes.PostBack, Value = "__personalization_Age_30-36" },
                            new CardAction() { Title = "37-45", Type = ActionTypes.PostBack, Value = "__personalization_Age_37-45" },
                            new CardAction() { Title = "46-52", Type = ActionTypes.PostBack, Value = "__personalization_Age_46-52" },
                            new CardAction() { Title = "53+", Type = ActionTypes.PostBack, Value = "__personalization_Age_>53" },
                            new CardAction() { Title = GetResourceValue<PersonalizationReplies>(nameof(Skip), locale), Type = ActionTypes.PostBack, Value = "__personalization_Skip_Age" }
                        }
                    };
                    break;
            }

            await context.PostAsync(reply);
            context.Wait(PersonalizationAnswerReceivedAsync);
        }

        private async Task PersonalizationAnswerReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = (await result ?? context.Activity) as Activity;
            EventToMessageActivity(ref activity, ref result);
            var reply = activity.CreateReply();
            context.UserData.TryGetValue("Locale", out string locale);

            PersonalizationSteps lastStep, nextStep;

            if (activity.ChannelId == "facebook")
            {
                var fbData = ChannelsHelper.Facebook.GetChannelData(activity.ChannelData.ToString());
                if (fbData != null)
                {
                    var coor = fbData.message?.attachments?.FirstOrDefault()?.payload?.coordinates;
                    if (coor != null)
                    {
                        ///TODO - Call maps API for city
                        activity.Text = $"__personalization_Location_({coor.lat}, {coor.@long})";
                    }
                }
            }

            if (activity.Text.StartsWith("__"))
            {
                string[] replyInfo;
                string answer = string.Empty;
                if (activity.Text == "__personality_answer" || activity.Text == "__continue")
                {
                    await this.AskPersonalizationQuestionAsync(context, result);
                    return;
                }
                else if (activity.Text.StartsWith("__wit__"))
                {
                    replyInfo = activity.Text.Remove(0, "__wit__".Length).Split(new string[1] { "__" }, StringSplitOptions.RemoveEmptyEntries);
                    switch (replyInfo[0])
                    {
                        case WitEntities.Gender:
                            lastStep = PersonalizationSteps.Gender;
                            answer = replyInfo[1];
                            break;
                        case WitEntities.Number:
                            lastStep = PersonalizationSteps.Age;
                            answer = CalculateAgeRange(int.Parse(replyInfo[1]));
                            break;
                        case WitEntities.Word:
                            if (replyInfo[1] == "skip")
                                lastStep = PersonalizationSteps.Skip;
                            else
                                goto default;
                            break;
                        default:
                            await context.PostAsync(GetResourceValue<PersonalizationReplies>(nameof(InvalidAnswer), locale));
                            await this.AskPersonalizationQuestionAsync(context, result);
                            return;
                    }
                }
                else
                {
                    replyInfo = activity.Text.Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                    lastStep = (PersonalizationSteps)Enum.Parse(typeof(PersonalizationSteps), replyInfo[1]);
                    answer = replyInfo[2];
                }

                if (answer == "Unknown")
                {
                    reply.Text = GetResourceValue<PersonalizationReplies>(nameof(PersonalizationReplies.Error), locale);
                    nextStep = lastStep;
                }
                else
                {
                    UserData userData = new UserData(activity.From.Name, activity.From.Id);
                    switch (lastStep)
                    {
                        case PersonalizationSteps.Gender:
                            userData.Gender = answer;
                            context.UserData.SetValue("Gender", answer);
                            reply.Text = GetResourceValue<PersonalizationReplies>(nameof(GenderA), locale);
                            break;
                        case PersonalizationSteps.Location:
                            userData.Location = answer;
                            reply.Text = GetResourceValue<PersonalizationReplies>(nameof(LocationA), locale);
                            break;
                        case PersonalizationSteps.Age:
                            userData.AgeGroup = answer;
                            break;
                        case PersonalizationSteps.Skip:
                            nextStep = context.ConversationData.GetValue<PersonalizationSteps>("PersonalizationStep") + 1;

                            reply.Text = GetResourceValue<PersonalizationReplies>(nameof(SkipA), locale);
                            await context.PostAsync(reply);

                            context.ConversationData.SetValue("PersonalizationStep", nextStep);
                            await this.AskPersonalizationQuestionAsync(context, result);
                            return;
                    }

                    try
                    {
                        await TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData).ExecuteAsync(TableOperation.InsertOrMerge(userData));
                        nextStep = lastStep + 1;
                    }
                    catch (Exception)
                    {
                        reply.Text = GetResourceValue<PersonalizationReplies>(nameof(ErrorSave), locale);
                        nextStep = lastStep;
                    }

                    if (nextStep == PersonalizationSteps.Completed)
                    {
                        reply.Text = GetResourceValue<PersonalizationReplies>(nameof(Finished), locale);
                        await context.PostAsync(reply);

                        await this.EndPersonalizationAsync(context, result);
                        return;
                    }
                }

                await context.PostAsync(reply);

                context.ConversationData.SetValue("PersonalizationStep", nextStep);
                await this.AskPersonalizationQuestionAsync(context, result);
            }
            else
            {
                await context.Forward(new WitDialog(), PersonalizationAnswerReceivedAsync, activity);
            }
        }

        private async Task EndPersonalizationAsync(IDialogContext context, IAwaitable<object> result)
        {
            context.ConversationData.RemoveValue("PersonalizationStep");
            context.UserData.SetValue("HasPersonalized", true);

            if (!context.UserData.ContainsKey("Gender"))
                context.UserData.SetValue("Gender", "Female");

            context.Done(await result);
        }

        internal enum PersonalizationSteps
        {
            Gender = 1, Location, Age,
            Completed, Skip
        }
    }
}