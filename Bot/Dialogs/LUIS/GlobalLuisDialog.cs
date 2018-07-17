using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using Zoie.Bot.Dialogs.Main;

namespace Zoie.Bot.Dialogs.LUIS
{
    //European LUIS - ZoieV2
    [LuisModel(
        modelID:            "b6e28f40-f13e-4dc8-97f6-5f740d8e783f ", 
        subscriptionKey:    "283025aac3e04d7d8946a64ab4a77f80", 
        domain:             "westeurope.api.cognitive.microsoft.com")]
    [Serializable]
    public class GlobalLuisDialog<TResult> : LuisDialog<TResult>
    {
        [LuisIntent("")]
        [LuisIntent("None")]
        public async Task NoneIdentifiedAsync(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;

            EntityRecommendation entity = luisResult.Entities?.FirstOrDefault(e => e.Type.Equals("Personalization:Style#Entity"));
            string style = "Unknown";
            if (entity != null)
            {
                style = (string)(entity.Resolution?.Values?.FirstOrDefault() as List<object>)?.FirstOrDefault();
                activity.Text = $"__personalization_Style_{style}";

                context.Done(activity);
            }
            else
            {
                await context.Forward(new PersonalityDialog(), AfterPersonalityDialogAsync, await result);
            }
        }

        private async Task AfterPersonalityDialogAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = context.Activity as Activity;
            activity.Text = "__personality_answer";

            context.Done(activity);
        }
    }
}