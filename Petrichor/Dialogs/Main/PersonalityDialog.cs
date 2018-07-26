using System;
using System.Linq;
using Microsoft.Bot.Builder.PersonalityChat;
using Microsoft.Bot.Builder.PersonalityChat.Core;

namespace Zoie.Petrichor.Dialogs.Main
{
    [Serializable]
    public class PersonalityDialog : PersonalityChatDialog<object>
    {
        public PersonalityDialog()
        {
            PersonalityChatDialogOptions personalityChatDialogOptions = new PersonalityChatDialogOptions();

            this.SetPersonalityChatDialogOptions(personalityChatDialogOptions);
        }

        public override string GetResponse(PersonalityChatResults personalityChatResults)
        {
            var matchedScenarios = personalityChatResults?.ScenarioList;

            string response = string.Empty;

            if (matchedScenarios != null)
            {
                var topScenario = matchedScenarios.FirstOrDefault();

                if (topScenario?.Responses != null /*&& topScenario.Score > this.personalityChatDialogOptions.ScenarioThresholdScore*/ && topScenario.Responses.Count > 0)
                {
                    Random randomGenerator = new Random();
                    int randomIndex = randomGenerator.Next(topScenario.Responses.Count);

                    response = topScenario.Responses[randomIndex];
                }
            }

            return response;
        }
    }
}