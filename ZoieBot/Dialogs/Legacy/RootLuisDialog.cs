using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using ZoieBot.Helpers;
using ZoieBot.Models;
using ZoieBot.Models.SearchAPI;

namespace ZoieBot.Dialogs
{
    //European LUIS
    //[LuisModel("c63eb073-f284-42da-a536-49d28b2294ce", "70b25e78f45e45239ce4fe967afc3be1", domain: "westeurope.api.cognitive.microsoft.com")]

    //American LUIS
    [LuisModel("6a70ebea-6c59-4461-9ee1-090c779a7389", "4312563ba2124fbea40a818d56a8c851")]
    [Serializable]
    public class RootLuisDialog : LuisDialog<object>
    {
        private ZoieUser User;

        public RootLuisDialog()
        {
            //this.User = user;
        }

        [LuisIntent("")]
        [LuisIntent("None")]
        public async Task None(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;

            LastLuisResult lastLuisResult = GetLastLuisResult(context);
            if (lastLuisResult?.GoToTopIntent ?? false)
            {
                luisResult.TopScoringIntent = lastLuisResult.Value.TopScoringIntent;
                switch (lastLuisResult.Value.TopScoringIntent.Intent)
                {
                    case "Shopping.CatalogInformation":
                        await CatalogInformation(context, result, luisResult);
                        return;
                    case "Shopping.GeneralInformation":
                        await GeneralInformation(context, result, luisResult);
                        return;
                    case "Shopping.Shop":
                        if (activity.Text == "New search")
                        {
                            SetLastSearchFilters(context, null);
                            context.ConversationData.RemoveValue("CarouselPage");
                        }
                        await Shop(context, result, luisResult);
                        return;
                    default:
                        break;
                }
            }

            var replyMessage = context.MakeMessage();

            string[] messageWords = activity.Text.Split(' ');
            if (messageWords.Length == 1 || (messageWords.Length == 2 && (messageWords[0] == "a" || messageWords[0] == "an")))
            {
                if (!(lastLuisResult.Value.TopScoringIntent.Intent == "Shopping.Shop"))
                {
                    replyMessage.Text = ":)";
                    await context.PostAsync(replyMessage);
                    replyMessage.Text = "Try something of the following";
                    replyMessage.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = "Shop", Type=ActionTypes.ImBack, Value="I want to shop with Zoie." },
                            new CardAction(){ Title = "Get to know Zoie", Type=ActionTypes.ImBack, Value="I need some help." },
                            new CardAction(){ Title = "See top products", Type=ActionTypes.ImBack, Value="Show me the top trends."}
                        }
                    };
                }
                else
                    replyMessage.Text = "That's not helping at all...";
               
                await context.PostAsync(replyMessage);

