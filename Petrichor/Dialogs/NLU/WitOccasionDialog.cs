using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
using static Zoie.Helpers.NLUHelper;

namespace Zoie.Petrichor.Dialogs.NLU
{
    [Serializable]
    public class WitOccasionDialog : WitDialog
    {
        protected override async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            //Helpers.DialogsHelper.EventToMessageActivity(ref activity, ref result);

            string witOccasion = ((
                (activity.ChannelData as dynamic)?.message?.nlp?.entities as JObject)?
                .Children().FirstOrDefault(e => e.ToObject<JProperty>().Name == WitEntities.OccasionType)?
                .First.First as dynamic).value?.ToString();

            if (!string.IsNullOrEmpty(witOccasion))
            {
                activity.Text = "__occasion_" + witOccasion;
                context.Done(activity);
            }
            else
                await context.Forward(new PersonalityDialog(), AfterPersonalityDialogAsync, activity);
        }
    }
}