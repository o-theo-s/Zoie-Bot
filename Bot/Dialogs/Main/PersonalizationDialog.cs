using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Apis.Models;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using Zoie.Bot.Dialogs.LUIS;
using Zoie.Bot.Models.Entities;
using Zoie.Helpers;

namespace Zoie.Bot.Dialogs.Main
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

            PersonalizationSteps startStep = context.UserData.ContainsKey("Gender") ? PersonalizationSteps.Location : PersonalizationSteps.Gender;
            Occasion occasion = context.ConversationData.GetValue<Occasion>("OccasionSelected");

            reply.Text = $"Before I show you some of the best outfits for {occasion.Name}, it would be great to know you a little bit better! ☺";
            await context.PostAsync(reply);

            context.ConversationData.SetValue("PersonalizationStep", startStep);
            await this.AskPersonalizationQuestionAsync(context, result);
        }

        private async Task AskPersonalizationQuestionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            var step = context.ConversationData.GetValue<PersonalizationSteps>("PersonalizationStep");

            if (step == PersonalizationSteps.Location && context.Activity.ChannelId != "facebook")
                step += 1;

            switch (step)
            {
                case PersonalizationSteps.GenderAgain:
                case PersonalizationSteps.Gender:
                    reply.Text = "Are you a girl or a boy?";
                    reply.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>
                        {
                            new CardAction() { Title = "Girl 👧", Type = ActionTypes.PostBack, Value = "__personalization_Gender_Female" },
                            new CardAction() { Title = "Boy 👦", Type = ActionTypes.PostBack, Value = "__personalization_Gender_Male" },
                            new CardAction() { Title = "Skip", Type = ActionTypes.PostBack, Value = "__personalization_Skip_Gender" },
                            new CardAction() { Title = "Quit", Type = ActionTypes.PostBack, Value = "__personalization_Quit_Empty" }
                        }
                    };
                    break;
                case PersonalizationSteps.LocationAgain:
                case PersonalizationSteps.Location:
                    reply.Text = "Could I please access your location to make sure the outfits I suggest match the weather out there?";
                    reply.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>
                        {
                            new CardAction() { Title = "Skip", Type = ActionTypes.PostBack, Value = "__personalization_Skip_Location" },
                            new CardAction() { Title = "Quit", Type = ActionTypes.PostBack, Value = "__personalization_Quit_Empty" }
                        }
                    };
                    reply.ChannelData = ChannelsHelper.Facebook.AddLocationButton(reply.ChannelData);
                    reply.ChannelData = ChannelsHelper.Facebook.AddQuickReplyButton(reply.ChannelData, "Skip", "__personalization_Skip_Location");
                    reply.ChannelData = ChannelsHelper.Facebook.AddQuickReplyButton(reply.ChannelData, "Quit", "__personalization_Quit_Empty");
                    break;
                case PersonalizationSteps.AgeAgain:
                case PersonalizationSteps.Age:
                    reply.Text = "I know we are all kids at heart, but how old are you?";
                    reply.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>
                        {
                            new CardAction() { Title = "Below 16", Type = ActionTypes.PostBack, Value = "__personalization_Age_<16" },
                            new CardAction() { Title = "16-22", Type = ActionTypes.PostBack, Value = "__personalization_Age_16-22" },
                            new CardAction() { Title = "23-29", Type = ActionTypes.PostBack, Value = "__personalization_Age_23-29" },
                            new CardAction() { Title = "30-36", Type = ActionTypes.PostBack, Value = "__personalization_Age_30-36" },
                            new CardAction() { Title = "37-45", Type = ActionTypes.PostBack, Value = "__personalization_Age_37-45" },
                            new CardAction() { Title = "46-52", Type = ActionTypes.PostBack, Value = "__personalization_Age_46-52" },
                            new CardAction() { Title = "53+", Type = ActionTypes.PostBack, Value = "__personalization_Age_>53" },
                            new CardAction() { Title = "Skip", Type = ActionTypes.PostBack, Value = "__personalization_Skip_Age" },
                            new CardAction() { Title = "Quit", Type = ActionTypes.PostBack, Value = "__personalization_Quit_Empty" }
                        }
                    };
                    break;
                case PersonalizationSteps.StyleAgain:
                case PersonalizationSteps.Style:
                    reply.Text = "One last question! What style of the below describes you the best?";
                    reply.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>
                        {
                            new CardAction() { Title = "Casual", Type = ActionTypes.PostBack, Value = "__personalization_Style_Casual" },
                            new CardAction() { Title = "Trendy", Type = ActionTypes.PostBack, Value = "__personalization_Style_Trendy" },
                            new CardAction() { Title = "Elegant", Type = ActionTypes.PostBack, Value = "__personalization_Style_Elegant" },
                            new CardAction() { Title = "Artsy", Type = ActionTypes.PostBack, Value = "__personalization_Style_Artsy" },
                            new CardAction() { Title = "Sporty", Type = ActionTypes.PostBack, Value = "__personalization_Style_Sporty" },
                            new CardAction() { Title = "Business", Type = ActionTypes.PostBack, Value = "__personalization_Style_Business" },
                            new CardAction() { Title = "Quit", Type = ActionTypes.PostBack, Value = "__personalization_Quit_Empty" }
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
            var reply = activity.CreateReply();

            string answer;
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
                string[] replyInfo = activity.Text.Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                lastStep = (PersonalizationSteps)Enum.Parse(typeof(PersonalizationSteps), replyInfo[1]);
                answer = replyInfo[2];

                if (answer == "Unknown")
                {
                    reply.Text = "Sorry, I didn't get that. Can you tell me once more?";
                    nextStep = lastStep;
                }
                else
                {
                    UserData userData = new UserData(activity.From.Name, activity.From.Id);
                    switch (lastStep)
                    {
                        case PersonalizationSteps.Gender:
                        case PersonalizationSteps.GenderAgain:
                            userData.Gender = answer;
                            context.UserData.SetValue("Gender", answer);
                            break;
                        case PersonalizationSteps.Location:
                        case PersonalizationSteps.LocationAgain:
                            userData.Location = answer;
                            break;
                        case PersonalizationSteps.Age:
                        case PersonalizationSteps.AgeAgain:
                            userData.AgeGroup = answer;
                            break;
                        case PersonalizationSteps.Style:
                        case PersonalizationSteps.StyleAgain:
                            userData.Style = answer;
                            break;
                        case PersonalizationSteps.Skip:
                            nextStep = (PersonalizationSteps)Enum.Parse(typeof(PersonalizationSteps), answer) + 1;

                            reply.Text = "Alright! No problem 🙂 Let's move on!";
                            await context.PostAsync(reply);

                            context.ConversationData.SetValue("PersonalizationStep", nextStep);
                            await this.AskPersonalizationQuestionAsync(context, result);
                            return;
                        case PersonalizationSteps.Quit:
                            reply.Text = "Got it. If you change your mind and want more personalized results, let me know!";
                            await context.PostAsync(reply);

                            await this.EndPersonalizationAsync(context, result);
                            return;
                    }

                    try
                    {
                        await TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData).ExecuteAsync(TableOperation.InsertOrMerge(userData));
                        reply.Text = "Nice!";
                        nextStep = lastStep + 1;
                    }
                    catch (Exception)
                    {
                        reply.Text = "Sorry, I couldn't save your answer 🙁. Can you tell me once more?";
                        nextStep = lastStep;
                    }

                    if (nextStep == PersonalizationSteps.Completed)
                    {
                        reply.Text = "Perfect! Now that I know you better, you will get more personalized results! 😁";
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
                await context.Forward(new PersonalizationLuisDialog(), PersonalizationAnswerReceivedAsync, activity);
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
            StyleAgain = -4, AgeAgain, LocationAgain, GenderAgain,
            Gender = 1, Location, Age, Style,
            Completed, Skip, Quit = 0
        }
    }
}