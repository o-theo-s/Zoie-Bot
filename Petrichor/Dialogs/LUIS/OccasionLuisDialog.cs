using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;

namespace Zoie.Petrichor.Dialogs.LUIS
{
    [Serializable]
    public class OccasionLuisDialog : GlobalLuisDialog<object>
    {
        [LuisIntent("Occasion")]
        public async Task OccasionIntentAsync(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;

            EntityRecommendation entity = luisResult.Entities?.FirstOrDefault(e => e.Type.Equals("Occasion#Type"));
            string occasionType = string.Empty;
            if (entity != null)
                occasionType = (string) (entity.Resolution?.Values?.FirstOrDefault() as List<object>)?.FirstOrDefault();

            activity.Text = $"__occasion_{occasionType}";

            context.Done(activity);
        }
    }
}