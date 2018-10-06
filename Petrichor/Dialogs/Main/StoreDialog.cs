using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Zoie.Apis;
using Zoie.Apis.Models;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Zoie.Petrichor.Models;
using Zoie.Helpers;
using Zoie.Helpers.Channels.Facebook.Library;
using Zoie.Resources.DialogReplies;
using static Zoie.Helpers.DialogsHelper;
using static Zoie.Helpers.GeneralHelper;
using static Zoie.Resources.DialogReplies.StoreReplies;
using static Zoie.Resources.DialogReplies.GeneralReplies;
using Zoie.Petrichor.Dialogs.Main.Prefatory;
using Zoie.Petrichor.Dialogs.NLU;

#pragma warning disable CS0642 // Possible mistaken empty statement

namespace Zoie.Petrichor.Dialogs.Main
{
    [Serializable]
    public class StoreDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            if (context.PrivateConversationData.ContainsKey("Referral"))
                context.Wait(ReferralReceivedAsync);
            else if (context.ConversationData.ContainsKey("MarketplaceSelected"))
            {
                context.ConversationData.RemoveValue("StoreSelected");
                context.ConversationData.RemoveValue("MarketplaceSelected");
                if (!context.PrivateConversationData.ContainsKey("Filters"))
                    context.PrivateConversationData.SetValue("Filters", new ShoppingFilters());

                (context.Activity as Activity).Text = "__store_shop_filters";
                context.Wait(MessageReceivedAsync);
            }
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
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            context.ConversationData.TryGetValue("StoresNextPage", out int currentPage);

            var storesApi = new ApiCaller<StoresRoot>();
            var storesRoot = await storesApi.CallAsync(new Dictionary<string, string>(1) { { "page", currentPage.ToString() } })
                ?? await storesApi.CallAsync(new Dictionary<string, string>(1) { { "page", (currentPage = 0).ToString() } });
            context.ConversationData.SetValue("StoresNextPage", currentPage + 1);

