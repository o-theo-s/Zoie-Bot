using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Apis;
using Apis.Models;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Zoie.Bot.Dialogs.LUIS;
using Zoie.Bot.Models;
using Zoie.Helpers;
using Zoie.Helpers.Channels.Facebook.Library;

namespace Zoie.Bot.Dialogs.Main
{
    [Serializable]
    public class StoreDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            if (context.PrivateConversationData.ContainsKey("Referral"))
                context.Wait(ReferralReceivedAsync);
            else
                context.Wait(SelectStoreAsync);

            return Task.CompletedTask;
        }

        private async Task ReferralReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            Referral referral = context.PrivateConversationData.GetValue<Referral>("Referral");
            context.PrivateConversationData.RemoveValue("Referral");

            context.PrivateConversationData.SetValue("StoreSelected", JsonConvert.DeserializeObject<Store>(referral.Item));
            await this.ShowStoreMenuAsync(context, result);
        }

        private async Task SelectStoreAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply("Let’s pick a store and buy an outfit for today!");
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GeneralHelper.GetActualAsyncMethodName());

            context.ConversationData.TryGetValue("StoresNextPage", out int currentPage);

            var storesApi = new API<StoresRoot>();
            var storesRoot = await storesApi.CallAsync(new Dictionary<string, string>(1) { { "page", currentPage.ToString() } })
                ?? await storesApi.CallAsync(new Dictionary<string, string>(1) { { "page", (currentPage = 0).ToString() } });
            context.ConversationData.SetValue("StoresNextPage", currentPage + 1);

            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            Store store;
            for (int i = 0; i < 9 && i < storesRoot.Stores.Count; i++)
            {
                store = storesRoot.Stores[i];
                if (string.IsNullOrWhiteSpace(store.ImageUrl))
                    store.ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/StoresLogos/all_stores.png";
                reply.Attachments.Add(
                    new HeroCard()
                    {
                        Title = GeneralHelper.CapitalizeFirstLetter(store.Name),
                        Images = new List<CardImage> { new CardImage { Url = store.ImageUrl} },
                        Buttons = new List<CardAction> { new CardAction { Title = "Select", Type = ActionTypes.PostBack, Value = $"__store_select_{JsonConvert.SerializeObject(store)}" } }
                    }.ToAttachment());
            }
            reply.Attachments.Add(
                new HeroCard()
                {
                    Title = "Create Your Store!",
                    Subtitle = "Create your own messenger store for FREE, NOW!!",
                    Images = new List<CardImage> { new CardImage { Url = "https://zoiebot.azurewebsites.net/Files/Images/Stores/store-create.jpg" } },
                    Buttons = new List<CardAction> { new CardAction { Title = "Create", Type = ActionTypes.OpenUrl, Value = "http://zoie.io/sign-up.php" } }
                }.ToAttachment());

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = storesRoot.RemainingPages > 0 ? "Next page" : "First page", Type = ActionTypes.PostBack, Value = "__store_show_more" },
                    new CardAction(){ Title = "New search", Type = ActionTypes.PostBack, Value = "__menu_new_search" }
                }
            };
            if (storesRoot.RemainingPages == 0 && currentPage == 0)
                reply.SuggestedActions.Actions.RemoveAt(0);

            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            switch (activity.Text)
            {
                case string text when text.StartsWith("__store_select"):
                    string storeJson = activity.Text.Remove(0, "__store_select".Length);
                    if (!string.IsNullOrWhiteSpace(storeJson))
                    {
                        Store store = JsonConvert.DeserializeObject<Store>(storeJson.Remove(0, 1));
                        context.PrivateConversationData.SetValue("StoreSelected", store);
                    }
                    await this.ShowStoreMenuAsync(context, result);
                    return;
                case "__store_show_more":
                    await this.SelectStoreAsync(context, result);
                    return;
                case "__store_shop":
                    context.ConversationData.RemoveValue("WindowShopNextPage");
                    if (context.UserData.ContainsKey("HasPersonalized") && context.UserData.GetValue<bool>("HasPersonalized"))
                        await this.SelectShopCategoryAsync(context, result);
                    else
                        await context.Forward(new PersonalizationDialog(), SelectShopCategoryAsync, activity);
                    return;
                case "__store_info":
                    await this.CustomerServiceAsync(context, result);
                    return;
                case "__store_window":
                    await this.WindowShopAsync(context, result);
                    return;
                case "__store_reselect":
                    context.ConversationData.Clear();
                    await this.SelectStoreAsync(context, result);
                    return;
                case string text when text.StartsWith("__shop_"):
                    string gender = context.UserData.GetValue<string>("Gender");

                    SearchModel searchModel = JsonConvert.DeserializeObject<SearchModel>(activity.Text.Remove(0, "__shop_".Length));
                    searchModel.Gender = gender == "Male" ? "άνδρας" : "γυναίκα";
                    context.ConversationData.SetValue("SearchModel", searchModel);

                    var searchApparelsApi = new API<ApparelsRoot>();
                    var apparelsRoot = await searchApparelsApi.CallAsync(searchModel.GetAttributesDictionary());

                    await this.UnimplementedAsync(context, result);
                    return;
                case "__cs_contact":
                    await this.ContactAsync(context, result);
                    return;
                case string text when text.StartsWith("__view_collection"):
                    await this.ApparelsForCollectionAsync(context, result);
                    return;
                case "__more_collections":
                    await this.WindowShopAsync(context, result);
                    return;
                case string text when text.StartsWith("__feedback_rate"):
                    await this.FeedbackRateAsync(context, result);
                    return;
                case "__menu_new_search":
                    context.ConversationData.Clear();
                    context.PrivateConversationData.Clear();
                    await this.EndAsync(context, result);
                    return;
                case "__personality_answer":
                    var lastSubdialog = context.PrivateConversationData.GetValue<string>("LastStoreSubdialog");
                    MethodInfo reshowLastSubdialog = this.GetType().GetMethod(lastSubdialog, BindingFlags.NonPublic | BindingFlags.Instance);

                    if (context.ConversationData.TryGetValue("StoresNextPage", out int currentPage))
                        context.ConversationData.SetValue("StoresNextPage", currentPage - 1);
                    if (context.ConversationData.TryGetValue("WindowShopNextPage", out currentPage))
                        context.ConversationData.SetValue("WindowShopNextPage", currentPage - 1);

                    await (Task) reshowLastSubdialog.Invoke(this, new object[] { context, result });
                    return;
                default:
                    await context.Forward(new GlobalLuisDialog<object>(), MessageReceivedAsync, activity);
                    return;
            }
        }

        private async Task ShowStoreMenuAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GeneralHelper.GetActualAsyncMethodName());

            Store store = context.PrivateConversationData.GetValue<Store>("StoreSelected");

            if (activity.ChannelId == "facebook")
            {
                var storeContents = new List<FacebookGenericTemplateContent>(4)
                {
                    new FacebookGenericTemplateContent()
                    {
                        Title = GeneralHelper.CapitalizeFirstLetter(store.Name),
                        ImageUrl = store.ImageUrl
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = "Shop",
                        Subtitle = "Let's start shopping!",
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/store-shop.jpeg",
                        Buttons = new[] { new FacebookPostbackButton(title: "Shop", payload: "__store_shop") }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = "Customer Service",
                        Subtitle = $"Do you want to learn more about {store.Name}?",
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/store-info.jpg",
                        Buttons = new[] { new FacebookPostbackButton(title: "More Info", payload: "__store_info") }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = "Window Shopping",
                        Subtitle = $"View all the collections created by {store.Name}!",
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/store-window.jpeg",
                        Buttons = new[] { new FacebookPostbackButton(title: "View Collections", payload: "__store_window") }
                    }
                };

                var shareButton = new FacebookShareButton()
                {
                    ShareContents = new FacebookShareButtonContents()
                    {
                        Attachment = new FacebookAttachment()
                        {
                            Type = "template",
                            Payload = new FacebookGenericTemplate()
                            {
                                TemplateType = "generic",
                                Elements = new[] {
                                    new FacebookGenericTemplateContent()
                                    {
                                        ImageUrl = store.ImageUrl,
                                        Title = GeneralHelper.CapitalizeFirstLetter(store.Name) + " Messenger Store on Zoie, an AI Powered Marketplace!",
                                        Subtitle = $"Shop in {GeneralHelper.CapitalizeFirstLetter(store.Name)} through Zoie's Store in messenger! Tap the image or click the button below to start shopping.",
                                        Buttons = new[]
                                        {
                                            new FacebookUrlButton(
                                                url: $"https://m.me/{ConfigurationManager.AppSettings.Get("MessengerBotId")}?" +
                                                        $"ref={GeneralHelper.Hashify($"__referral__{activity.From.Id}__{Referral.Types.Store}__{JsonConvert.SerializeObject(store)}")}",
                                                title: "Visit Store")
                                        },
                                        Tap = new FacebookDefaultAction(
                                                url: $"https://m.me/{ConfigurationManager.AppSettings.Get("MessengerBotId")}?" +
                                                        $"ref={GeneralHelper.Hashify($"__referral__{activity.From.Id}__{Referral.Types.Store}__{JsonConvert.SerializeObject(store)}")}")
                                    }
                                }
                            }
                        }
                    }
                };

                reply.ChannelData = ChannelsHelper.Facebook.Templates.CreateListTemplate(storeContents.ToArray(), shareButton);
            }
            else
            {
                reply.AttachmentLayout = AttachmentLayoutTypes.List;

                context.Wait(UnimplementedAsync);
                return;
            }


            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "Reselect store", Type = ActionTypes.PostBack, Value = "__store_reselect" },
                    new CardAction(){ Title = "New search", Type = ActionTypes.PostBack, Value = "__menu_new_search" }
                }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task SelectShopCategoryAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GeneralHelper.GetActualAsyncMethodName());

            reply.Text = "Let’s get this shopping started! What are you looking for?";
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "T-shirts", Type = ActionTypes.PostBack, Value = $"__shop_{JsonConvert.SerializeObject(new SearchModel{ Type = "t-shirt" })}" },
                    new CardAction(){ Title = "Trousers", Type = ActionTypes.PostBack, Value = $"__shop_{JsonConvert.SerializeObject(new SearchModel{ Type = "παντελόνι" })}" },
                    new CardAction(){ Title = "Dresses", Type = ActionTypes.PostBack, Value = $"__shop_{JsonConvert.SerializeObject(new SearchModel{ Type = "φόρεμα" })}" },
                    new CardAction(){ Title = "Jeans", Type = ActionTypes.PostBack, Value = $"__shop_{JsonConvert.SerializeObject(new SearchModel{ Type = "τζιν" })}" },
                }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task ShowSearchResultsAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
        }

        private async Task CustomerServiceAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GeneralHelper.GetActualAsyncMethodName());

            Store store = context.PrivateConversationData.GetValue<Store>("StoreSelected");

            reply.Text = $"Customer service for {store.Name} store:";
            await context.PostAsync(reply);

            if (activity.ChannelId == "facebook")
            {
                var customerServiceContents = new FacebookGenericTemplateContent[4]
                {
                    new FacebookGenericTemplateContent()
                    {
                        Title = "Brands",
                        Subtitle = $"View all the available brands in {store.Name}.",
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/cs-brands.jpeg",
                        Buttons = new[] { new FacebookUrlButton(url: ApiNames.CustomerService + $"?business_id={store.Id}&service_id=1", title: "Brands") }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = "About",
                        Subtitle = $"Learn more about {store.Name}.",
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/cs-about.jpeg",
                        Buttons = new[] { new FacebookUrlButton(url: ApiNames.CustomerService + $"?business_id={store.Id}&service_id=2", title: "About") }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = "Returns Policy",
                        Subtitle = $"Learn everything you want to know about returns in {store.Name}.",
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/cs-returns.jpeg",
                        Buttons = new[] { new FacebookUrlButton(url: ApiNames.CustomerService + $"?business_id={store.Id}&service_id=3", title: "Returns") }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = "Shipping",
                        Subtitle = $"View the available shipping ways, days to deliver and more about {store.Name}.",
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/cs-shipping.jpeg",
                        Buttons = new[] { new FacebookUrlButton(url: ApiNames.CustomerService + $"?business_id={store.Id}&service_id=4", title: "Shipping") }
                    }
                };
                for (int i = 0; i < 4; i++)
                    customerServiceContents[i].Tap = new FacebookDefaultAction((customerServiceContents[i].Buttons.First() as FacebookUrlButton).Url);

                var bottomButton = new FacebookPostbackButton(title: "Contact", payload: "__cs_contact");

                reply.ChannelData = ChannelsHelper.Facebook.Templates.CreateListTemplate(customerServiceContents, bottomButton);
                reply.Text = null;
            }
            else
            {
                reply.AttachmentLayout = AttachmentLayoutTypes.List;

                context.Wait(UnimplementedAsync);
                return;
            }

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "Back to store", Type = ActionTypes.PostBack, Value = "__store_select" },
                    new CardAction(){ Title = "Shop", Type = ActionTypes.PostBack, Value = "__store_shop" }
                }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task WindowShopAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GeneralHelper.GetActualAsyncMethodName());

            Store store = context.PrivateConversationData.GetValue<Store>("StoreSelected");
            string gender = context.UserData.GetValue<string>("Gender");
            context.ConversationData.TryGetValue("WindowShopNextPage", out int currentPage);

            var collectionsApi = new API<CollectionsRoot>();
            var apiParams = new Dictionary<string, string>(3)
            {
                { "page", currentPage.ToString() },
                { "gender", (gender == "Female") ? "0" : "1" },
                { "created_by", store.Id.ToString() }
            };
            var collectionsRoot = await collectionsApi.CallAsync(apiParams)
                ?? await collectionsApi.CallAsync(apiParams.ToDictionary(kvp => kvp.Key, kvp => (kvp.Key == "page") ? (currentPage = 0).ToString() : kvp.Value));
            context.ConversationData.SetValue("WindowShopNextPage", currentPage + 1);

            if (collectionsRoot == null)
            {
                reply.Text = $"Sorry, no collections found for {store.Name.ToLower()} :/";
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Back to store", Type = ActionTypes.PostBack, Value = "__store_select" },
                        new CardAction(){ Title = "Shop", Type = ActionTypes.PostBack, Value = "__store_shop" }
                    }
                };
                await context.PostAsync(reply);
                context.Wait(MessageReceivedAsync);
                return;
            }

            if (activity.ChannelId == "facebook")
            {
                var contentsForCurrentPage = new List<FacebookGenericTemplateContent>(4)
                {
                    new FacebookGenericTemplateContent()
                    {
                        Title = GeneralHelper.CapitalizeFirstLetter(store.Name) + " window shopping - " + ((gender == "Male") ? "Men" : "Women"),
                        Subtitle = $"Fashion suggestions by {store.Name.ToLower()} - Page {currentPage + 1}",
                        ImageUrl = store.ImageUrl
                    }
                };

                Collection collection;
                for (int i = 0; i < 3 && i < collectionsRoot.Collections.Count; i++)
                {
                    collection = collectionsRoot.Collections[i];
                    contentsForCurrentPage.Add(
                        new FacebookGenericTemplateContent()
                        {
                            Title = GeneralHelper.CapitalizeFirstLetter(collection.Title),
                            Subtitle = $"By {store.Name}",
                            Buttons = new[] { new FacebookPostbackButton(title: "View items", payload: $"__view_collection_{collection.Id}") },
                            ImageUrl = collection.ImageUrl ??
                                $"https://zoiebot.azurewebsites.net/Files/Images/Occasions/Outdoor/{gender}/{i + 1}.jpg"
                        });
                }

                FacebookPostbackButton bottomPageButton = null;
                if (!(currentPage == 0 && collectionsRoot.RemainingPages == 0))
                    bottomPageButton = new FacebookPostbackButton(title: collectionsRoot.RemainingPages > 0 ? "Next page" : "First page", payload: "__more_collections");

                reply.ChannelData = ChannelsHelper.Facebook.Templates.CreateListTemplate(contentsForCurrentPage.ToArray(), bottomPageButton);
            }
            else
            {
                reply.AttachmentLayout = AttachmentLayoutTypes.List;

                context.Wait(UnimplementedAsync);
                return;
            }


            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "Back to store", Type = ActionTypes.PostBack, Value = "__store_select" },
                    new CardAction(){ Title = "New search", Type = ActionTypes.PostBack, Value = "__menu_new_search" }
                }
            };

            await context.PostAsync(reply);
            context.Wait(MessageReceivedAsync);
        }

        private async Task ApparelsForCollectionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GeneralHelper.GetActualAsyncMethodName());

            string gender = context.UserData.GetValue<string>("Gender");
            string collectionId = null;
            if (activity.Text.StartsWith("__view_collection_"))
                collectionId = activity.Text.Remove(0, "__view_collection_".Length);
            if (!string.IsNullOrWhiteSpace(collectionId))
                context.PrivateConversationData.SetValue("LastStoreCollectionViewed", collectionId);
            else if (!context.PrivateConversationData.TryGetValue("LastStoreCollectionViewed", out collectionId))
                await this.WindowShopAsync(context, result);

            var collectionApparelsApi = new API<CollectionApparelsRoot>();
            var collectionApparelsRoot = await collectionApparelsApi.CallAsync(new Dictionary<string, string>(1) { { "collection_id", collectionId } });

            reply.Text = "Here you are!";
            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            foreach (var apparel in collectionApparelsRoot.Items)
            {
                reply.Attachments.Add(
                    new HeroCard()
                    {
                        Title = GeneralHelper.CapitalizeFirstLetter(apparel.Name),
                        Subtitle = apparel.PriceString + "€",
                        Images = new List<CardImage> { new CardImage { Url = apparel.ImageUrl } },
                        Buttons = new List<CardAction> { new CardAction { Title = "Buy", Type = ActionTypes.OpenUrl, Value = apparel.Link } },
                        Tap = new CardAction { Type = ActionTypes.OpenUrl, Value = apparel.Link }
                    }.ToAttachment());
            }

            reply.Attachments.Add(
                new HeroCard()
                {
                    Title = "Collection rate",
                    Subtitle = "Did you like that set?",
                    Images = new List<CardImage> { new CardImage { Url = "http://zoie.io/images/Brand-icon.png" } },
                    Buttons = new List<CardAction>
                    {
                        new CardAction { Title = "😍 Very much", Type = ActionTypes.PostBack, Value = $"__feedback_rate_{collectionId}_3"},
                        new CardAction { Title = "😐 So and so", Type = ActionTypes.PostBack, Value = $"__feedback_rate_{collectionId}_2"},
                        new CardAction { Title = "😒 Not at all", Type = ActionTypes.PostBack, Value = $"__feedback_rate_{collectionId}_1"},
                    }
                }.ToAttachment());

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "More Collections", Type = ActionTypes.PostBack, Value = "__more_collections" },
                    new CardAction(){ Title = "Back to store", Type = ActionTypes.PostBack, Value = "__store_select" }
                }
            };

            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task ContactAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply("Contact us at info@zoie.io and a represenative will contact you.");
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GeneralHelper.GetActualAsyncMethodName());

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = "Back to store", Type = ActionTypes.PostBack, Value = "__store_select" },
                        new CardAction(){ Title = "Shop", Type = ActionTypes.PostBack, Value = "__store_shop" }
                    }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task FeedbackRateAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply("Thank your for your feedback!");
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GeneralHelper.GetActualAsyncMethodName());

            string[] feedbackData = null;
            if (activity.Text.StartsWith("__feedback_rate_"))
                feedbackData = activity.Text.Remove(0, "__feedback_rate_".Length).Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries);

            if (feedbackData != null)
            {
                int collectionId = int.Parse(feedbackData[0]);
                int rate = int.Parse(feedbackData[1]);

                //TODO: Store rate for collection
            }

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = "More Collections", Type = ActionTypes.PostBack, Value = "__more_collections" },
                            new CardAction(){ Title = "Back to store", Type = ActionTypes.PostBack, Value = "__store_select" }
                        }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task UnimplementedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            await context.PostAsync("Feature not available yet in " + GeneralHelper.CapitalizeFirstLetter(activity.ChannelId));

            await this.EndAsync(context, result);
        }

        private async Task EndAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result;

            await context.PostAsync("Hope you found what you were looking for! ☺");
            context.Done(activity);
        }
    }
}