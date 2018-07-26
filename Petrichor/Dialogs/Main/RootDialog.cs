using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using Zoie.Helpers;
using Zoie.Petrichor.Dialogs.Main;
using Zoie.Petrichor.Models;
using Zoie.Petrichor.Dialogs.LUIS;

namespace Zoie.Petrichor.Dialogs.Main
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        public async Task StartAsync(IDialogContext context)
        {
            var tableResult = await TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData)
                .ExecuteAsync(TableOperation.Retrieve(context.Activity.From.Name, context.Activity.From.Id));

            ResumeAfter<object> resumeAfter;
            if (context.PrivateConversationData.ContainsKey("Referral"))
                resumeAfter = ReferralReceivedAsync;
            else if (context.PrivateConversationData.ContainsKey("MenuStore"))
            {
                context.PrivateConversationData.RemoveValue("MenuStore");
                resumeAfter = FunctionSelectedAsync;
            }
            else
                resumeAfter = SelectFunctionAsync;

            if (tableResult.Result == null)
                context.Call(new HandshakeDialog(), resumeAfter);
            else
                context.Wait(resumeAfter);
        }

        private async Task ReferralReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            Referral referral = context.PrivateConversationData.GetValue<Referral>("Referral");
            switch (referral.Type)
            {
                case Referral.Types.Store:
                    await context.Forward(new StoreDialog(), SelectFunctionAsync, await result as IMessageActivity);
                    return;
                case Referral.Types.Collection:
                    await context.Forward(new OccasionDialog(), SelectFunctionAsync, await result as IMessageActivity);
                    return;
                case Referral.Types.Invitation:
                    //TODO: Implement Invitation Dialog?
                    await this.SelectFunctionAsync(context, result);
                    return;
            }
        }

        private async Task SelectFunctionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply($"What do you have in mind for {GeneralHelper.GetDaytime(activity.LocalTimestamp ?? activity.Timestamp)}?");

            context.ConversationData.Clear();
            context.PrivateConversationData.Clear();

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "Occasion ✨", Type = ActionTypes.PostBack, Value = "__function_occasion" },
                    //new CardAction(){ Title = "Gift 🎁", Type = ActionTypes.PostBack, Value = "__function_gift" },
                    new CardAction(){ Title = "Stores 🏬", Type = ActionTypes.PostBack, Value = "__function_store" }
                }
            };
            await context.PostAsync(reply);

            context.Wait(FunctionSelectedAsync);
        }

        private async Task FunctionSelectedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            DialogsHelper.EventToMessageActivity(ref activity, ref result);

            if (!activity.Text.StartsWith("__"))
            {
                string message = activity.Text.ToLowerInvariant();

                if (message.Contains("occasion"))
                    activity.Text = "__function_occasion";
                else if (message.Contains("gift"))
                    activity.Text = "__function_gift";
                else if (message.Contains("store"))
                    activity.Text = "__function_store";
            }

            switch (activity.Text)
            {
                case "__function_occasion":
                    await context.Forward(new OccasionDialog(), SelectFunctionAsync, activity);
                    return;
                case "__function_gift":
                    await context.Forward(new GiftDialog(), SelectFunctionAsync, activity);
                    return;
                case string cmd when cmd == "__function_store" || cmd == "__menu_new_store":
                    await context.Forward(new StoreDialog(), SelectFunctionAsync, activity);
                    return;
                case "__personality_answer":
                    await context.PostAsync("Please select one of the options below:");
                    await SelectFunctionAsync(context, result);
                    return;
                case "__continue":
                    await SelectFunctionAsync(context, result);
                    return;
                default:
                    await context.Forward(new GlobalLuisDialog<object>(), FunctionSelectedAsync, activity);
                    return;
            }
        }
    }
}