            reply.Text = GetResourceValue<StoreReplies>(nameof(SelectStore), locale);
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
                        Title = CapitalizeFirstLetter(store.Name),
                        Images = new List<CardImage> { new CardImage { Url = store.ImageUrl} },
                        Buttons = new List<CardAction>
                        {
                            new CardAction { Title = GetResourceValue<StoreReplies>(nameof(SelectBtn), locale), Type = ActionTypes.PostBack, Value = $"__store_select_{JsonConvert.SerializeObject(store)}" },
                            new CardAction { Title = GetResourceValue<StoreReplies>(nameof(AboutBtn), locale), Type = ActionTypes.OpenUrl, Value = ApiNames.CustomerService + $"?business_id={store.Id}&service_id=2" }
                        }
                    }.ToAttachment());
            }
            reply.Attachments.Add(
                new HeroCard()
                {
                    Title = GetResourceValue<StoreReplies>(nameof(CreateTitle), locale),
                    Subtitle = GetResourceValue<StoreReplies>(nameof(CreateSubtitle), locale),
                    Images = new List<CardImage> { new CardImage { Url = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Stores/store-create.jpg" } },
                    Buttons = new List<CardAction> { new CardAction {
                        Title = GetResourceValue<StoreReplies>(nameof(CreateBtn), locale),
                        Type = ActionTypes.OpenUrl, Value = "http://zoie.io/sign-up.php" } }
                }.ToAttachment());

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){
                        Title = GetResourceValue<StoreReplies>(storesRoot.RemainingPages > 0 ? nameof(MoreStores) : nameof(RevisitStores), locale),
                        Type = ActionTypes.PostBack, Value = "__store_show_more" }
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
            EventToMessageActivity(ref activity, ref result);

            string lastSubdialog = null;
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
                        await this.StartShoppingAsync(context, result);
                    else
                        await context.Forward(new PersonalizationDialog(), StartShoppingAsync, activity);
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

                    var searchApparelsApi = new ApiCaller<ApparelsRoot>();
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
                case string text when text.StartsWith("__feedback_"):
                    await this.FeedbackAsync(context, result);
                    return;
                case "__store_shop_filters":
                    context.ConversationData.RemoveValue("SearchResultsNextPage");
                    await this.SelectShoppingFiltersAsync(context, result);
                    //await context.Forward(new FiltersDialog(), this.ShowSearchResultsAsync, activity);
                    return;
                case "__search_results_show_more":
                    await this.ShowSearchResultsAsync(context, result);
                    return;
                case "__search_results_show_all":
                    context.ConversationData.RemoveValue("SearchResultsNextPage");
                    goto case "__search_results_show_more";
                case "__personality_answer":
                case "__continue":
                    lastSubdialog = context.PrivateConversationData.GetValue<string>("LastStoreSubdialog");
                    MethodInfo reshowLastSubdialog = this.GetType().GetMethod(lastSubdialog, BindingFlags.NonPublic | BindingFlags.Instance);

                    if ( reshowLastSubdialog.Name == nameof(this.SelectStoreAsync) 
                            && context.ConversationData.TryGetValue("StoresNextPage", out int currentPage) )
                        context.ConversationData.SetValue("StoresNextPage", currentPage - 1);
                    if ( reshowLastSubdialog.Name == nameof(this.WindowShopAsync) 
                            && context.ConversationData.TryGetValue("WindowShopNextPage", out currentPage))
                        context.ConversationData.SetValue("WindowShopNextPage", currentPage - 1);
                    if (reshowLastSubdialog.Name == nameof(this.ShowSearchResultsAsync)
                            && context.ConversationData.TryGetValue("SearchResultsNextPage", out currentPage))
                        context.ConversationData.SetValue("SearchResultsNextPage", currentPage - 1);

                    await (Task) reshowLastSubdialog.Invoke(this, new object[] { context, result });
                    return;
                case string text when text.StartsWith("__filters_change"):
                    await this.ChangeShoppingFilterAsync(context, result);
                    return;
                case string text when text.StartsWith("__filters_set"):
                    await this.SetShoppingFilterAsync(context, result);
                    return;
                default:
                    lastSubdialog = context.PrivateConversationData.GetValue<string>("LastStoreSubdialog");
                    ResumeAfter<object> resumeAfter;
                    if (lastSubdialog == nameof(this.StartShoppingAsync))
                    {
                        context.PrivateConversationData.SetValue("Filters", new ShoppingFilters());
                        resumeAfter = this.ShowSearchResultsAsync;
                    }
                    else
                        resumeAfter = this.SelectShoppingFiltersAsync;

                    await context.Forward(new WitStoreDialog(), resumeAfter, activity);
                    return;
            }
        }

        private async Task ShowStoreMenuAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            Store store = context.PrivateConversationData.GetValue<Store>("StoreSelected");

            if (activity.ChannelId == "facebook")
            {
                var storeContents = new List<FacebookGenericTemplateContent>(4)
                {
                    new FacebookGenericTemplateContent()
                    {
                        Title = CapitalizeFirstLetter(store.Name),
                        ImageUrl = store.ImageUrl
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = GetResourceValue<StoreReplies>(nameof(ShopTitle), locale),
                        Subtitle = GetResourceValue<StoreReplies>(nameof(ShopSubtitle), locale),
                        ImageUrl = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Stores/store-shop.jpg",
                        Buttons = new[] { new FacebookPostbackButton(title: GetResourceValue<StoreReplies>(nameof(ShopBtn), locale), payload: "__store_shop") }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = GetResourceValue<StoreReplies>(nameof(CustomerServiceTitle), locale),
                        Subtitle = GetResourceValue<StoreReplies>(nameof(CustomerServiceSubtitle), locale, store.Name),
                        ImageUrl = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Stores/store-info.jpg",
                        Buttons = new[] { new FacebookPostbackButton(title: GetResourceValue<StoreReplies>(nameof(CustomerServiceBtn), locale), payload: "__store_info") }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = GetResourceValue<StoreReplies>(nameof(WindowShoppingTitle), locale),
                        Subtitle = GetResourceValue<StoreReplies>(nameof(WindowShoppingSubtitle), locale, store.Name),
                        ImageUrl = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Stores/store-window.jpg",
                        Buttons = new[] { new FacebookPostbackButton(title: GetResourceValue<StoreReplies>(nameof(WindowShoppingBtn), locale), payload: "__store_window") }
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
                                        Title = GetResourceValue<StoreReplies>(nameof(ReferralTitle), locale, CapitalizeFirstLetter(store.Name)),
                                        Subtitle = GetResourceValue<StoreReplies>(nameof(ReferralSubtitle), locale, CapitalizeFirstLetter(store.Name)),
                                        Buttons = new[]
                                        {
                                            new FacebookUrlButton(
                                                url: $"https://m.me/{ConfigurationManager.AppSettings.Get("MessengerBotId")}?" +
                                                        $"ref={Hashify($"__referral__{activity.From.Id}__{Referral.Types.Store}__{JsonConvert.SerializeObject(store)}")}",
                                                title: GetResourceValue<StoreReplies>(nameof(ReferralBtn), locale))
                                        },
                                        Tap = new FacebookDefaultAction(
                                                url: $"https://m.me/{ConfigurationManager.AppSettings.Get("MessengerBotId")}?" +
                                                        $"ref={Hashify($"__referral__{activity.From.Id}__{Referral.Types.Store}__{JsonConvert.SerializeObject(store)}")}")
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
                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(ReselectBtn), locale), Type = ActionTypes.PostBack, Value = "__store_reselect" }
                }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task StartShoppingAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            if (!context.PrivateConversationData.ContainsKey("Filters"))
                context.PrivateConversationData.SetValue("Filters", new ShoppingFilters());

            reply.Text = GetResourceValue<StoreReplies>(nameof(ShopIntro), locale);
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FilteredSearch), locale), Type = ActionTypes.PostBack, Value = "__store_shop_filters" },
                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(ViewAll), locale), Type = ActionTypes.PostBack, Value = "__search_results_show_all" }
                }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task SelectShoppingFiltersAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            var quickActions = new List<CardAction>()
            {
                new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FilterPrice), locale), Type = ActionTypes.PostBack, Value = "__filters_change_Price" },
                new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FilterCategory), locale), Type = ActionTypes.PostBack, Value = "__filters_change_Type" },
                new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FilterSize), locale), Type = ActionTypes.PostBack, Value = "__filters_change_Size" },
                new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FilterColor), locale), Type = ActionTypes.PostBack, Value = "__filters_change_Color" },
                new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FilterBrand), locale), Type = ActionTypes.PostBack, Value = "__filters_change_Manufacturer" },
                new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FilterGender), locale), Type = ActionTypes.PostBack, Value = "__filters_change_Gender" }
            };

            ShoppingFilters filters = context.PrivateConversationData.GetValue<ShoppingFilters>("Filters");
            if (filters.EmptyFilters())
            {
                reply.Text = GetResourceValue<StoreReplies>(nameof(FiltersEmpty), locale);
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = quickActions
                };
                await context.PostAsync(reply);
            }
            else
            {
                reply.Text = GetResourceValue<StoreReplies>(nameof(FiltersCurrent), locale, "\n\n");
                foreach (var filterProp in typeof(ShoppingFilters).GetProperties())
                {
                    if (filterProp.GetValue(filters) != null)
                    {
                        if (!filterProp.Name.ToLowerInvariant().Contains("price"))
                        {
                            if (filterProp.Name == nameof(ShoppingFilters.Manufacturer))
                                reply.Text += GetResourceValue<StoreReplies>(nameof(FilterBrand), locale);
                            else if (filterProp.Name == nameof(ShoppingFilters.Type))
                                reply.Text += GetResourceValue<StoreReplies>(nameof(FilterCategory), locale);
                            else
                                reply.Text += GetResourceValue<StoreReplies>("Filter" + filterProp.Name, locale);

                            reply.Text += ": " + CapitalizeFirstLetter(filterProp.GetValue(filters).ToString()) + "\n\n";
                        }
                    }
                }

                if (filters.MaxPrice != null)
                    reply.Text += GetResourceValue<StoreReplies>(nameof(FilterPrice), locale) + $": {filters.MinPrice ?? 0} - {filters.MaxPrice} €";
                else if (filters.MinPrice != null)
                    reply.Text += GetResourceValue<StoreReplies>(nameof(FilterPrice), locale) + ": " 
                        + GetResourceValue<StoreReplies>(nameof(MoreThan), locale) + $" {filters.MinPrice} €";

                quickActions.Insert(0, new CardAction() {
                    Title = GetResourceValue<StoreReplies>(nameof(NoContinueBtn), locale),
                    Type = ActionTypes.PostBack, Value = "__filters_change_no" });
                quickActions.Add(new CardAction() {
                    Title = GetResourceValue<GeneralReplies>(nameof(Reset), locale),
                    Type = ActionTypes.PostBack, Value = "__filters_change_reset" });
                await context.PostAsync(reply);

                reply.Text = GetResourceValue<StoreReplies>(nameof(FiltersChangeQ), locale);
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = quickActions
                };
                await context.PostAsync(reply);
            }

            context.Wait(MessageReceivedAsync);
        }

        private async Task ChangeShoppingFilterAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            string filterInput = activity.Text.Split('_').Last();
            if (filterInput == "no")
            {
                await this.ShowSearchResultsAsync(context, result);
                return;
            }
            else if (filterInput == "reset")
            {
                context.PrivateConversationData.SetValue("Filters", new ShoppingFilters());
                await this.StartShoppingAsync(context, result);
                return;
            }
            else
            {
                if (Enum.TryParse(filterInput, out ShoppingFilters.Filters filterToChange))
                {
                    switch (filterToChange)
                    {
                        case ShoppingFilters.Filters.Price:
                            reply.Text = GetResourceValue<StoreReplies>(nameof(FiltersPriceQ), locale, "\n\n");
                            reply.SuggestedActions = new SuggestedActions()
                            {
                                Actions = new List<CardAction>()
                                {
                                    new CardAction(){ Title = "0-20", Type = ActionTypes.PostBack, Value = "__filters_set_Price_0-20" },
                                    new CardAction(){ Title = "21-50", Type = ActionTypes.PostBack, Value = "__filters_set_Price_21-50" },
                                    new CardAction(){ Title = "51-70", Type = ActionTypes.PostBack, Value = "__filters_set_Price_51-70" },
                                    new CardAction(){ Title = "71-100", Type = ActionTypes.PostBack, Value = "__filters_set_Price_71-100" },
                                    new CardAction(){ Title = "101-150", Type = ActionTypes.PostBack, Value = "__filters_set_Price_101-150" },
                                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FiltersAny), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Price_any" },
                                    new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(Back), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Price_cancel" },
                                }
                            };
                            break;
                        case ShoppingFilters.Filters.Type:
                            reply.Text = GetResourceValue<StoreReplies>(nameof(FiltersCategoryQ), locale, "\n\n");
                            reply.SuggestedActions = new SuggestedActions()
                            {
                                Actions = new List<CardAction>()
                                {
                                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FiltersCategoryA1), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Type_t-shirt" },
                                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FiltersCategoryA2), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Type_trousers" },
                                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FiltersCategoryA3), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Type_dress" },
                                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FiltersCategoryA4), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Type_purse" },
                                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FiltersCategoryA5), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Type_shoes" },
                                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FiltersCategoryA6), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Type_jacket" },
                                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FiltersAny), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Type_any" },
                                    new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(Back), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Type_cancel" }
                                }
                            };
                            break;
                        case ShoppingFilters.Filters.Size:
                            reply.Text = GetResourceValue<StoreReplies>(nameof(FiltersSizeQ), locale);
                            if (context.PrivateConversationData.TryGetValue("Filters", out ShoppingFilters filters))
                            {
                                if (filters.Type != "shoes")
                                {
                                    reply.Text += GetResourceValue<StoreReplies>(nameof(FiltersTypeOwn), locale, "\n\n");
                                    reply.SuggestedActions = new SuggestedActions()
                                    {
                                        Actions = new List<CardAction>()
                                        {
                                            new CardAction(){ Title = "XS", Type = ActionTypes.PostBack, Value = "__filters_set_Size_XS" },
                                            new CardAction(){ Title = "S", Type = ActionTypes.PostBack, Value = "__filters_set_Size_S" },
                                            new CardAction(){ Title = "M", Type = ActionTypes.PostBack, Value = "__filters_set_Size_M" },
                                            new CardAction(){ Title = "L", Type = ActionTypes.PostBack, Value = "__filters_set_Size_L" },
                                            new CardAction(){ Title = "XL", Type = ActionTypes.PostBack, Value = "__filters_set_Size_XL" },
                                            new CardAction(){ Title = "XXL", Type = ActionTypes.PostBack, Value = "__filters_set_Size_XXL" },
                                            new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FiltersAny), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Size_any" },
                                            new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(Back), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Size_cancel" }
                                        }
                                    };
                                }
                            }
                            break;
                        case ShoppingFilters.Filters.Color:
                            reply.Text = GetResourceValue<StoreReplies>(nameof(FiltersColorQ), locale, "\n\n");
                            reply.SuggestedActions = new SuggestedActions()
                            {
                                Actions = new List<CardAction>()
                                {
                                    new CardAction() { Title = GetResourceValue<StoreReplies>(nameof(FiltersColorWhite), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Color_white", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/white.jpg" },
                                    new CardAction() { Title = GetResourceValue<StoreReplies>(nameof(FiltersColorPink), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Color_pink", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/pink.jpg" },
                                    new CardAction() { Title = GetResourceValue<StoreReplies>(nameof(FiltersColorBlue), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Color_blue", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/blue.jpg" },
                                    new CardAction() { Title = GetResourceValue<StoreReplies>(nameof(FiltersColorBrown), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Color_brown", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/brown.jpg" },
                                    new CardAction() { Title = GetResourceValue<StoreReplies>(nameof(FiltersColorGreen), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Color_green", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/green.jpg" },
                                    new CardAction() { Title = GetResourceValue<StoreReplies>(nameof(FiltersColorOrange), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Color_orange", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/orange.jpg" },
                                    new CardAction() { Title = GetResourceValue<StoreReplies>(nameof(FiltersColorPurple), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Color_purple", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/purple.jpg" },
                                    new CardAction() { Title = GetResourceValue<StoreReplies>(nameof(FiltersColorRed), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Color_red", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/red.jpg" },
                                    new CardAction() { Title = GetResourceValue<StoreReplies>(nameof(FiltersColorBlack), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Color_black", Image = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Colors/black.jpg" },
                                    new CardAction() { Title = GetResourceValue<StoreReplies>(nameof(FiltersAny), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Color_any" },
                                    new CardAction() { Title = GetResourceValue<GeneralReplies>(nameof(Back), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Color_cancel" }
                                }
                            };
                            break;
                        case ShoppingFilters.Filters.Manufacturer:
                            reply.Text = GetResourceValue<StoreReplies>(nameof(FiltersBrandQ), locale);
                            reply.SuggestedActions = new SuggestedActions()
                            {
                                Actions = new List<CardAction>()
                                {
                                    new CardAction() { Title = GetResourceValue<StoreReplies>(nameof(FiltersAny), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Manufacturer_any" },
                                    new CardAction() { Title = GetResourceValue<GeneralReplies>(nameof(Back), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Manufacturer_cancel" }
                                }
                            };
                            break;
                        case ShoppingFilters.Filters.Gender:
                            /**/
                            reply.Text = StoreReplies.FiltersGenderQ;
                            reply.SuggestedActions = new SuggestedActions()
                            {
                                Actions = new List<CardAction>()
                                {
                                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FiltersGenderMale), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Gender_male" },
                                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FiltersGenderFemale), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Gender_female" },
                                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(FiltersAny), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Gender_any" },
                                    new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(Back), locale), Type = ActionTypes.PostBack, Value = "__filters_set_Gender_cancel" }
                                }
                            };
                            break;
                    }
                    await context.PostAsync(reply);
                }

                context.Wait(MessageReceivedAsync);
            }
        }

        private async Task SetShoppingFilterAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            string[] filterInputs = activity.Text.Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries).Skip(2).ToArray();

            string filterToSet = filterInputs[0];
            string filterValue = filterInputs[1];
            filterInputs = null;

            ShoppingFilters filters = context.PrivateConversationData.GetValue<ShoppingFilters>("Filters");
            if (filterValue != "cancel")
            {
                if (filterToSet == ShoppingFilters.Filters.Price.ToString())
                {
                    if (filterValue == "any")
                    {
                        filters.MinPrice = null;
                        filters.MaxPrice = null;
                    }
                    else
                    {
                        filters.MinPrice = float.Parse(filterValue.Split('-').First());
                        filters.MaxPrice = float.Parse(filterValue.Split('-').Last());
                    }
                }
                else
                {
                    typeof(ShoppingFilters).GetProperty(filterToSet).SetValue(filters, filterValue == "any" ? null : filterValue);
                }
                context.PrivateConversationData.SetValue("Filters", filters);

                reply.Text = GetResourceValue<StoreReplies>(nameof(FilterChanged), locale);
                await context.PostAsync(reply);
            }

            await this.SelectShoppingFiltersAsync(context, result);
        }

        private async Task ShowSearchResultsAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            var filters = context.PrivateConversationData.GetValue<ShoppingFilters>("Filters");
            context.ConversationData.TryGetValue("SearchResultsNextPage", out int currentPage);

            context.PrivateConversationData.TryGetValue("StoreSelected", out Store store);

            SearchModel searchAttributes = new SearchModel
            {
                Gender = (filters.Gender ?? context.UserData.GetValue<string>("Gender"))?.ToLowerInvariant() == "female" ? "0" : "1",
                Manufacturer = filters.Manufacturer?.ToLower(),
                Page = currentPage,
                Shop = store?.Id,
                Size = filters.Size?.ToLower(),
                Min_Price = filters.MinPrice == 0.0f ? null : filters.MinPrice,
                Max_Price = filters.MaxPrice == 0.0f ? null : filters.MaxPrice
            };
            bool hasTranslation = TryGetResourceValue<Resources.Dictionaries.Apparels.Colors>(filters.Color?.ToLower(), out string color, locale);
            searchAttributes.Color = hasTranslation ? color : filters.Color;
            hasTranslation = TryGetResourceValue<Resources.Dictionaries.Apparels.Types>(filters.Type?.ToLower(), out string type, locale);
            searchAttributes.Type = hasTranslation ? type : filters.Type;
            
            var searchApi = new ApiCaller<ApparelsRoot>();
            ApparelsRoot apparelsRoot = await searchApi.CallAsync(searchAttributes.GetAttributesDictionary());
            if (apparelsRoot == null)
            {
                searchAttributes.Page = currentPage = 0;
                apparelsRoot = await searchApi.CallAsync(searchAttributes.GetAttributesDictionary());

                var filterToRemove = (ShoppingFilters.Filters) Enum.GetValues(typeof(ShoppingFilters.Filters)).Cast<int>().Max();
                var searchModelProperties = typeof(SearchModel).GetProperties();
                List<string> filterNamesRemoved = new List<string>();
                while (apparelsRoot == null)
                {
                    while (filterToRemove > 0
                            && typeof(ShoppingFilters).GetProperties().First(p => p.Name.Contains(filterToRemove.ToString())).GetValue(filters) == null)
                        filterToRemove--;
                    if (filterToRemove == ShoppingFilters.Filters.Invalid)
                        break;

                    var attributesToRemove = searchModelProperties.Where((p) => p.Name.Contains(filterToRemove.ToString()));
                    foreach (var attr in attributesToRemove)
                        attr.SetValue(searchAttributes, null);
                    apparelsRoot = await searchApi.CallAsync(searchAttributes.GetAttributesDictionary());

                    filterNamesRemoved.Add(filterToRemove.ToString());
                    filterToRemove--;
                }

                if (filterToRemove == ShoppingFilters.Filters.Invalid)
                {
                    if (store != null)
                        reply.Text = GetResourceValue<StoreReplies>(nameof(TheStore), locale) + " " + store.Name + " ";
                    else
                        reply.Text = GetResourceValue<StoreReplies>(nameof(TheMarketplace), locale) + " ";
                    reply.Text += GetResourceValue<StoreReplies>(nameof(NotHave), locale) + " "
                        + GetResourceValue<GeneralReplies>(searchAttributes.Gender == "0" ? nameof(Womens) : nameof(Mens), locale) + " "
                        + GetResourceValue<StoreReplies>(nameof(Wear), locale) + ".";
                    reply.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(ChangeFiltersBtn), locale), Type = ActionTypes.PostBack, Value = "__store_shop_filters" },
                            new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(BackToStoreBtn), locale), Type = ActionTypes.PostBack, Value = "__store_select" },
                            new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(ChangeStoreBtn), locale), Type = ActionTypes.PostBack, Value = "__menu_new_store" }
                        }
                    };
                    if (store == null)
                    {
                        int qbNum = reply.SuggestedActions.Actions.Count;
                        reply.SuggestedActions.Actions.RemoveAt(qbNum - 1);
                        reply.SuggestedActions.Actions.RemoveAt(qbNum - 2);
                    }

                    await context.PostAsync(reply);
                    context.Wait(MessageReceivedAsync);
                    return;
                }
                else if (filterNamesRemoved.Count > 0)
                {
                    var filtersProps = typeof(ShoppingFilters).GetProperties();
                    string text = GetResourceValue<StoreReplies>(nameof(ThereAreNo), locale);
                    text += " " + filtersProps.FirstOrDefault(p => p.Name == filterNamesRemoved.FirstOrDefault(f => f == nameof(ShoppingFilters.Filters.Color)))?.GetValue(filters);
                    text = text.TrimEnd();
                    text += " " + filtersProps.FirstOrDefault(p => p.Name == filterNamesRemoved.FirstOrDefault(f => f == nameof(ShoppingFilters.Filters.Manufacturer)))?.GetValue(filters);
                    text = text.TrimEnd();
                    text += " " + (filtersProps.FirstOrDefault(p => p.Name == filterNamesRemoved.FirstOrDefault(f => f == nameof(ShoppingFilters.Filters.Type)))?.GetValue(filters)
                        ?? GetResourceValue<StoreReplies>(nameof(Products), locale));
                    if (filterNamesRemoved.Any(f => f == nameof(ShoppingFilters.Filters.Price)))
                        text += " " + GetResourceValue<StoreReplies>(nameof(BetweenPriceRange), locale, filters.MinPrice.ToString(), filters.MaxPrice.ToString());
                    if (filterNamesRemoved.Any(f => f == nameof(ShoppingFilters.Filters.Size)))
                        text += " " + GetResourceValue<StoreReplies>(nameof(ThisSize), locale);
                    text += " " + GetResourceValue<StoreReplies>(nameof(CanBeFound), locale);
                    text += ".";

                    reply.Text = text;
                    await context.PostAsync(reply);

                    reply.Text = GetResourceValue<StoreReplies>(nameof(However), locale);
                    await context.PostAsync(reply);

                    foreach (var filterName in filterNamesRemoved)
                        typeof(ShoppingFilters).GetProperties().First(p => p.Name.Contains(filterName)).SetValue(filters, null);
                    context.PrivateConversationData.SetValue("Filters", filters);
                }
            }
            context.ConversationData.SetValue("SearchResultsNextPage", currentPage + 1);
            context.ConversationData.SetValue("SearchResultsHaveRemainingPages", apparelsRoot.RemainingPages > 0);

            reply.Text = GetResourceValue<StoreReplies>(nameof(ResultsFound), locale);
            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            foreach (var apparel in apparelsRoot?.Apparels)
            {
                reply.Attachments.Add(
                    new HeroCard()
                    {
                        Title = CapitalizeFirstLetter(apparel.Name),
                        Images = new List<CardImage> { new CardImage { Url = apparel.ImageUrl } },
                        Subtitle = apparel.Price + "€",
                        Buttons = new List<CardAction>
                        {
                            new CardAction { Title = "👍", Type = ActionTypes.PostBack, Value = $"__feedback_{apparel.Id}_-2" },
                            new CardAction { Title = "👎", Type = ActionTypes.PostBack, Value = $"__feedback_{apparel.Id}_-1" },
                            new CardAction { Title = GetResourceValue<GeneralReplies>(nameof(DetailsAndBuy), locale), Type = ActionTypes.OpenUrl, Value = apparel.ProductUrl }
                        },
                        Tap = new CardAction { Type = ActionTypes.OpenUrl, Value = apparel.ProductUrl }
                    }.ToAttachment());
            }
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(SeeMore), locale), Type = ActionTypes.PostBack, Value = "__search_results_show_more" },
                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(ChangeFiltersBtn), locale), Type = ActionTypes.PostBack, Value = "__store_shop_filters" },
                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(BackToStoreBtn), locale), Type = ActionTypes.PostBack, Value = "__store_select" }
                }
            };
            if (apparelsRoot.RemainingPages == 0)
                reply.SuggestedActions.Actions.RemoveAt(0);
            if (store == null)
            {
                int qbNum = reply.SuggestedActions.Actions.Count;
                reply.SuggestedActions.Actions.RemoveAt(qbNum - 1);
            }

            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task CustomerServiceAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            Store store = context.PrivateConversationData.GetValue<Store>("StoreSelected");

            reply.Text = GetResourceValue<StoreReplies>(nameof(CustomerService), locale, store.Name);
            await context.PostAsync(reply);

            if (activity.ChannelId == "facebook")
            {
                var customerServiceContents = new FacebookGenericTemplateContent[4]
                {
                    new FacebookGenericTemplateContent()
                    {
                        Title = GetResourceValue<StoreReplies>(nameof(BrandsTitle), locale),
                        Subtitle = GetResourceValue<StoreReplies>(nameof(BrandsSubtitle), locale, store.Name),
                        ImageUrl = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Stores/cs-brands.jpg",
                        Buttons = new[] { new FacebookUrlButton(
                            url: ApiNames.CustomerService + $"?business_id={store.Id}&service_id=1",
                            title: GetResourceValue<StoreReplies>(nameof(BrandsTitle), locale)) }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = GetResourceValue<StoreReplies>(nameof(AboutTitle), locale),
                        Subtitle = GetResourceValue<StoreReplies>(nameof(AboutSubtitle), locale, store.Name),
                        ImageUrl = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Stores/cs-about.jpg",
                        Buttons = new[] { new FacebookUrlButton(
                            url: ApiNames.CustomerService + $"?business_id={store.Id}&service_id=2",
                            title: GetResourceValue<StoreReplies>(nameof(AboutTitle), locale)) }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = GetResourceValue<StoreReplies>(nameof(ReturnsPolicyTitle), locale),
                        Subtitle = GetResourceValue<StoreReplies>(nameof(ReturnsPolicySubtitle), locale, store.Name),
                        ImageUrl = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Stores/cs-returns.jpg",
                        Buttons = new[] { new FacebookUrlButton(
                            url: ApiNames.CustomerService + $"?business_id={store.Id}&service_id=3", 
                            title: GetResourceValue<StoreReplies>(nameof(ReturnsPolicyBtn), locale)) }
                    },
                    new FacebookGenericTemplateContent()
                    {
                        Title = GetResourceValue<StoreReplies>(nameof(ShippingTitle), locale),
                        Subtitle = GetResourceValue<StoreReplies>(nameof(ShippingSubtitle), locale, store.Name),
                        ImageUrl = $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Stores/cs-shipping.jpg",
                        Buttons = new[] { new FacebookUrlButton(
                            url: ApiNames.CustomerService + $"?business_id={store.Id}&service_id=4", 
                            title: GetResourceValue<StoreReplies>(nameof(ShippingTitle), locale)) }
                    }
                };
                for (int i = 0; i < 4; i++)
                    customerServiceContents[i].Tap = new FacebookDefaultAction((customerServiceContents[i].Buttons.First() as FacebookUrlButton).Url);

                var bottomButton = new FacebookPostbackButton(title: GetResourceValue<StoreReplies>(nameof(ContactBtn), locale), payload: "__cs_contact");

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
                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(BackToStoreBtn), locale), Type = ActionTypes.PostBack, Value = "__store_select" },
                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(ShopBtn), locale), Type = ActionTypes.PostBack, Value = "__store_shop" }
                }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task WindowShopAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            Store store = context.PrivateConversationData.GetValue<Store>("StoreSelected");
            string gender = context.UserData.GetValue<string>("Gender");
            context.ConversationData.TryGetValue("WindowShopNextPage", out int currentPage);

            var collectionsApi = new ApiCaller<CollectionsRoot>();
            var apiParams = new Dictionary<string, string>(3)
            {
                { "page", currentPage.ToString() },
                { "gender", (gender == "Female") ? "0" : "1" },
                { "created_by", store.Id.ToString() }
            };
            var collectionsRoot = await collectionsApi.CallAsync(apiParams)
                ?? await collectionsApi.CallAsync(apiParams.ToDictionary(kvp => kvp.Key, kvp => (kvp.Key == "page") ? (currentPage = 0).ToString() : kvp.Value));
            context.ConversationData.SetValue("WindowShopNextPage", currentPage + 1);
            context.ConversationData.SetValue("WindowShopHasRemainingPages", collectionsRoot?.RemainingPages > 0);

            if (collectionsRoot == null)
            {
                reply.Text = GetResourceValue<StoreReplies>(nameof(CollectionsEmpty), locale, CapitalizeFirstLetter(store.Name));
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(BackToStoreBtn), locale), Type = ActionTypes.PostBack, Value = "__store_select" },
                        new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(ShopBtn), locale), Type = ActionTypes.PostBack, Value = "__store_shop" }
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
                        Title = CapitalizeFirstLetter(store.Name) + " " 
                            + GetResourceValue<StoreReplies>(nameof(WindowCollectionTitle), locale, GetResourceValue<GeneralReplies>(gender == "Male" ? nameof(Men) : nameof(Women), locale)),
                        Subtitle = GetResourceValue<StoreReplies>(nameof(WindowCollectionSubtitle), locale),
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
                            Title = CapitalizeFirstLetter(collection.Title),
                            Subtitle = $"By {store.Name}",
                            Buttons = new[] { new FacebookPostbackButton(
                                title: GetResourceValue<StoreReplies>(nameof(ViewItemsBtn), locale),
                                payload: $"__view_collection_{collection.Id}") },
                            ImageUrl = collection.ImageUrl ??
                                $"{ConfigurationManager.AppSettings["BotServerUrl"]}/Files/Images/Occasions/Outdoor/{gender}/{i + 1}.jpg"
                        });
                }

                FacebookPostbackButton bottomPageButton = null;
                if (collectionsRoot.RemainingPages > 0)
                    bottomPageButton = new FacebookPostbackButton(title: GetResourceValue<GeneralReplies>(nameof(SeeMore), locale), payload: "__more_collections");

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
                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(BackToStoreBtn), locale), Type = ActionTypes.PostBack, Value = "__store_select" }
                }
            };

            await context.PostAsync(reply);
            context.Wait(MessageReceivedAsync);
        }

        private async Task ApparelsForCollectionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            string gender = context.UserData.GetValue<string>("Gender");
            string collectionId = null;
            if (activity.Text.StartsWith("__view_collection_"))
                collectionId = activity.Text.Remove(0, "__view_collection_".Length);
            if (!string.IsNullOrWhiteSpace(collectionId))
                context.PrivateConversationData.SetValue("LastStoreCollectionViewed", collectionId);
            else if (!context.PrivateConversationData.TryGetValue("LastStoreCollectionViewed", out collectionId))
                await this.WindowShopAsync(context, result);

            var collectionApparelsApi = new ApiCaller<CollectionApparelsRoot>();
            var collectionApparelsRoot = await collectionApparelsApi.CallAsync(new Dictionary<string, string>(1) { { "collection_id", collectionId } });

            reply.Text = GetResourceValue<StoreReplies>(nameof(HereYouAre), locale);
            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            foreach (var apparel in collectionApparelsRoot.Items)
            {
                reply.Attachments.Add(
                    new HeroCard()
                    {
                        Title = CapitalizeFirstLetter(apparel.Name),
                        Subtitle = apparel.PriceString + "€",
                        Images = new List<CardImage> { new CardImage { Url = apparel.ImageUrl } },
                        Buttons = new List<CardAction>
                        {
                            new CardAction { Title = "👍", Type = ActionTypes.PostBack, Value = $"__feedback_{apparel.Id}_-2" },
                            new CardAction { Title = "👎", Type = ActionTypes.PostBack, Value = $"__feedback_{apparel.Id}_-1" },
                            new CardAction { Title = GetResourceValue<GeneralReplies>(nameof(DetailsAndBuy), locale), Type = ActionTypes.OpenUrl, Value = apparel.Link },
                        },
                        Tap = new CardAction { Type = ActionTypes.OpenUrl, Value = apparel.Link }
                    }.ToAttachment());
            }

            reply.Attachments.Add(
                new HeroCard()
                {
                    Title = GetResourceValue<GeneralReplies>(nameof(FeedbackQ), locale),
                    Images = new List<CardImage> { new CardImage { Url = "http://zoie.io/images/Brand-icon.png" } },
                    Buttons = new List<CardAction>
                    {
                         new CardAction { Title = GetResourceValue<GeneralReplies>(nameof(FeedbackA3), locale), Type = ActionTypes.PostBack, Value = $"__feedback_{collectionId}_3"},
                         new CardAction { Title = GetResourceValue<GeneralReplies>(nameof(FeedbackA2), locale), Type = ActionTypes.PostBack, Value = $"__feedback_{collectionId}_2"},
                         new CardAction { Title = GetResourceValue<GeneralReplies>(nameof(FeedbackA1), locale), Type = ActionTypes.PostBack, Value = $"__feedback_{collectionId}_1"},
                    }
                }.ToAttachment());

            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(SeeMore), locale), Type = ActionTypes.PostBack, Value = "__more_collections" },
                    new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(BackToStoreBtn), locale), Type = ActionTypes.PostBack, Value = "__store_select" }
                }
            };
            if (!context.ConversationData.GetValue<bool>("WindowShopHasRemainingPages"))
                reply.SuggestedActions.Actions.RemoveAt(0);

            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task ContactAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            context.PrivateConversationData.SetValue("LastStoreSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            reply.Text = GetResourceValue<StoreReplies>(nameof(Contact), locale);
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(BackToStoreBtn), locale), Type = ActionTypes.PostBack, Value = "__store_select" },
                        new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(ShopBtn), locale), Type = ActionTypes.PostBack, Value = "__store_shop" }
                    }
            };
            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task FeedbackAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var reply = activity.CreateReply();
            //context.PrivateConversationData.SetValue("LastStoreSubdialog", GetActualAsyncMethodName());
            context.UserData.TryGetValue("Locale", out string locale);

            string[] feedbackData = activity.Text.Remove(0, "__feedback_".Length).Split(new char[1] { '_' }, StringSplitOptions.RemoveEmptyEntries);

            int id = int.Parse(feedbackData[0]);
            int rate = int.Parse(feedbackData[1]);
            if (rate > 0)
                ;   //TODO: Feedback for collection
            else
                ;    //TODO: Feedback for apparel

            reply.Text = GetResourceValue<GeneralReplies>(nameof(FeedbackThanks), locale);

            string lastSubdialog = context.PrivateConversationData.GetValue<string>("LastStoreSubdialog");
            if (lastSubdialog == nameof(this.ApparelsForCollectionAsync))
            {
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(SeeMore), locale), Type = ActionTypes.PostBack, Value = "__more_collections" },
                            new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(BackToStoreBtn), locale), Type = ActionTypes.PostBack, Value = "__store_select" }
                        }
                };
                if (!context.ConversationData.GetValue<bool>("WindowShopHasRemainingPages"))
                    reply.SuggestedActions.Actions.RemoveAt(0);
            }
            else if (lastSubdialog == nameof(this.ShowSearchResultsAsync))
            {
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = GetResourceValue<GeneralReplies>(nameof(SeeMore), locale), Type = ActionTypes.PostBack, Value = "__search_results_show_more" },
                        new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(ChangeFiltersBtn), locale), Type = ActionTypes.PostBack, Value = "__store_shop_filters" },
                        new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(BackToStoreBtn), locale), Type = ActionTypes.PostBack, Value = "__store_select" }
                    }
                };
                if (!context.ConversationData.GetValue<bool>("SearchResultsHaveRemainingPages"))
                    reply.SuggestedActions.Actions.RemoveAt(0);
                if (!context.PrivateConversationData.TryGetValue("StoreSelected", out Store store))
                {
                    int qbNum = reply.SuggestedActions.Actions.Count;
                    reply.SuggestedActions.Actions.RemoveAt(qbNum - 1);
                }
            }
            else
            {
                reply.SuggestedActions = new SuggestedActions()
                {
                    Actions = new List<CardAction>()
                    {
                        new CardAction(){ Title = GetResourceValue<StoreReplies>(nameof(BackToStoreBtn), locale), Type = ActionTypes.PostBack, Value = "__store_select" }
                    }
                };
            }

            await context.PostAsync(reply);

            context.Wait(MessageReceivedAsync);
        }

        private async Task UnimplementedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            context.UserData.TryGetValue("Locale", out string locale);

            await context.PostAsync(GetResourceValue<GeneralReplies>(nameof(Unimplemented), locale, activity.ChannelId));

            context.Done(activity);
        }

        internal class ShoppingFilters
        {
            public enum Filters
            {
                Gender, Size, Price, Type, Color, Manufacturer,
                Invalid = -1
            }

            public float? MaxPrice { get; set; }
            public float? MinPrice { get; set; }
            public string Type { get; set; }
            public string Size { get; set; }
            public string Color { get; set; }
            public string Manufacturer { get; set; }
            public string Gender { get; set; }

            public bool EmptyFilters()
            {
                bool isEmpty = true;
                foreach (var property in typeof(ShoppingFilters).GetProperties())
                    isEmpty &= property.GetValue(this) == null;
                return isEmpty;
            }
        }
    }
}