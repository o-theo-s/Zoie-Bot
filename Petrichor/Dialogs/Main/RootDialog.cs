using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage.Table;
using Zoie.Helpers;
using Zoie.Petrichor.Models;
using Zoie.Resources.DialogReplies;
using static Zoie.Helpers.DialogsHelper;
using static Zoie.Helpers.GeneralHelper;
using static Zoie.Resources.DialogReplies.RootReplies;
using static Zoie.Resources.DialogReplies.OccasionReplies;
using static Zoie.Resources.DialogReplies.StoreReplies;
using Zoie.Petrichor.Dialogs.Prefatory;
using Zoie.Petrichor.Dialogs.NLU;

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
            else if (context.PrivateConversationData.ContainsKey("MenuNew"))
            {
                context.PrivateConversationData.RemoveValue("MenuNew");
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
            var reply = activity.CreateReply();
            context.UserData.TryGetValue("Locale", out string locale);

            context.ConversationData.Clear();
            context.PrivateConversationData.Clear();

            reply.Text = GetResourceValue<RootReplies>(nameof(SelectFunction), locale, await GetDaytimeAsync(activity));
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = GetResourceValue<RootReplies>(nameof(MarketplaceBtn), locale), Type = ActionTypes.PostBack, Value = "__function_marketplace" },
                    new CardAction(){ Title = GetResourceValue<OccasionReplies>(nameof(OccasionBtn), locale), Type = ActionTypes.PostBack, Value = "__function_occasion" },
                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(StoresBtn), locale), Type = ActionTypes.PostBack, Value = "__function_store" },
                }
            };
            await context.PostAsync(reply);

            context.Wait(FunctionSelectedAsync);
        }

        private async Task FunctionSelectedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            EventToMessageActivity(ref activity, ref result);

            if (!activity.Text.StartsWith("__"))
            {
                string message = activity.Text.ToLowerInvariant();

                if (message.Contains("occasion"))
                    activity.Text = "__function_occasion";
                else if (message.Contains("store"))
                    activity.Text = "__function_store";
                else if (message.Contains("marketplace"))
                    activity.Text = "__function_marketplace";
            }

            switch (activity.Text)
            {
                case "__function_marketplace":
                    context.ConversationData.SetValue("MarketplaceSelected", true);
                    await context.Forward(new StoreDialog(), SelectFunctionAsync, activity);
                    return;
                case "__function_occasion":
                    await context.Forward(new OccasionDialog(), SelectFunctionAsync, activity);
                    return;
                case string cmd when cmd == "__function_store" || cmd == "__menu_new_store":
                    await context.Forward(new StoreDialog(), SelectFunctionAsync, activity);
                    return;
                case "__continue":
                    await SelectFunctionAsync(context, result);
                    return;
                default:
                    await context.Forward(new PersonalityDialog(), SelectFunctionAsync, activity);
                    return;
            }
        }
    }
}