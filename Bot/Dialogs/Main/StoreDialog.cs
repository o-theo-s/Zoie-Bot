using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Apis;
using Apis.Models;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

            context.ConversationData.SetValue("StoreSelected", JsonConvert.DeserializeObject<Store>(referral.Item));
            await this.ShowFunctionsForStoreAsync(context, result);
        }

        private async Task SelectStoreAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply("Let’s pick a store and buy an outfit for today!");
            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;

            context.ConversationData.TryGetValue("StoresNextPage", out int currentPage);

            var storesApi = new API<StoresRoot>();
            var storesRoot = await storesApi.CallAsync(new Dictionary<string, string>(1) { { "page", currentPage.ToString() } });
            if (storesRoot.RemainingPages > 0)
                context.ConversationData.SetValue("StoresNextPage", currentPage + 1);
            else
                context.ConversationData.RemoveValue("StoresNextPage");

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
                        Subtitle = store.Link,
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

            context.Wait(StoreSelectedAsync);
        }

        private async Task StoreSelectedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();

            if (activity.Text.StartsWith("__store"))
            {
                if (activity.Text.StartsWith("__store_select"))
                {
                    Store store = JsonConvert.DeserializeObject<Store>(activity.Text.Remove(0, "__store_select_".Length));
                    context.ConversationData.SetValue("StoreSelected", store);

                    await this.ShowFunctionsForStoreAsync(context, result);
                }
                else if (activity.Text == "__store_show_more")
                {
                    await this.SelectStoreAsync(context, result);
                }
            }
            //TODO: Remove else if - It will work from persistent menu
            else if (activity.Text == "__menu_new_search")
            {
                await context.PostAsync("Alright!");
                await this.EndAsync(context, result);
            }
            else if (activity.Text == "__personality_answer")
            {
                await this.SelectStoreAsync(context, result);
            }
            else
            {
                await context.Forward(new GlobalLuisDialog<object>(), StoreSelectedAsync, activity);
            }

            return;
        }

        private async Task ShowFunctionsForStoreAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();

            Store store = context.ConversationData.GetValue<Store>("StoreSelected");

            if (activity.ChannelId == "facebook")
            {
                var storeContents = new List<FacebookGenericTemplateContent>(4)
                {
                    new FacebookGenericTemplateContent()
                    {
                        Title = GeneralHelper.CapitalizeFirstLetter(store.Name),
                        Subtitle = store.Link,
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
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/store-help.jpeg",
                        Buttons = new[] { new FacebookPostbackButton(title: "More Info", payload: "__store_info") }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = "Window Shopping",
                        Subtitle = $"View all the collections created by {store.Name}!",
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/store-window.jpg",
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

            context.Wait(AfterShowFunctionsForStoreAsync);
        }

        private async Task AfterShowFunctionsForStoreAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            switch (activity.Text)
            {
                case "__store_shop":
                    break;
                case "__store_info":
                    await this.SelectCustomerServiceTypeAsync(context, result);
                    return;
                case "__store_window":
                    break;
                case "__store_reselect":
                    context.ConversationData.RemoveValue("StoreSelected");
                    await this.SelectStoreAsync(context, result);
                    return;
                case "__menu_new_search":
                    context.ConversationData.RemoveValue("StoreSelected");
                    await this.EndAsync(context, result);
                    return;
                default:
                    break;
            }
            
            await this.UnimplementedAsync(context, result);
        }

        private async Task ShopAsync(IDialogContext context, IAwaitable<object> result)
        {

        }

        private async Task SelectCustomerServiceTypeAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();

            Store store = context.ConversationData.GetValue<Store>("StoreSelected");

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
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/cs-brands0.jpg",
                        Buttons = new[] { new FacebookUrlButton(url: ApiNames.CustomerService + $"?business_id={store.Id}&service_id=1", title: "Brands") }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = "About",
                        Subtitle = $"Learn more about {store.Name}.",
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/cs-about0.jpg",
                        Buttons = new[] { new FacebookUrlButton(url: ApiNames.CustomerService + $"?business_id={store.Id}&service_id=2", title: "About") }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = "Returns Policy",
                        Subtitle = $"Learn everything you want to know about returns in {store.Name}.",
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/cs-returns0.jpg",
                        Buttons = new[] { new FacebookUrlButton(url: ApiNames.CustomerService + $"?business_id={store.Id}&service_id=3", title: "Returns") }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = "Shipping",
                        Subtitle = $"View the available shipping ways, days to deliver and more about {store.Name}.",
                        ImageUrl = "https://zoiebot.azurewebsites.net/Files/Images/Stores/cs-shipping0.jpg",
                        Buttons = new[] { new FacebookUrlButton(url: ApiNames.CustomerService + $"?business_id={store.Id}&service_id=4", title: "Shipping") }
                    }
                };
                for (int i = 0; i < 4; i++)
                    customerServiceContents[i].Tap = new FacebookDefaultAction((customerServiceContents[i].Buttons.First() as FacebookUrlButton).Url);

                reply.ChannelData = ChannelsHelper.Facebook.Templates.CreateListTemplate(customerServiceContents, null, "compact");
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
                    new CardAction(){ Title = "Back", Type = ActionTypes.PostBack, Value = $"__store_select_{JsonConvert.SerializeObject(store)}" }
                }
            };
            await context.PostAsync(reply);

            context.Wait(StoreSelectedAsync);
        }

        private async Task WindowShopAsync(IDialogContext context, IAwaitable<object> result)
        {

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