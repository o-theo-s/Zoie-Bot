using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Zoie.Petrichor.Dialogs.NLU
{
    [Serializable]
    public class WitDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        protected virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            //Helpers.DialogsHelper.EventToMessageActivity(ref activity, ref result);

            JToken witEntity = ((activity.ChannelData as dynamic)?.message?.nlp?.entities as JObject)?.Children().FirstOrDefault();
            string witValue = (witEntity?.First.First as dynamic).value?.ToString();

            if (!string.IsNullOrEmpty(witValue))
            {
                activity.Text = $"__wit__{witEntity.ToObject<JProperty>().Name}__{witValue}";
                context.Done(activity);
            }
            else
                await context.Forward(new PersonalityDialog(), AfterPersonalityDialogAsync, activity);
        }

        protected Task AfterPersonalityDialogAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = context.Activity as Activity;
            activity.Text = "__personality_answer";

            context.Done(activity);

            return Task.CompletedTask;
        }
    }
}