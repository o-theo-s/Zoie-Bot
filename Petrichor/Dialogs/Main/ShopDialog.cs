using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using System.Threading.Tasks;
using Zoie.Apis;
using Zoie.Apis.Models;
using Zoie.Helpers;
using static Zoie.Petrichor.Dialogs.Main.FiltersDialog;

namespace Zoie.Petrichor.Dialogs.Main
{
    [Serializable]
    public class ShopDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Call(new FiltersDialog(), this.SelectStorePromptAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            DialogsHelper.EventToMessageActivity(ref activity, ref result);

            switch (activity.Text)
            {
                case string text when text.StartsWith("__store_select"):
                    string storeJson = activity.Text.Remove(0, "__store_select".Length);
                    if (!string.IsNullOrWhiteSpace(storeJson))
                    {
                        Store store = JsonConvert.DeserializeObject<Store>(storeJson.Remove(0, 1));
                        context.PrivateConversationData.SetValue("StoreSelected", store);
                    }
                    await this.ShowSearchResultsAsync(context, result);
                    return;
                case "__store_show_more":
                    await this.SelectStoreAsync(context, result);
                    return;
                case "__search_results_show_more":
                    await this.ShowSearchResultsAsync(context, result);
                    return;
                case "__shop_filters_change":
                    context.ConversationData.RemoveValue("SearchResultsNextPage");
                    await context.Forward(new FiltersDialog(), this.ShowSearchResultsAsync, activity);
                    return;
                case "__shop_store_change":
                    context.ConversationData.RemoveValue("SearchResultsNextPage");
                    context.ConversationData.RemoveValue("StoresNextPage");
                    await this.SelectStoreAsync(context, result);
                    return;
                case string text when text.ToLowerInvariant().Contains("yes"):
                    if (context.PrivateConversationData.GetValue<string>("LastShopSubdialog") == nameof(this.SelectStoreAsync))
                        await this.SelectStoreAsync(context, result);
                    return;
                case string text when text.ToLowerInvariant().Contains("no"):
                    if (context.PrivateConversationData.GetValue<string>("LastShopSubdialog") == nameof(this.SelectStoreAsync))
                        await this.ShowSearchResultsAsync(context, result);
                    return;
                case "__continue":
                    await this.AfterPersonalityDialogAsync(context, result);
                    return;
                default:
                    await context.Forward(new PersonalityDialog(), AfterPersonalityDialogAsync, activity);
                    return;
            }
        }

