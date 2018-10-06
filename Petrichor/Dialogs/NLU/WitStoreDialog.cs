using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;
using static Zoie.Helpers.NLUHelper;
using static Zoie.Petrichor.Dialogs.Main.StoreDialog;

namespace Zoie.Petrichor.Dialogs.NLU
{
    public class WitStoreDialog : WitDialog
    {
        protected override async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            //Helpers.DialogsHelper.EventToMessageActivity(ref activity, ref result);

            context.PrivateConversationData.TryGetValue("Filters", out ShoppingFilters filters);

            JObject nlpEntities = (activity.ChannelData as dynamic)?.message?.nlp?.entities;
            if (nlpEntities?.Count > 0)
            {
                foreach (var entity in nlpEntities.Children())
                {
                    string witValue = (entity.First.First as dynamic).value?.ToString();
                    if (witValue == "any")
                        continue;

                    switch (entity.ToObject<JProperty>().Name)
                    {
                        case WitEntities.AmountOfMoney:
                            filters.MinPrice = float.TryParse((entity.First.First as dynamic).from?.value.ToString(), out float minPrice) ? minPrice : default;
                            filters.MaxPrice = float.TryParse((entity.First.First as dynamic).to?.value.ToString(), out float maxPrice) ? maxPrice
                                             : float.TryParse((entity.First.First as dynamic).value?.ToString(), out maxPrice) ? maxPrice
                                             : default;
                            break;
                        case WitEntities.ApparelType:
                            filters.Type = witValue;
                            break;
                        case WitEntities.ApparelSize:
                            filters.Size = witValue;
                            break;
                        case WitEntities.ApparelColor:
                            filters.Color = witValue;
                            break;
                        case WitEntities.ApparelManufacturer:
                            filters.Manufacturer = witValue;
                            break;
                        case WitEntities.Number:
                            if (entity.First.Children().Count() > 1 && activity.Text.Contains("to "))
                            {
                                float[] numbers = new float[2]
                                {
                                        float.Parse((entity.First.Children().ElementAt(0) as dynamic).value.ToString()),
                                        float.Parse((entity.First.Children().ElementAt(1) as dynamic).value.ToString())
                                };

                                filters.MinPrice = numbers.Min();
                                filters.MaxPrice = numbers.Max();
                            }
                            else
                                goto case WitEntities.ApparelSize;
                            break;
                        case WitEntities.Gender:
                            filters.Gender = witValue;
                            break;
                    }
                }

                context.PrivateConversationData.SetValue("Filters", filters);
                context.Done(activity);
            }
            else
                await context.Forward(new PersonalityDialog(), AfterPersonalityDialogAsync, activity);
        }
    }
}