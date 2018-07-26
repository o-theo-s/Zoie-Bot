using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using Zoie.Helpers;
using Zoie.Petrichor.Dialogs.LUIS;
using Zoie.Petrichor.Models.Entities;

namespace Zoie.Petrichor.Dialogs.Scorables
{
    [Serializable]
    public class SettingsDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            DialogsHelper.EventToMessageActivity(ref activity, ref result);
            var reply = activity.CreateReply();

            if (activity.ChannelId == "facebook")
            {
                var fbData = ChannelsHelper.Facebook.GetChannelData(activity.ChannelData.ToString());
                if (fbData != null)
                {
                    var coor = fbData.message?.attachments?.FirstOrDefault()?.payload?.coordinates;
                    if (coor != null)
                    {
                        ///TODO - Call maps API for city
                        activity.Text = $"__settings_location_change_({coor.lat}, {coor.@long})";
                    }
                }
            }

            switch (activity.Text)
            {
                case "__menu_settings_my_gender":
                    await this.GenderPromptAsync(context, result);
                    return;
                case string text when text.StartsWith("__settings_gender_change"):
                    await this.GenderChangeAsync(context, result);
                    return;

                case "__menu_settings_my_style":
                    await this.StylePromptAsync(context, result);
                    return;
                case "__settings_style_change":
                    await this.StyleAskAsync(context, result);
                    return;
                case string text when text.StartsWith("__settings_style_change"):
                    await this.StyleChangeAsync(context, result);
                    return;

                case "__menu_settings_my_age":
                    await this.AgePromptAsync(context, result);
                    return;
                case "__settings_age_change":
                    await this.AgeAskAsync(context, result);
                    return;
                case string text when text.StartsWith("__settings_age_change"):
                    await this.AgeChangeAsync(context, result);
                    return;

                case "__menu_settings_change_location":
                    await this.LocationAskAsync(context, result);
                    return;
                case string text when text.StartsWith("__settings_location_change"):
                    await this.LocationChangeAsync(context, result);
                    return;

                case string text when text.EndsWith("nochange"):
                case "__settings_cancel":
                    await this.EndAsync(context, result);
                    return;


                case "__personality_answer":
                    var lastSubdialog = context.PrivateConversationData.GetValue<string>("LastSettingsSubdialog");
                    MethodInfo reshowLastSubdialog = this.GetType().GetMethod(lastSubdialog, BindingFlags.NonPublic | BindingFlags.Instance);

                    await (Task) reshowLastSubdialog.Invoke(this, new object[] { context, result });
                    return;
                case "__continue":
                    context.Done(activity);
                    return;
                default:
                    await context.Forward(new GlobalLuisDialog<object>(), MessageReceivedAsync, activity);
                    return;
            }
        }

        private async Task GenderPromptAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());

            if (context.UserData.TryGetValue("Gender", out string gender))
            {
                reply.Text = "Your current gender selection is: " + gender;
                await context.PostAsync(reply);

                reply.Text = "Would you like to change it?";
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Yes", Type = ActionTypes.PostBack, Value = $"__settings_gender_change_{ (gender == "Male" ? "Female" : "Male")}" },
                        new CardAction(){ Title = "No", Type = ActionTypes.PostBack, Value = "__settings_gender_nochange" }
                    }
                };
                await context.PostAsync(reply);
            }
            else
            {
                reply.Text = "Your gender has not been set yet.";
                await context.PostAsync(reply);

                reply.Text = "Are you a boy or a girl?";
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Girl 👧", Type = ActionTypes.PostBack, Value = "__settings_gender_change_Female" },
                        new CardAction(){ Title = "Boy 👦", Type = ActionTypes.PostBack, Value = "__settings_gender_change_Male" },
                        new CardAction(){ Title = "Cancel", Type = ActionTypes.PostBack, Value = "__settings_cancel" }
                    }
                };
                await context.PostAsync(reply);
            }

            context.Wait(MessageReceivedAsync);
        }

        private async Task GenderChangeAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());

            try
            {
                string newGender = activity.Text.Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries).Last();

                UserData userData = new UserData(activity.From.Name, activity.From.Id)
                {
                    Gender = newGender,
                    ETag = "*"
                };
                await TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData).ExecuteAsync(TableOperation.Merge(userData));
                context.UserData.SetValue("Gender", newGender);

                reply.Text = "Your gender has been changed successfully!";
                await context.PostAsync(reply);

                await this.EndAsync(context, result);
            }
            catch (Exception)
            {
                reply.Text = "Sorry, I couldn't save your answer 🙁. Can you tell me once more?";
                await context.PostAsync(reply);

                reply.Text = "Are you a boy or a girl?";
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Girl 👧", Type = ActionTypes.PostBack, Value = "__settings_gender_change_Female" },
                        new CardAction(){ Title = "Boy 👦", Type = ActionTypes.PostBack, Value = "__settings_gender_change_Male" },
                        new CardAction(){ Title = "Cancel", Type = ActionTypes.PostBack, Value = "__settings_cancel" }
                    }
                };
                await context.PostAsync(reply);

                context.Wait(MessageReceivedAsync);
            }
        }

        private async Task StylePromptAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());

            string style = null;
            try
            {
                TableResult tableResult = await TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData)
                    .ExecuteAsync(TableOperation.Retrieve(activity.From.Name, activity.From.Id, new List<string>(1) { nameof(UserData.Style) }));
                style = (tableResult?.Result as DynamicTableEntity)?.Properties[nameof(UserData.Style)].StringValue;
            }
            catch { }

            if (!string.IsNullOrEmpty(style))
            {
                reply.Text = "Your current style selection is: " + style;
                await context.PostAsync(reply);

                reply.Text = "Would you like to change it?";
            }
            else
            {
                reply.Text = "Your style has not been set yet.";
                await context.PostAsync(reply);

                reply.Text = "Would you like to set in now?";
            }
                
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "Yes", Type = ActionTypes.PostBack, Value = "__settings_style_change" },
                    new CardAction(){ Title = "No", Type = ActionTypes.PostBack, Value = "__settings_style_nochange" }
                }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task StyleAskAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());

            reply.Text = "What style of the below describes you the best?";
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>
                    {
                        new CardAction() { Title = "Casual", Type = ActionTypes.PostBack, Value = "__settings_style_change_Casual" },
                        new CardAction() { Title = "Trendy", Type = ActionTypes.PostBack, Value = "__settings_style_change_Trendy" },
                        new CardAction() { Title = "Elegant", Type = ActionTypes.PostBack, Value = "__settings_style_change_Elegant" },
                        new CardAction() { Title = "Artsy", Type = ActionTypes.PostBack, Value = "__settings_style_change_Artsy" },
                        new CardAction() { Title = "Sporty", Type = ActionTypes.PostBack, Value = "__settings_style_change_Sporty" },
                        new CardAction() { Title = "Business", Type = ActionTypes.PostBack, Value = "__settings_style_change_Business" },
                        new CardAction() { Title = "Cancel", Type = ActionTypes.PostBack, Value = "__settings_cancel" }
                    }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task StyleChangeAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());

            try
            {
                string newStyle = activity.Text.Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries).Last();

                UserData userData = new UserData(activity.From.Name, activity.From.Id)
                {
                    Style = newStyle,
                    ETag = "*"
                };
                await TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData).ExecuteAsync(TableOperation.Merge(userData));

                reply.Text = "Your style has been changed successfully!";
                await context.PostAsync(reply);

                await this.EndAsync(context, result);
            }
            catch (Exception)
            {
                reply.Text = "Sorry, I couldn't save your answer 🙁. Can you tell me once more?";
                await context.PostAsync(reply);

                await this.StyleAskAsync(context, result);
            }
        }

        private async Task AgePromptAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());

            string ageGroup = null;
            try
            {
                TableResult tableResult = await TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData)
                    .ExecuteAsync(TableOperation.Retrieve(activity.From.Name, activity.From.Id, new List<string>(1) { nameof(UserData.AgeGroup) }));
                ageGroup = (tableResult?.Result as DynamicTableEntity)?.Properties[nameof(UserData.AgeGroup)].StringValue;
            }
            catch { }

            if (!string.IsNullOrEmpty(ageGroup))
            {
                reply.Text = "Your current age-group selection is: " + ageGroup;
                await context.PostAsync(reply);

                reply.Text = "Would you like to change it?";
            }
            else
            {
                reply.Text = "Your age-group has not been set yet.";
                await context.PostAsync(reply);

                reply.Text = "Would you like to set in now?";
            }

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "Yes", Type = ActionTypes.PostBack, Value = "__settings_age_change" },
                    new CardAction(){ Title = "No", Type = ActionTypes.PostBack, Value = "__settings_age_nochange" }
                }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task AgeAskAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());

            reply.Text = "I know we are all kids at heart, but how old are you?";
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>
                    {
                        new CardAction() { Title = "Below 16", Type = ActionTypes.PostBack, Value = "__settings_age_change_<16" },
                        new CardAction() { Title = "16-22", Type = ActionTypes.PostBack, Value = "__settings_age_change_16-22" },
                        new CardAction() { Title = "23-29", Type = ActionTypes.PostBack, Value = "__settings_age_change_23-29" },
                        new CardAction() { Title = "30-36", Type = ActionTypes.PostBack, Value = "__settings_age_change_30-36" },
                        new CardAction() { Title = "37-45", Type = ActionTypes.PostBack, Value = "__settings_age_change_37-45" },
                        new CardAction() { Title = "46-52", Type = ActionTypes.PostBack, Value = "__settings_age_change_46-52" },
                        new CardAction() { Title = "53+", Type = ActionTypes.PostBack, Value = "__settings_age_change_>53" },
                        new CardAction() { Title = "Cancel", Type = ActionTypes.PostBack, Value = "__settings_cancel" }
                    }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task AgeChangeAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());

            try
            {
                string newAgeGroup = activity.Text.Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries).Last();

                UserData userData = new UserData(activity.From.Name, activity.From.Id)
                {
                    AgeGroup = newAgeGroup,
                    ETag = "*"
                };
                await TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData).ExecuteAsync(TableOperation.Merge(userData));

                reply.Text = "Your age-group has been changed successfully!";
                await context.PostAsync(reply);

                await this.EndAsync(context, result);
            }
            catch (Exception)
            {
                reply.Text = "Sorry, I couldn't save your answer 🙁. Can you tell me once more?";
                await context.PostAsync(reply);

                await this.AgeAskAsync(context, result);
            }
        }

        private async Task LocationAskAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());

            reply.Text = "What is your location?";
            if (activity.ChannelId == "facebook")
            {
                reply.ChannelData = ChannelsHelper.Facebook.AddLocationButton(reply.ChannelData);
                reply.ChannelData = ChannelsHelper.Facebook.AddQuickReplyButton(reply.ChannelData, "Cancel", "__settings_cancel");
            }
            else
            {
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>
                    {
                        new CardAction() { Title = "Cancel", Type = ActionTypes.PostBack, Value = "__settings_cancel" }
                    }
                };
            }
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task LocationChangeAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());

            try
            {
                string newLocation = activity.Text.Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries).Last();

                UserData userData = new UserData(activity.From.Name, activity.From.Id)
                {
                    Location = newLocation,
                    ETag = "*"
                };
                await TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData).ExecuteAsync(TableOperation.Merge(userData));

                reply.Text = "Your location has been changed successfully!";
                await context.PostAsync(reply);

                await this.EndAsync(context, result);
            }
            catch (Exception)
            {
                reply.Text = "Sorry, I couldn't save your answer 🙁. Can you tell me once more?";
                await context.PostAsync(reply);

                await this.LocationAskAsync(context, result);
            }
        }

        private async Task EndAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply("Alright! Let's continue from where we left of..");

            await context.PostAsync(reply);

            context.Done(activity);
        }
    }
}