        private async Task SelectStorePromptAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply("Would you like to select a specific store for your search?");
            context.PrivateConversationData.SetValue("LastShopSubdialog", GeneralHelper.GetActualAsyncMethodName());

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "Yes", Type = ActionTypes.PostBack, Value = "yes" },
                    new CardAction(){ Title = "No", Type = ActionTypes.PostBack, Value = "no" },
                }
            };

            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task SelectStoreAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply("Let’s pick a store!");
            context.PrivateConversationData.SetValue("LastShopSubdialog", GeneralHelper.GetActualAsyncMethodName());

            context.ConversationData.TryGetValue("StoresNextPage", out int currentPage);

            var storesApi = new ApiCaller<StoresRoot>();
            var storesRoot = await storesApi.CallAsync(new Dictionary<string, string>(1) { { "page", currentPage.ToString() } })
                ?? await storesApi.CallAsync(new Dictionary<string, string>(1) { { "page", (currentPage = 0).ToString() } });
            context.ConversationData.SetValue("StoresNextPage", currentPage + 1);

            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            Store store;
            for (int i = 0; i < 9 && i < storesRoot.Stores.Count; i++)
            {
                store = storesRoot.Stores[i];
                if (string.IsNullOrWhiteSpace(store.ImageUrl))
                    store.ImageUrl = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Stores/zoie_logo.png";
                reply.Attachments.Add(
                    new HeroCard()
                    {
                        Title = GeneralHelper.CapitalizeFirstLetter(store.Name),
                        Images = new List<CardImage> { new CardImage { Url = store.ImageUrl } },
                        Buttons = new List<CardAction> { new CardAction { Title = "Select", Type = ActionTypes.PostBack, Value = $"__store_select_{JsonConvert.SerializeObject(store)}" } }
                    }.ToAttachment());
            }
            reply.Attachments.Add(
                new HeroCard()
                {
                    Title = "Create Your Store!",
                    Subtitle = "Create your own messenger store for FREE, NOW!!",
                    Images = new List<CardImage> { new CardImage { Url = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Stores/store-create.jpg" } },
                    Buttons = new List<CardAction> { new CardAction { Title = "Create", Type = ActionTypes.OpenUrl, Value = "http://zoie.io/sign-up.php" } }
                }.ToAttachment());

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = storesRoot.RemainingPages > 0 ? "Next page" : "First page", Type = ActionTypes.PostBack, Value = "__store_show_more" }
                }
            };
            if (storesRoot.RemainingPages == 0 && currentPage == 0)
                reply.SuggestedActions.Actions.RemoveAt(0);

            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task ShowSearchResultsAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastShopSubdialog", GeneralHelper.GetActualAsyncMethodName());

            var filters = context.PrivateConversationData.GetValue<Dictionary<string, string>>("Filters");
            context.ConversationData.TryGetValue("SearchResultsNextPage", out int currentPage);

            SearchModel searchAttributes = new SearchModel
            {
                Gender = context.UserData.GetValue<string>("Gender") == "Female" ? "0" : "1",
                Manufacturer = filters[Filters.Manufacturer.ToString().ToLower()],
                Page = currentPage,
                Size = filters[Filters.Size.ToString().ToLower()]
            };
            if (context.PrivateConversationData.TryGetValue("StoreSelected", out Store store))
                searchAttributes.Shop = store.Id;
            if (DialogsHelper.TryGetResourceValue<Resources.Dictionaries.Apparels.Colors>(filters[Filters.Color.ToString().ToLower()], out string color))
                searchAttributes.Color = color;
            if (DialogsHelper.TryGetResourceValue<Resources.Dictionaries.Apparels.Types>(filters[Filters.Type.ToString().ToLower()], out string type))
                searchAttributes.Type = type;
            if (int.TryParse(filters["max_price"], out int maxPrice))
                searchAttributes.Max_Price = maxPrice;
            if (int.TryParse(filters["min_price"], out int minPrice))
                searchAttributes.Min_Price = minPrice;

            var searchApi = new ApiCaller<ApparelsRoot>();
            ApparelsRoot apparelsRoot = await searchApi.CallAsync(searchAttributes.GetAttributesDictionary());
            if (apparelsRoot == null)
            {
                searchAttributes.Page = currentPage = 0;
                apparelsRoot = await searchApi.CallAsync(searchAttributes.GetAttributesDictionary());
            }
            context.ConversationData.SetValue("SearchResultsNextPage", currentPage + 1);

            reply.Text = $"Here's what I found {(store != null ? "in " + store.Name : "")} (page {currentPage + 1} of {apparelsRoot.RemainingPages + currentPage + 1}):";
            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            foreach (var apparel in apparelsRoot.Apparels)
            {
                reply.Attachments.Add(
                    new HeroCard()
                    {
                        Title = GeneralHelper.CapitalizeFirstLetter(apparel.Name),
                        Images = new List<CardImage> { new CardImage { Url = apparel.ImageUrl } },
                        Subtitle = apparel.Price + "€",
                        Buttons = new List<CardAction> { new CardAction { Title = "Buy", Type = ActionTypes.OpenUrl, Value = apparel.ProductUrl } },
                        Tap = new CardAction { Type = ActionTypes.OpenUrl, Value = apparel.ProductUrl }
                    }.ToAttachment());
            }
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = apparelsRoot.RemainingPages > 0 ? "Next page" : "First page", Type = ActionTypes.PostBack, Value = "__search_results_show_more" },
                    new CardAction(){ Title = "Change filters", Type = ActionTypes.PostBack, Value = "__shop_filters_change" },
                    new CardAction(){ Title = (store == null ? "Select" : "Reselect") + " store", Type = ActionTypes.PostBack, Value = "__shop_store_change" }
                }
            };
            if (apparelsRoot.RemainingPages == 0 && currentPage == 0)
                reply.SuggestedActions.Actions.RemoveAt(0);

            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task AfterPersonalityDialogAsync(IDialogContext context, IAwaitable<object> result)
        {
            var lastSubdialog = context.PrivateConversationData.GetValue<string>("LastShopSubdialog");
            MethodInfo reshowLastSubdialog = this.GetType().GetMethod(lastSubdialog, BindingFlags.NonPublic | BindingFlags.Instance);

            if (reshowLastSubdialog.Name == nameof(this.SelectStoreAsync)
                            && context.ConversationData.TryGetValue("StoresNextPage", out int currentPage))
                context.ConversationData.SetValue("StoresNextPage", currentPage - 1);
            if (reshowLastSubdialog.Name == nameof(this.ShowSearchResultsAsync)
                            && context.ConversationData.TryGetValue("SearchResultsNextPage", out currentPage))
                context.ConversationData.SetValue("SearchResultsNextPage", currentPage - 1);

            await (Task)reshowLastSubdialog.Invoke(this, new object[] { context, new AwaitableFromItem<IActivity>(context.Activity) });
        }
    }
}