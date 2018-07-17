using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Zoie.Bot.Dialogs.LUIS
{
    [Serializable]
    public class PersonalizationLuisDialog : GlobalLuisDialog<object>
    {
        public new async Task NoneIdentifiedAsync(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
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
                await base.NoneIdentifiedAsync(context, result, luisResult);
            }
        }

        [LuisIntent("Personalization:Gender")]
        public async Task GenderIntentAsync(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;

            EntityRecommendation entity = luisResult.Entities?.FirstOrDefault(e => e.Type.Equals("Personalization:Gender#Entity"));
            string gender = "Unknown";
            if (entity != null)
            {
                gender = (string) (entity.Resolution?.Values?.FirstOrDefault() as List<object>)?.FirstOrDefault();
                gender = char.ToUpper(gender.First()) + gender.Substring(1);
            }

            activity.Text = $"__personalization_Gender_{gender}";
            context.Done(activity);
        }

        [LuisIntent("Personalization:Age")]
        public async Task AgeIntentAsync(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;

            EntityRecommendation entity = luisResult.Entities?.FirstOrDefault(e => e.Type.Equals("builtin.number"));
            string ageGroup = "Unknown";
            if (entity != null)
            {
                int.TryParse(entity.Entity, out int age);

                if (age < 5)
                    ageGroup = "Unknown";
                else if (age < 16)
                    ageGroup = "<16";
                else if (age <= 22)
                    ageGroup = "16-22";
                else if (age <= 29)
                    ageGroup = "23-29";
                else if (age <= 36)
                    ageGroup = "30-36";
                else if (age <= 45)
                    ageGroup = "37-45";
                else if (age <= 52)
                    ageGroup = "46-52";
                else
                    ageGroup = ">53";
            }

            activity.Text = $"__personalization_Age_{ageGroup}";
            context.Done(activity);
        }
    }
}