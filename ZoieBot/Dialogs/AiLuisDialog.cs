using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using ZoieBot.Helpers;

namespace ZoieBot.Dialogs
{
    //European LUIS
    //[LuisModel("c63eb073-f284-42da-a536-49d28b2294ce", "70b25e78f45e45239ce4fe967afc3be1", domain: "westeurope.api.cognitive.microsoft.com")]

    //American LUIS
    [LuisModel("6a70ebea-6c59-4461-9ee1-090c779a7389", "4312563ba2124fbea40a818d56a8c851")]
    [Serializable]
    public class AiLuisDialog : LuisDialog<object>
    {
        [LuisIntent("")]
        [LuisIntent("None")]
        public async Task None(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;

            var replyMessage = activity.CreateReply("I'm sorry, I didn't understand");
            await context.PostAsync(replyMessage);

            replyMessage.Text = "You can though try something of the following";
            replyMessage.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Select store", Type = ActionTypes.PostBack, Value = "__store"},
                        new CardAction(){ Title = "Top products", Type = ActionTypes.PostBack, Value = "__top_products"},
                        new CardAction(){ Title = "Category search", Type = ActionTypes.PostBack, Value = "__categories"}
                    }
            };
            await context.PostAsync(replyMessage);

            activity.Text = "__luis_neutral";
            context.Done(activity);
        }

        [LuisIntent("AbusiveContent")]
        public async Task AbusiveContent(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            string[] replies = new string[] {
                "I have never offended you so I demand to speak nice to me!",
                "Stop abusing me or I will report you."
            };

            replyMessage.Text = replies.ElementAt(new Random().Next(replies.Length));
            await context.PostAsync(replyMessage);

            replyMessage.Text = "Please be nice and try something of the following";
            replyMessage.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Select store", Type = ActionTypes.PostBack, Value = "__store"},
                        new CardAction(){ Title = "Top products", Type = ActionTypes.PostBack, Value = "__top_products"},
                        new CardAction(){ Title = "Category search", Type = ActionTypes.PostBack, Value = "__categories"}
                    }
            };
            await context.PostAsync(replyMessage);

            activity.Text = "__luis_neutral";
            context.Done(activity);
        }

        [LuisIntent("Shopping.GeneralInformation")]
        public async Task GeneralInformation(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            if (activity.Text.ToLower().Contains("help"))
            {
                string[] replies = new string[]
                {
                    "What can I help you with?",
                    "How can I be helpful to you?",
                    "Ask me your question."
                };

                replyMessage.Text = replies.ElementAt(new Random().Next(replies.Length));
                await context.PostAsync(replyMessage);

                context.Wait(MessageReceived);
                return;
            }
            else
            {
                string answer = QnAHelper.GetAnswer(activity.Text).Answers.First().Answer;
                if (answer == "No good match found in the KB")
                    answer = "Sorry, can't help you with that.";

                replyMessage.Text = answer;
                await context.PostAsync(replyMessage);

                replyMessage.Text = "Ask me something else or choose something of the following";
                replyMessage.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Select store", Type = ActionTypes.PostBack, Value = "__store"},
                        new CardAction(){ Title = "Top products", Type = ActionTypes.PostBack, Value = "__top_products"},
                        new CardAction(){ Title = "Category search", Type = ActionTypes.PostBack, Value = "__categories"}
                    }
                };
                await context.PostAsync(replyMessage);

                activity.Text = "__luis_neutral";
                context.Done(activity);
            }

            return;
        }

        [LuisIntent("Shopping.Shop")]
        public async Task Shop(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            foreach (var filter in luisResult.Entities?.Where(ent => ent.Type.StartsWith("Shopping")))
            {
                string entityNormalValue = (filter.Resolution?["values"] as List<object>)?.FirstOrDefault() as string;
                switch (filter.Type)
                {
                    case "Shopping.Product.Brand":
                        context.ConversationData.SetValue("ManufacturerFilter", entityNormalValue);
                        break;
                    case "Shopping.Product.Gender":
                        context.ConversationData.SetValue("GenderFilter", entityNormalValue);
                        break;
                    case "Shopping.Product.Category.Men":
                        context.ConversationData.SetValue("GenderFilter", string.Empty);
                        context.ConversationData.SetValue("TypeFilter", entityNormalValue);
                        break;
                    case "Shopping.Product.Category.Women":
                        context.ConversationData.SetValue("GenderFilter", string.Empty);
                        context.ConversationData.SetValue("TypeFilter", entityNormalValue);
                        break;
                    case "Shopping.Product.Category.MenOrWomen":
                        context.ConversationData.SetValue("TypeFilter", entityNormalValue);
                        break;
                    case "Shopping.Product.Color":
                        context.ConversationData.SetValue("ColorFilter", entityNormalValue);
                        break;
                    case "Shopping.Product.Size":
                        if (string.IsNullOrEmpty(entityNormalValue) && luisResult.TryFindEntity("builtin.number", out EntityRecommendation sizeNumberEntity))
                            context.ConversationData.SetValue("SizeFilter", sizeNumberEntity.Entity);
                        else
                            context.ConversationData.SetValue("SizeFilter", entityNormalValue);
                        break;
                    case "Shopping.Store":
                        context.PrivateConversationData.SetValue("StoreFilter", entityNormalValue);
                        break;
                    case "Shopping.Product.PriceRange::MinPrice":
                        entityNormalValue = string.Concat(filter.Entity.Where(c => char.IsNumber(c)));
                        context.ConversationData.SetValue("MinPriceFilter", entityNormalValue);
                        break;
                    case "Shopping.Product.PriceRange":
                    case "Shopping.Product.PriceRange::MaxPrice":
                        entityNormalValue = string.Concat(filter.Entity.Where(c => char.IsNumber(c)));
                        context.ConversationData.SetValue("MaxPriceFilter", entityNormalValue);
                        break;
                }
            }

            activity.Text = "__luis_shopping";
            context.Done(activity);
        }

        [LuisIntent("SimplePhrases")]
        public async Task SimplePhrases(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();
            string[] replies;

            if (luisResult.TryFindEntity("WishingWords.Greeting::Hello", out EntityRecommendation greetingHello))
            {
                if (activity.ChannelId == "webchat")
                {
                    replyMessage.Text = "Hi there!";
                }
                else
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
                        $"Good {dayTime} {activity.From.Name.Split(' ').FirstOrDefault()}!",
                        $"Hello {activity.From.Name.Split(' ').FirstOrDefault()}"
                    };
                    replyMessage.Text = replies.ElementAt(new Random().Next(replies.Length));
                }

                await context.PostAsync(replyMessage);
            }

            if (luisResult.TryFindEntity("WishingWords.Complimenting", out EntityRecommendation complimenting))
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

            if (luisResult.TryFindEntity("WishingWords.Thanking", out EntityRecommendation thanking))
            {
                replyMessage.Text = "You're welcome!";

                await context.PostAsync(replyMessage);
            }

            if (luisResult.TryFindEntity("WishingWords.Greeting::Goodbye", out EntityRecommendation greetingGoodbye))
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

                activity.Text = "__luis_goodbye";
                context.Done(activity);
                return;
            }

            replyMessage.Text = "What would you like to do next?";
            replyMessage.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Select store", Type = ActionTypes.PostBack, Value = "__store"},
                        new CardAction(){ Title = "Top products", Type = ActionTypes.PostBack, Value = "__top_products"},
                        new CardAction(){ Title = "Category search", Type = ActionTypes.PostBack, Value = "__categories"}
                    }
            };
            await context.PostAsync(replyMessage);

            activity.Text = "__luis_neutral";
            context.Done(activity);
        }

        [LuisIntent("ProfileAction")]
        public async Task ProfileAction(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            var profileActionEntity = luisResult.Entities.FirstOrDefault(ent => ent.Type.StartsWith("ProfileActivity"));
            if (profileActionEntity != null)
            {
                string profileAction = profileActionEntity.Type.Split(new string[1] { "::" }, StringSplitOptions.RemoveEmptyEntries).Last();

                switch (profileAction)
                {
                    case "Delete":
                        activity.Text = "__luis_delete_personal_data";
                        context.Done(activity);
                        return;
                    case "Create":
                    case "Edit":
                    default:
                        await None(context, result, luisResult);
                        return;
                }
            }
        }

        [LuisIntent("Shopping.ViewTopProducts")]
        public async Task TopProducts(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;

            activity.Text = "__luis_top_products";
            context.Done(activity);
        }

        [LuisIntent("ChangeLanguage")]
        public async Task ChangeLanguage(IDialogContext context, IAwaitable<IMessageActivity> result, LuisResult luisResult)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply("Zoie is currently learning new languages. Stay tuned!");

            replyMessage.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Select store", Type = ActionTypes.PostBack, Value = "__store"},
                        new CardAction(){ Title = "Top products", Type = ActionTypes.PostBack, Value = "__top_products"},
                        new CardAction(){ Title = "Category search", Type = ActionTypes.PostBack, Value = "__categories"}
                    }
            };
            await context.PostAsync(replyMessage);

            activity.Text = "__luis_neutral";
            context.Done(activity);
        }
    }
}