                SetLastLuisResult(context, luisResult, goToTopIntent: false);
                context.Wait(this.MessageReceived);
                //context.Done(activity);
                return;
            }
            

            string[] replies = new string[] {
                "I'm sorry, but I only can help you with your shopping!",
                "Sorry, I didn't understand."
            };

            replyMessage.Text = replies.ElementAt(new Random().Next(replies.Length)) + " Try something of the following";
            replyMessage.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Shop", Type=ActionTypes.ImBack, Value="I want to shop with Zoie." },
                        new CardAction(){ Title = "Get to know Zoie", Type=ActionTypes.ImBack, Value="I need some help." },
                        new CardAction(){ Title = "See top products", Type=ActionTypes.ImBack, Value="Show me the top trends."}
                    }
            };
            await context.PostAsync(replyMessage);

            SetLastLuisResult(context, luisResult, goToTopIntent: false);
            context.Wait(this.MessageReceived);
            //context.Done(activity);
        }

        [LuisIntent("AbusiveContent")]
        public async Task AbusiveContent(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            string[] replies = new string[] {
                "I have never offended you so I demand to speak nice to me!",
                "Stop abusing me or I will report you."
            };

            var activity = await result as Activity;
            var replyMessage = context.MakeMessage();
            replyMessage.Text = replies.ElementAt(new Random().Next(replies.Length));
            await context.PostAsync(replyMessage);

            SetLastLuisResult(context, luisResult, goToTopIntent: false);
            context.Wait(this.MessageReceived);
            //context.Done(activity);
        }

        [LuisIntent("Shopping.CatalogInformation")]
        public async Task CatalogInformation(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            EntityRecommendation store;
            var activity = await result as Activity;
            var replyMessage = context.MakeMessage();

            if (luisResult.TryFindEntity("Shopping.Store", out store))
            {
                replyMessage.Text = "Wait a moment please...";

                SetLastLuisResult(context, luisResult, goToTopIntent: false);
            }
            else
            {
                replyMessage.Text = "Which store are you interested in?";
                replyMessage.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Top 1 store", Type=ActionTypes.ImBack, Value="nespo" },
                        new CardAction(){ Title = "Top 2 store", Type=ActionTypes.ImBack, Value="nespo" },
                        new CardAction(){ Title = "Top 3 store", Type=ActionTypes.ImBack, Value="nespo" }
                    }
                };

                SetLastLuisResult(context, luisResult, goToTopIntent: true);
            }

            await context.PostAsync(replyMessage);

            context.Wait(this.MessageReceived);
            //context.Done(activity);
        }

        [LuisIntent("Shopping.GeneralInformation")]
        public async Task GeneralInformation(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;
            var replyMessage = context.MakeMessage();

            if (activity.Text.ToLower().Contains("help"))
            {
                string[] replies = new string[] 
                {
                    "What can I help you with?",
                    "How can I be helpful to you?",
                    "Ask me your question."
                };

                replyMessage.Text = replies.ElementAt(new Random().Next(replies.Length));
                SetLastLuisResult(context, luisResult, goToTopIntent: true);
            }
            else
            {
                replyMessage.Text = QnAHelper.GetAnswer(activity.Text).Answers.First().Answer;
                SetLastLuisResult(context, luisResult, goToTopIntent: false);
            }

            await context.PostAsync(replyMessage);

            context.Wait(this.MessageReceived);
            //context.Done(activity);
        }

        [LuisIntent("Shopping.Shop")]
        public async Task Shop(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;
            var replyMessage = context.MakeMessage();

            if (activity.Text.ToLower().Contains("quit"))
            {
                replyMessage.Text = "Roger that!";
                await context.PostAsync(replyMessage);

                replyMessage.Text = "What would you like to do next?";
                replyMessage.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Make another search", Type=ActionTypes.ImBack, Value="I want to shop with Zoie." },
                        new CardAction(){ Title = "Get to know Zoie", Type=ActionTypes.ImBack, Value="I need some help." },
                        new CardAction(){ Title = "See top products", Type=ActionTypes.ImBack, Value="Show me the top trends."}
                    }
                };
                await context.PostAsync(replyMessage);

                SetLastSearchFilters(context, null);
                SetLastLuisResult(context, luisResult, goToTopIntent: false);
                context.Wait(this.MessageReceived);
                return;
            }

            SearchFilters filtersForCategory = GetLastSearchFilters(context, checkIfActive: true);
            string gender;
            bool lastfiltersAreUsed = filtersForCategory != null;

            if (filtersForCategory != null)
            {
                if (!string.IsNullOrEmpty(gender = activity.Text.ToLower().Split(' ')?.FirstOrDefault(w => w == "man" || w == "woman" || w == "men" || w == "women")))
                {
                    luisResult = GetLastLuisResult(context).Value;
                    filtersForCategory.Gender = (gender.Contains("wo")) ? SearchFilters.ProductGender.Women : SearchFilters.ProductGender.Men;
                }
            }
            else
            {
                if (!lastfiltersAreUsed)
                    filtersForCategory = new SearchFilters();

                bool filtersFound = false;
                //bool tShirtEntityExists =
                //    luisResult.Entities?.Where(ent => ent.Entity.ToLower().Contains("shirt"))?.Any(ent => ent.Entity.ToLower().Trim().StartsWith("t")) ?? false;

                foreach (var filter in luisResult.Entities?.Where(ent => ent.Type.StartsWith("Shopping")))
                {
                    SearchFilters.FilterAdd(luisResult, filter, ref filtersForCategory);
                    filtersFound = true;
                }

                if (activity.Text.ToLower().Contains(" buck"))
                    filtersForCategory.Currency = "Dollar";
                else
                    filtersForCategory.Currency = luisResult.Entities?.FirstOrDefault(ent => ent.Type == "builtin.currency")?.Resolution["unit"] as string;

                if (filtersFound && string.IsNullOrEmpty(filtersForCategory.Category))
                {
                    replyMessage.Text = $"I'm sorry, but what exactly did you say you want to buy?";
                    await context.PostAsync(replyMessage);

                    SetLastLuisResult(context, luisResult, goToTopIntent: true);
                    context.Wait(this.MessageReceived);
                    //context.Done(activity);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(filtersForCategory.Category))
            {
                if (filtersForCategory.Gender == SearchFilters.ProductGender.NotSelected || filtersForCategory.Gender == SearchFilters.ProductGender.MenOrWomen)
                {
                    if (!lastfiltersAreUsed)
                    {
                        /*if (User.Gender != null)
                        {
                            switch (User.Gender)
                            {
                                case Genders.Female:
                                    filtersForCategory.Gender = SearchFilters.ProductGender.Men;
                                    break;
                                case Genders.Male:
                                    filtersForCategory.Gender = SearchFilters.ProductGender.Women;
                                    break;
                            }
                        }
                        else
                        {
                            replyMessage.Text = $"Are you looking for man or woman {filtersForCategory.Category}?";
                        }*/
                        replyMessage.Text = $"Are you looking for man or woman {filtersForCategory.Category}?";
                    }
                    else
                        replyMessage.Text = "Type 'man' or 'woman' to select gender or quit to exit.";

                    await context.PostAsync(replyMessage);

                    SetLastSearchFilters(context, filtersForCategory);
                    SetLastLuisResult(context, luisResult, goToTopIntent: true);
                    context.Wait(this.MessageReceived);
                    //context.Done(activity);

                    return;
                }

                StringBuilder strBuilder = new StringBuilder($"OK, let's take a look at the");
                ShopMessageProcess(ref strBuilder, filtersForCategory.Colors);
                ShopMessageProcess(ref strBuilder, filtersForCategory.Brands);
                strBuilder.Append($" {filtersForCategory.Category}");
                ShopMessageProcess(ref strBuilder, filtersForCategory.Sizes, startWith: $" of size{((filtersForCategory.Sizes.Count > 1) ? "s" : "")}");
                ShopMessageProcess(ref strBuilder, filtersForCategory.Stores, startWith: " from", endWith: $" store{((filtersForCategory.Sizes.Count > 1) ? "s" : "")}");

                if (filtersForCategory.PriceRange?.HasApprox ?? false)
                    strBuilder.Append($", which cost arround {filtersForCategory.PriceRange.ApproximatePrice} {filtersForCategory.Currency}s");
                else
                {
                    if (filtersForCategory.PriceRange?.HasMin ?? false && filtersForCategory.PriceRange.HasMax)
                        strBuilder.Append($", which cost is in the range of {filtersForCategory.PriceRange.MinimumPrice} - {filtersForCategory.PriceRange.MaximumPrice} {filtersForCategory.Currency}s");
                    else
                    {
                        if (filtersForCategory.PriceRange?.HasMax ?? false)
                            strBuilder.Append($", which cost not more than {filtersForCategory.PriceRange.MaximumPrice} {filtersForCategory.Currency}s");
                        else if (filtersForCategory.PriceRange?.HasMin ?? false)
                            strBuilder.Append($", which cost more than {filtersForCategory.PriceRange.MinimumPrice} {filtersForCategory.Currency}s");
                    }
                }
                strBuilder.Append(" that I found:");
                //strBuilder.AppendLine("    (Service not ready yet...)");
                replyMessage.Text = strBuilder.ToString();


                #region Products carousel
                replyMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                replyMessage.Attachments = new List<Attachment>();

                context.ConversationData.TryGetValue("CarouselPage", out int carouselPage);
                List<Product> prods = await this.ProductsSearchAPICall(filtersForCategory);

                Product currentProd;
                if (!(activity.Text == "See more" || carouselPage * 5 >= prods?.Count))
                {
                    int i;
                    if (carouselPage == 0)
                        i = 2;
                    else
                        i = carouselPage * 5;
                    for ( ; i < carouselPage * 5 + 5 && i < (prods?.Count ?? 0); i++)
                    {
                        currentProd = prods.ElementAt(i);
                        List<CardImage> cardImages = new List<CardImage>
                            {
                                new CardImage(url: currentProd.ImageUrl)
                            };

                        List<CardAction> cardButtons = new List<CardAction>
                            {
                                new CardAction()
                                {
                                    Value = currentProd.ProductUrl,
                                    Type = "openUrl",
                                    Title = "Buy"
                                }
                            };

                        Attachment carouselAttachment = new HeroCard()
                        {
                            Title = currentProd.Name,
                            Subtitle = currentProd.Category,
                            Images = cardImages,
                            Buttons = cardButtons,
                            Tap = cardButtons.ElementAt(0)
                        }
                        .ToAttachment();

                        replyMessage.Attachments.Add(carouselAttachment);

                        replyMessage.SuggestedActions = new SuggestedActions()
                        {
                            Actions = new List<CardAction>()
                            {
                                new CardAction() { Title = "See more", Type = ActionTypes.ImBack, Value = "See more"},
                                new CardAction() { Title = "New search", Type = ActionTypes.ImBack, Value = "New search"}
                            }
                        };
                    }
                }
                else
                {
                    replyMessage.Text = "There are no other products. Search something else!";
                    replyMessage.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>()
                            {
                                new CardAction() { Title = "Nike shoes", Type = ActionTypes.ImBack, Value = "Nike shoes"},
                                new CardAction() { Title = "Puma trousers", Type = ActionTypes.ImBack, Value = "Puma trousers"}
                            }
                    };

                    SetLastLuisResult(context, luisResult, goToTopIntent: false);
                    this.SetLastSearchFilters(context, null);

                    await context.PostAsync(replyMessage);

                    context.Wait(this.MessageReceived);
                    return;
                }

                context.ConversationData.SetValue("CarouselPage", ++carouselPage);
                this.SetLastSearchFilters(context, filtersForCategory);
                this.SetLastLuisResult(context, luisResult, goToTopIntent: true);

                await context.PostAsync(replyMessage);

                context.Wait(this.MessageReceived);
                return;
                #endregion
            }
            else
            {
                replyMessage.Text = $"All right {activity.From.Name.Split(' ').First()}! What would you like to buy?";
            }

            await context.PostAsync(replyMessage);

            SetLastLuisResult(context, luisResult, goToTopIntent: false);
            context.Wait(this.MessageReceived);
            //context.Done(activity);
        }

        [LuisIntent("SimplePhrases")]
        public async Task SimplePhrases(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            EntityRecommendation complimenting, greetingHello, greetingGoodbye, thanking;
            string[] replies;

            var activity = await result as Activity;
            var replyMessage = context.MakeMessage();

            if (luisResult.TryFindEntity("WishingWords.Greeting::Hello", out greetingHello))
            {
                int hourSent = activity.LocalTimestamp?.Hour ?? -1;
                string dayTime = string.Empty;

                if (hourSent >= 5 && hourSent < 12)
                    dayTime = "morning";
                else if (hourSent >= 12 && hourSent < 20)
                    dayTime = "afternoon";
                else if (hourSent >= 20 || hourSent < 5)
                    dayTime = "evening";

                replies = new string[]
                {
                    "Hi! Nice to hear from you again!",
                    $"Good {dayTime} {activity.From.Name.Split(' ').First()}!",
                    $"Hello {activity.From.Name.Split(' ').First()}"
                };
                replyMessage.Text = replies.ElementAt(new Random().Next(replies.Length));
                await context.PostAsync(replyMessage);

                replyMessage.Text = "What would you like to do?";
                replyMessage.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Shop", Type=ActionTypes.ImBack, Value="I want to shop with Zoie." },
                        new CardAction(){ Title = "Get to know Zoie", Type=ActionTypes.ImBack, Value="I need some help." },
                        new CardAction(){ Title = "See top products", Type=ActionTypes.ImBack, Value="Show me the top trends."}
                    }
                };
                await context.PostAsync(replyMessage);

                context.Wait(this.MessageReceived);
                //context.Done(activity);
                return;
            }

            if (luisResult.TryFindEntity("WishingWords.Complimenting", out complimenting))
            {
                replies = new string[]
                {
                    "Thank you very much!",
                    "Very kind of you! :)",
                    $"Thanks a lot {activity.From.Name.Split(' ').First()}!"
                };
                replyMessage.Text = replies.ElementAt(new Random().Next(replies.Length));

                await context.PostAsync(replyMessage);
            }

            if (luisResult.TryFindEntity("WishingWords.Thanking", out thanking))
            {
                replyMessage.Text = "You're welcome!";

                await context.PostAsync(replyMessage);
            }

            if (luisResult.TryFindEntity("WishingWords.Greeting::Goodbye", out greetingGoodbye))
            {
                int hourSent = activity.LocalTimestamp?.Hour ?? -1;
                string dayTime = string.Empty;

                if (hourSent >= 5 && hourSent < 12)
                    dayTime = "day";
                else if (hourSent >= 12 && hourSent < 20)
                    dayTime = "afternoon";
                else if (hourSent >= 20 || hourSent < 5)
                    dayTime = "night";

                replies = new string[]
                {
                    "Goodbye!",
                    $"Have a nice {dayTime} {activity.From.Name.Split(' ').First()}!",
                    $"See you {activity.From.Name.Split(' ').First()}!"
                };
                replyMessage.Text = replies.ElementAt(new Random().Next(replies.Length));
                await context.PostAsync(replyMessage);
            }

            SetLastLuisResult(context, luisResult, goToTopIntent: false);
            context.Wait(this.MessageReceived);
            //context.Done(activity);
        }

        [LuisIntent("ProfileAction")]
        public async Task ProfileAction(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;
            var replyMessage = context.MakeMessage();

            var profileActionEntity = luisResult.Entities.FirstOrDefault(ent => ent.Type.StartsWith("ProfileActivity"));
            if (profileActionEntity != null)
            {
                string profileAction = profileActionEntity.Type.Split(new string[1] { "::" }, StringSplitOptions.RemoveEmptyEntries).Last();

                switch (profileAction)
                {
                    case "Create":
                        replyMessage.Text = "Let's create your profile. (Not ready yet...)";
                        break;
                    case "Edit":
                        replyMessage.Text = "Let's edit your profile. (Not ready yet...)";
                        break;
                    case "Delete":
                        replyMessage.Text = "Ok, I'll delete all your personal information. (Not ready yet...)";
                        break;
                    default:
                        replyMessage.Text = "I'm not sure what you want to do with your profile.";
                        replyMessage.SuggestedActions = new SuggestedActions()
                        {
                            Actions = new List<CardAction>()
                            {
                                new CardAction(){ Title = "Create a new one", Type=ActionTypes.ImBack, Value="I want to create a new profile in Zoie." },
                                new CardAction(){ Title = "Change it", Type=ActionTypes.ImBack, Value="I want to edit my profile in Zoie." },
                                new CardAction(){ Title = "Delete it", Type=ActionTypes.ImBack, Value="I want to delete my profile in Zoie." }
                            }
                        };
                        break;
                }
            }
            else
            {
                replyMessage.Text = "I'm not sure what you want to do with your profile.";
                replyMessage.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Create a new one", Type=ActionTypes.ImBack, Value="I want to create a new profile in Zoie." },
                        new CardAction(){ Title = "Change it", Type=ActionTypes.ImBack, Value="I want to edit my profile in Zoie." },
                        new CardAction(){ Title = "Delete it", Type=ActionTypes.ImBack, Value="I want to delete my profile in Zoie." }
                    }
                };
            }

            await context.PostAsync(replyMessage);

            SetLastLuisResult(context, luisResult, goToTopIntent: false);
            context.Wait(this.MessageReceived);
            //context.Done(activity);
        }

        [LuisIntent("Shopping.ViewTopProducts")]
        public async Task TopProducts(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;
            var replyMessage = context.MakeMessage();

            replyMessage.Text = "Top products feature coming soon...";

            await context.PostAsync(replyMessage);

            SetLastLuisResult(context, luisResult, goToTopIntent: false);
            context.Wait(this.MessageReceived);
            //context.Done(activity);
        }
        
        private async Task<List<Product>> ProductsSearchAPICall(SearchFilters filters)
        {
            HttpClient client = new HttpClient { BaseAddress = new Uri("http://zoie.io/API/products-results.php") };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            StringBuilder requestParams = new StringBuilder();
            try
            {
                requestParams = new StringBuilder("?");
                requestParams.Append($"manufacturer={filters.Brands.FirstOrDefault()}&");
                requestParams.Append($"gender={filters.GetGenderString()}&");
                requestParams.Append($"type={filters.Category}&");
                requestParams.Append($"color={(filters.Colors.FirstOrDefault() == "black" ? "μαυρο" : "")}&");
                requestParams.Append($"min_price={filters.PriceRange?.MinimumPrice}&");
                //requestParams.Append($"max_price={(filters.PriceRange.HasMax ? filters.PriceRange.MaximumPrice.ToString() : string.Empty)}&");
                requestParams.Append($"size={filters.Sizes.FirstOrDefault()}&");
                requestParams.Append($"shop={filters.Stores.FirstOrDefault()}&");
            }
            catch (Exception e)
            {

            }

            HttpResponseMessage response = await client.GetAsync(requestParams.ToString());
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(json))
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<RootObject>(json).Products;
            }

            return new List<Product>(0);
        }

        private LastLuisResult GetLastLuisResult(IDialogContext context)
        {
            context.ConversationData.TryGetValue("LastLuisResult", out LastLuisResult lastLuisResult);
            return lastLuisResult;
        }

        private void SetLastLuisResult(IDialogContext context, LuisResult luisResult, bool goToTopIntent)
        {
            context.ConversationData.SetValue("LastLuisResult", new LastLuisResult(luisResult, goToTopIntent));
        }

        private SearchFilters GetLastSearchFilters(IDialogContext context, bool checkIfActive)
        {
            context.ConversationData.TryGetValue("LastSearchFiltersIsActive", out bool isActive);
            if (checkIfActive && !isActive)
                return null;

            if (context.ConversationData.TryGetValue("LastSearchFilters", out SearchFilters lastSearchFilters))
                context.ConversationData.SetValue("LastSearchFiltersIsActive", false);
            return lastSearchFilters;
        }

        private void SetLastSearchFilters(IDialogContext context, SearchFilters searchFilters)
        {
            if (searchFilters == null)
            {
                context.ConversationData.RemoveValue("LastSearchFilters");
                context.ConversationData.RemoveValue("LastSearchFiltersIsActive");
                return;
            }

            context.ConversationData.SetValue("LastSearchFilters", searchFilters);
            context.ConversationData.SetValue("LastSearchFiltersIsActive", true);
        }

        private void ShopMessageProcess(ref StringBuilder stringBuilder, List<string> values, string startWith = null, string endWith = null)
        {
            if (values.Count > 0)
            {
                stringBuilder.Append(startWith);
                stringBuilder.Append($" {values.FirstOrDefault()}");
                for (int i = 1; i < values.Count - 1; i++)
                    stringBuilder.Append($", {values[i]}");
                if (values.Count > 1)
                    stringBuilder.Append($" and {values.LastOrDefault()}");
                stringBuilder.Append(endWith);
            }
        }

        private class LastLuisResult
        {
            public LuisResult Value { get; set; }
            public bool GoToTopIntent { get; set; }

            public LastLuisResult()
            {

            }

            public LastLuisResult(LuisResult luisResult, bool goToTopIntent)
            {
                this.Value = luisResult;
                this.GoToTopIntent = goToTopIntent;
            }
        }

        private class SearchFilters
        {
            public List<string> Brands { get; set; }
            public string Category { get; set; }
            public ProductGender Gender { get; set; }
            public List<string> Colors { get; set; }
            public ProductPriceRange PriceRange { get; set; }
            public List<string> Sizes { get; set; }
            public List<string> Stores { get; set; }
            public string Currency { get; set; }


            public SearchFilters()
            {
                this.Brands = new List<string>();
                this.Colors = new List<string>();
                this.Sizes = new List<string>();
                this.Stores = new List<string>();
            }

            public static void FilterAdd(LuisResult luisResult, EntityRecommendation filterEntityToAdd, ref SearchFilters filters, string normalValue = null)
            {
                string entityNormalValue = normalValue ?? (filterEntityToAdd.Resolution?["values"] as List<object>)?.FirstOrDefault() as string;
                switch (filterEntityToAdd.Type)
                {
                    case "Shopping.Product.Brand":
                        filters.Brands.Add(entityNormalValue);
                        break;
                    case "Shopping.Product.Category.Men":
                        if (filters.Category == null)
                            filters.Category = entityNormalValue;
                        filters.Gender = ProductGender.Men;
                        break;
                    case "Shopping.Product.Category.Women":
                        if (filters.Category == null)
                            filters.Category = entityNormalValue;
                        filters.Gender = ProductGender.Women;
                        break;
                    case "Shopping.Product.Category.MenOrWomen":
                        if (filters.Category == null)
                            filters.Category = entityNormalValue;
                        filters.Gender = ProductGender.MenOrWomen;
                        break;
                    case "Shopping.Product.Color":
                        filters.Colors.Add(entityNormalValue);
                        break;
                    case "Shopping.Product.Size":
                        if (string.IsNullOrEmpty(entityNormalValue) 
                            && luisResult.Query.Contains("size")
                            && luisResult.TryFindEntity("builtin.number", out EntityRecommendation chosenEntity))
                        {
                            int sizeWordStartIndex = luisResult.Query.IndexOf("size");
                            int sizeWordEndIndex = sizeWordStartIndex + 3;
                            int shortestNumDistanceFromSizeWord = int.MaxValue;

                            foreach (var numberEntity in luisResult.Entities.Where(ent => ent.Type == "builtin.number") ?? new List<EntityRecommendation>(0))
                            {
                                bool isNumberAfterSizeWord = numberEntity.StartIndex > sizeWordStartIndex;
                                int curNumDistanceFromSizeWord = 
                                    (isNumberAfterSizeWord) ? (int)numberEntity.StartIndex - sizeWordEndIndex : sizeWordStartIndex - (int)numberEntity.EndIndex;

                                if (curNumDistanceFromSizeWord < shortestNumDistanceFromSizeWord)
                                    chosenEntity = numberEntity;
                            }

                            filters.Sizes.Add(chosenEntity.Entity);
                        }
                        else
                        {
                            filters.Sizes.Add(entityNormalValue);
                        }
                        break;
                    case "Shopping.Store":
                        filters.Stores.Add(entityNormalValue);
                        break;
                    case "Shopping.Product.PriceRange::MinPrice":
                        entityNormalValue = normalValue ?? string.Concat(filterEntityToAdd.Entity.Where(c => char.IsNumber(c)));
                        if (float.TryParse(entityNormalValue.Trim(), out float minPrice))
                        {
                            if (filters.PriceRange == null)
                                filters.PriceRange = new ProductPriceRange() { MinimumPrice = minPrice };
                            else
                                filters.PriceRange.MinimumPrice = minPrice;
                        }
                        break;
                    case "Shopping.Product.PriceRange::MaxPrice":
                        entityNormalValue = normalValue ?? string.Concat(filterEntityToAdd.Entity.Where(c => char.IsNumber(c)));
                        if (float.TryParse(entityNormalValue.Trim(), out float maxPrice))
                        {
                            if (filters.PriceRange == null)
                                filters.PriceRange = new ProductPriceRange() { MaximumPrice = maxPrice };
                            else
                                filters.PriceRange.MaximumPrice = maxPrice;
                        }
                        break;
                    case "Shopping.Product.PriceRange":
                        entityNormalValue = normalValue ?? string.Concat(filterEntityToAdd.Entity.Where(c => char.IsNumber(c)));
                        if (float.TryParse(entityNormalValue.Trim(), out float approxPrice))
                        {
                            if (filters.PriceRange == null)
                                filters.PriceRange = new ProductPriceRange() { ApproximatePrice = approxPrice };
                            else
                                filters.PriceRange.ApproximatePrice = approxPrice;
                        }
                        break;
                    default:
                        break;
                }
            }

            public string GetGenderString()
            {
                switch (this.Gender)
                {
                    case ProductGender.Men:
                        return "man";
                    case ProductGender.Women:
                        return "woman";
                    default:
                        return string.Empty;
                }
            }

            public enum ProductGender
            {
                NotSelected, MenOrWomen, Men, Women
            }

            public class ProductPriceRange
            {
                private float? minimumPrice;
                private float? maximumPrice;
                private float? approximatePrice;

                public float? MinimumPrice { get { return minimumPrice; } set { minimumPrice = Math.Abs((float)value); } }
                public float? MaximumPrice { get { return maximumPrice; } set { maximumPrice = Math.Abs((float)value); } }
                public float? ApproximatePrice { get { return approximatePrice; } set { approximatePrice = Math.Abs((float)value); } }
                public bool HasMin { get { return minimumPrice != null; } }
                public bool HasMax { get { return maximumPrice != null; } }
                public bool HasApprox { get { return approximatePrice != null; } }
            }
        }
    }
}