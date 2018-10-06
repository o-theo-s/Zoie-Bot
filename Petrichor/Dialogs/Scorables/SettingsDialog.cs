using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Zoie.Helpers;
using Zoie.Petrichor.Dialogs.NLU;
using Zoie.Petrichor.Models.Entities;
using Zoie.Resources.DialogReplies;
using static Zoie.Resources.DialogReplies.SettingsReplies;
using static Zoie.Resources.DialogReplies.GeneralReplies;
using static Zoie.Helpers.DialogsHelper;

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
            EventToMessageActivity(ref activity, ref result);

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

            string lastSubdialog;
            switch (activity.Text)
            {
                case "__menu_settings_my_gender":
                    await this.GenderPromptAsync(context, result);
                    return;
                case string text when text.StartsWith("__settings_gender_change"):
                    if (context.UserData.TryGetValue("Gender", out string gender))
                        activity.Text += "_" + gender == "Male" ? "Female" : "Male";

                    await this.GenderChangeAsync(context, result);
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
                    lastSubdialog = context.PrivateConversationData.GetValue<string>("LastSettingsSubdialog");
                    MethodInfo reshowLastSubdialog = this.GetType().GetMethod(lastSubdialog, BindingFlags.NonPublic | BindingFlags.Instance);

                    await (Task) reshowLastSubdialog.Invoke(this, new object[] { context, result });
                    return;
                case "__continue":
                    context.Done(activity);
                    return;
                case string text when text.StartsWith("__wit__"):
                    string[] entityInfo = text.Remove(0, "__wit__".Length).Split(new string[1] { "__" }, StringSplitOptions.RemoveEmptyEntries);
                    if (entityInfo[0] == NLUHelper.WitEntities.Gender)
                    {
                        activity.Text = "__settings_gender_change_" + entityInfo[1];
                        await this.MessageReceivedAsync(context, result);
                        return;
                    }
                    else if (entityInfo[0] == NLUHelper.WitEntities.Number)
                    {
                        if (int.TryParse(entityInfo[1], out int age) && age > 5)
                        {
                            activity.Text = "__settings_age_change_" + NLUHelper.CalculateAgeRange(age);
                            await this.MessageReceivedAsync(context, result);
                            return;
                        }
                        else
                        {
                            context.UserData.TryGetValue("Locale", out string locale);
                            await context.PostAsync(activity.CreateReply(GetResourceValue<SettingsReplies>(nameof(InvalidAge), locale)));
                            await this.AgeAskAsync(context, result);
                            return;
                        }
                    }
                    else if (entityInfo[0] == NLUHelper.WitEntities.Word)
                    {
                        lastSubdialog = context.PrivateConversationData.GetValue<string>("LastSettingsSubdialog");
                        switch (entityInfo[1])
                        {
                            case "yes":
                                if (lastSubdialog == nameof(this.GenderPromptAsync))
                                {
                                    activity.Text = "__settings_gender_change";
                                    await this.MessageReceivedAsync(context, result);
                                    return;
                                }
                                else if (lastSubdialog == nameof(this.AgePromptAsync))
                                {
                                    activity.Text = "__settings_age_change";
                                    await this.MessageReceivedAsync(context, result);
                                    return;
                                }
                                return;
                            case "no":
                            case "cancel":
                                await this.EndAsync(context, result);
                                return;
                        }
                    }
                    return;
                default:
                    await context.Forward(new WitDialog(), MessageReceivedAsync, activity);
                    return;
            }
        }

        private async Task GenderPromptAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            if (context.UserData.TryGetValue("Gender", out string gender))
            {
                reply.Text = GetResourceValue<SettingsReplies>(nameof(GenderCurrent), locale) + " " + GetResourceValue<GeneralReplies>(gender, locale);
                await context.PostAsync(reply);

                reply.Text = GetResourceValue<SettingsReplies>(nameof(ChangePrompt), locale);
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(Yes), locale), Type = ActionTypes.PostBack, Value = "__settings_gender_change" },
                        new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(No), locale), Type = ActionTypes.PostBack, Value = "__settings_gender_nochange" }
                    }
                };
                await context.PostAsync(reply);
            }
            else
            {
                reply.Text = GetResourceValue<SettingsReplies>(nameof(GenderNotSet), locale);
                await context.PostAsync(reply);

                reply.Text = GetResourceValue<SettingsReplies>(nameof(GenderQ), locale);
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = GetResourceValue<SettingsReplies>(nameof(Girl), locale), Type = ActionTypes.PostBack, Value = "__settings_gender_change_Female" },
                        new CardAction(){ Title = GetResourceValue<SettingsReplies>(nameof(Boy), locale), Type = ActionTypes.PostBack, Value = "__settings_gender_change_Male" },
                        new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(Cancel), locale), Type = ActionTypes.PostBack, Value = "__settings_cancel" }
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
            context.UserData.TryGetValue("Locale", out string locale);

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

                reply.Text = GetResourceValue<SettingsReplies>(nameof(GenderSuccess), locale);
                await context.PostAsync(reply);

                await this.EndAsync(context, result);
            }
            catch (Exception)
            {
                reply.Text = GetResourceValue<SettingsReplies>(nameof(ErrorSave), locale);
                await context.PostAsync(reply);

                reply.Text = GetResourceValue<SettingsReplies>(nameof(GenderQ), locale);
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = GetResourceValue<SettingsReplies>(nameof(Girl), locale), Type = ActionTypes.PostBack, Value = "__settings_gender_change_Female" },
                        new CardAction(){ Title = GetResourceValue<SettingsReplies>(nameof(Boy), locale), Type = ActionTypes.PostBack, Value = "__settings_gender_change_Male" },
                        new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(Cancel), locale), Type = ActionTypes.PostBack, Value = "__settings_cancel" }
                    }
                };
                await context.PostAsync(reply);

                context.Wait(MessageReceivedAsync);
            }
        }

        private async Task AgePromptAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

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
                reply.Text = GetResourceValue<SettingsReplies>(nameof(AgeGroupCurrent), locale) + " " + ageGroup;
                await context.PostAsync(reply);

                reply.Text = GetResourceValue<SettingsReplies>(nameof(ChangePrompt), locale);
            }
            else
            {
                reply.Text = GetResourceValue<SettingsReplies>(nameof(AgeGroupNotSet), locale);
                await context.PostAsync(reply);

                reply.Text = GetResourceValue<SettingsReplies>(nameof(SetPrompt), locale);
            }

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(Yes), locale), Type = ActionTypes.PostBack, Value = "__settings_age_change" },
                    new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(No), locale), Type = ActionTypes.PostBack, Value = "__settings_age_nochange" }
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
            context.UserData.TryGetValue("Locale", out string locale);

            reply.Text = GetResourceValue<SettingsReplies>(nameof(AgeQ), locale);
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>
                    {
                        new CardAction() { Title = GetResourceValue<SettingsReplies>(nameof(Below16), locale), Type = ActionTypes.PostBack, Value = "__settings_age_change_<16" },
                        new CardAction() { Title = "16-22", Type = ActionTypes.PostBack, Value = "__settings_age_change_16-22" },
                        new CardAction() { Title = "23-29", Type = ActionTypes.PostBack, Value = "__settings_age_change_23-29" },
                        new CardAction() { Title = "30-36", Type = ActionTypes.PostBack, Value = "__settings_age_change_30-36" },
                        new CardAction() { Title = "37-45", Type = ActionTypes.PostBack, Value = "__settings_age_change_37-45" },
                        new CardAction() { Title = "46-52", Type = ActionTypes.PostBack, Value = "__settings_age_change_46-52" },
                        new CardAction() { Title = "53+", Type = ActionTypes.PostBack, Value = "__settings_age_change_>53" },
                        new CardAction() { Title = GetResourceValue<GeneralReplies>(nameof(Cancel), locale), Type = ActionTypes.PostBack, Value = "__settings_cancel" }
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
            context.UserData.TryGetValue("Locale", out string locale);

            try
            {
                string newAgeGroup = activity.Text.Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries).Last();

                UserData userData = new UserData(activity.From.Name, activity.From.Id)
                {
                    AgeGroup = newAgeGroup,
                    ETag = "*"
                };
                await TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData).ExecuteAsync(TableOperation.Merge(userData));

                reply.Text = GetResourceValue<SettingsReplies>(nameof(AgeGroupSuccess), locale);
                await context.PostAsync(reply);

                await this.EndAsync(context, result);
            }
            catch (Exception)
            {
                reply.Text = GetResourceValue<SettingsReplies>(nameof(ErrorSave), locale);
                await context.PostAsync(reply);

                await this.AgeAskAsync(context, result);
            }
        }

        private async Task LocationAskAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastSettingsSubdialog", GeneralHelper.GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            reply.Text = GetResourceValue<SettingsReplies>(nameof(AgeGroupSuccess), locale);
            if (activity.ChannelId == "facebook")
            {
                reply.ChannelData = ChannelsHelper.Facebook.AddLocationButton(reply.ChannelData);
                reply.ChannelData = ChannelsHelper.Facebook.AddQuickReplyButton(reply.ChannelData, GetResourceValue<GeneralReplies>(nameof(Cancel), locale), "__settings_cancel");
            }
            else
            {
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>
                    {
                        new CardAction() { Title = GetResourceValue<GeneralReplies>(nameof(Cancel), locale), Type = ActionTypes.PostBack, Value = "__settings_cancel" }
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
            context.UserData.TryGetValue("Locale", out string locale);

            try
            {
                string newLocation = activity.Text.Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries).Last();

                UserData userData = new UserData(activity.From.Name, activity.From.Id)
                {
                    Location = newLocation,
                    ETag = "*"
                };
                await TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData).ExecuteAsync(TableOperation.Merge(userData));

                reply.Text = GetResourceValue<SettingsReplies>(nameof(LocationSuccess), locale);
                await context.PostAsync(reply);

                await this.EndAsync(context, result);
            }
            catch (Exception)
            {
                reply.Text = GetResourceValue<SettingsReplies>(nameof(ErrorSave), locale);
                await context.PostAsync(reply);

                await this.LocationAskAsync(context, result);
            }
        }

        private async Task EndAsync(IDialogContext context, IAwaitable<object> result)
        {
            context.UserData.TryGetValue("Locale", out string locale);
            await context.PostAsync(GetResourceValue<SettingsReplies>(nameof(ContinueFlow), locale));

            context.Done(await result);
        }
    }
}