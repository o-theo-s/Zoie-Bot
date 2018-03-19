using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using ZoieBot.Helpers;
using ZoieBot.Models.SearchAPI;

namespace ZoieBot.Dialogs
{
    [Serializable]
    public class ShoppingDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            context.ConversationData.Clear();

            if (activity.Text == "__menu_top_products__")
            {
                activity.Text = "__top_products";
                await ContinueWithSearchTypeSelectionAsync(context, Awaitable.FromItem(activity));
                return;
            }
            else if (activity.Text.StartsWith("__forward_luis__"))
            {
                activity.Text = activity.Text.Split(new string[] { "__" }, StringSplitOptions.RemoveEmptyEntries).Last();
                await context.Forward(new AiLuisDialog(), ContinueAfterLuisDialogAsync, activity);
                return;
            }
            else if (!activity.Text.StartsWith("__"))
            {
                await context.Forward(new AiLuisDialog(), ContinueAfterLuisDialogAsync, activity);
                return;
            }


            replyMessage.Text = "Select one of the following or type whatever apparel you are looking for";
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

            context.Wait(ContinueWithSearchTypeSelectionAsync);
        }

        private async Task ContinueWithSearchTypeSelectionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            if (activity.Text.StartsWith("__"))
            {
                int page = 0;
                switch (activity.Text)
                {
                    case "__store":
                        replyMessage.Text = "Which store are you interested in?";
                        context.ConversationData.TryGetValue("StorePage", out page);
                        replyMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;

                        var stores = Dummies.GetStores();
                        for (int i = 0; i < 9 && page * 9 + i < stores.Count; i++)
                        {
                            var store = stores.ElementAt(page * 9 + i);

                            List<CardImage> cardImages = new List<CardImage> { new CardImage(url: store.ImageUrl, alt: "Image not available") };

                            List<CardAction> cardButtons;
                            if (store.Name == "All Zoie Stores")
                            {
                                cardButtons = new List<CardAction>
                                {
                                    new CardAction(title: "Select", type: "postBack", value: "__store_any")
                                };
                            }
                            else
                            {
                                cardButtons = new List<CardAction>
                                {
                                    new CardAction(title: "Select", type: "postBack", value: "__store_" + store.Name.Split(' ').First().ToLower()),
                                    new CardAction(title: "Visit online", type: "openUrl", value: store.Url)
                                };
                            }

                            Attachment carouselAttachment = new HeroCard()
                            {
                                Title = store.Name,
                                Images = cardImages,
                                Buttons = cardButtons,
                                Tap = cardButtons.First()
                            }.ToAttachment();

                            replyMessage.Attachments.Add(carouselAttachment);
                        }

                        if ((++page) * 9 >= stores.Count)
                            page = 0;
                        context.ConversationData.SetValue("StorePage", page);

                        if (stores.Count > 9)
                        {
                            replyMessage.SuggestedActions = new SuggestedActions()
                            {
                                Actions = new List<CardAction>()
                                {
                                    new CardAction(){ Title = "See more", Type = ActionTypes.PostBack, Value = "__store_page_next"},
                                    new CardAction(){ Title = "First page", Type = ActionTypes.PostBack, Value = "__store_page_first"},
                                    new CardAction(){ Title = "Last page", Type = ActionTypes.PostBack, Value = "__store_page_last"}
                                }
                            };
                        }

                        await context.PostAsync(replyMessage);
                        context.Wait(ContinueWithFilterAdditionAsync);
                        return;
                    case "__top_products":
                        replyMessage.Text = "Here are the top products:";
                        context.ConversationData.TryGetValue("TopProductsPage", out page);
                        replyMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;

                        var topProducts = await Dummies.GetTopProductsAsync();
                        for (int i = 0; i < 9 && page * 9 + i < topProducts.Count; i++)
                        {
                            var topProduct = topProducts.ElementAt(page * 9 + i);

                            List<CardImage> cardImages = new List<CardImage> { new CardImage(url: topProduct.ImageUrl, alt: "Image not available") };

                            List<CardAction> cardButtons = new List<CardAction>
                            {
                                new CardAction(title: "Buy", type: "openUrl", value: topProduct.ProductUrl),
                                new CardAction(title: "Add to wishlist", type: "postBack", value: "__wishlist_add_top_" + topProduct.Id)
                            };

                            Attachment carouselAttachment = new HeroCard()
                            {
                                Title = topProduct.Name,
                                Subtitle = topProduct.Price + "€",
                                Images = cardImages,
                                Buttons = cardButtons,
                                Tap = cardButtons.First()
                            }.ToAttachment();

                            replyMessage.Attachments.Add(carouselAttachment);
                        }

                        if (++page * 9 >= topProducts.Count)
                            page = 0;
                        context.ConversationData.SetValue("TopProductsPage", page);

                        replyMessage.SuggestedActions = new SuggestedActions()
                        {
                            Actions = new List<CardAction>()
                            {
                                new CardAction(){ Title = "See more", Type = ActionTypes.PostBack, Value = "__top_products_page_next"},
                                new CardAction(){ Title = "First page", Type = ActionTypes.PostBack, Value = "__top_products_page_first"},
                                new CardAction(){ Title = "Last page", Type = ActionTypes.PostBack, Value = "__top_products_page_last"}
                            }
                        };

                        await context.PostAsync(replyMessage);
                        context.Wait(ContinueAfterSearchResultsAsync);
                        return;
                    case "__categories":
                        replyMessage.Text = "Select a category or type what you have in mind";
                        replyMessage.SuggestedActions = new SuggestedActions()
                        {
                            Actions = new List<CardAction>()
                            {
                                new CardAction(){ Title = "Shoes", Type = ActionTypes.PostBack, Value = "__category_shoes"},
                                new CardAction(){ Title = "Shirts", Type = ActionTypes.PostBack, Value = "__category_shirts"},
                                new CardAction(){ Title = "Pants", Type = ActionTypes.PostBack, Value = "__category_pants"}
                            }
                        };

                        await context.PostAsync(replyMessage);
                        context.Wait(ContinueWithFilterAdditionAsync);
                        return;
                }
            }
            else
            {
                await context.Forward(new AiLuisDialog(), ContinueAfterLuisDialogAsync, activity);
            }

            return;
        }

        private async Task ContinueWithFilterAdditionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            if (activity.Text.StartsWith("__"))
            {
                if (activity.Text.Contains("category"))
                {
                    if (activity.Text.EndsWith("any"))
                        context.ConversationData.RemoveValue("TypeFilter");
                    else
                        context.ConversationData.SetValue("TypeFilter", activity.Text.Split('_').Last());
                }
                else if (activity.Text.Contains("store"))
                {
                    if (activity.Text.EndsWith("any"))
                        context.PrivateConversationData.RemoveValue("StoreFilter");
                    else
                        switch (activity.Text)
                        {
                            case "__store_page_next":
                                activity.Text = "__store";
                                await ContinueWithSearchTypeSelectionAsync(context, Awaitable.FromItem(activity));
                                return;
                            case "__store_page_first":
                                context.ConversationData.SetValue("StorePage", 0);
                                activity.Text = "__store";
                                await ContinueWithSearchTypeSelectionAsync(context, Awaitable.FromItem(activity));
                                return;
                            case "__store_page_last":
                                context.ConversationData.SetValue("StorePage", Dummies.GetStores().Count / 9);
                                activity.Text = "__store";
                                await ContinueWithSearchTypeSelectionAsync(context, Awaitable.FromItem(activity));
                                return;
                            default:
                                string tempStore = activity.Text.Split('_').Last();
                                context.PrivateConversationData.SetValue("StoreFilter", tempStore);

                                replyMessage.Text = $"If you want to exit {tempStore} store at any time type 'Exit store' or 'Exit {tempStore} store' or just 'Exit'.";
                                await context.PostAsync(replyMessage);
                                break;
                        }
                }
                else if (activity.Text.Contains("color"))
                {
                    if (activity.Text.EndsWith("any"))
                        context.ConversationData.RemoveValue("ColorFilter");
                    else
                        context.ConversationData.SetValue("ColorFilter", activity.Text.Split('_').Last());
                }
                else if (activity.Text.Contains("brand"))
                {
                    if (activity.Text.EndsWith("any"))
                        context.ConversationData.RemoveValue("ManufacturerFilter");
                    else
                        context.ConversationData.SetValue("ManufacturerFilter", activity.Text.Split('_').Last());
                }
                else if (activity.Text.Contains("price"))
                {
                    if (activity.Text.EndsWith("any"))
                    {
                        context.ConversationData.RemoveValue("MaxPriceFilter");
                        context.ConversationData.RemoveValue("MinPriceFilter");
                    }
                    else
                    {
                        string[] keywords = activity.Text.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
                        foreach (var keyword in keywords)
                        {
                            if (keyword.StartsWith("max"))
                                context.ConversationData.SetValue("MaxPriceFilter", keyword.Substring(3));
                            else if (keyword.StartsWith("min"))
                                context.ConversationData.SetValue("MinPriceFilter", keyword.Substring(3));
                        }
                    }
                }
                else if (activity.Text.Contains("size"))
                {
                    if (activity.Text.EndsWith("any"))
                        context.ConversationData.RemoveValue("SizeFilter");
                    else
                        context.ConversationData.SetValue("SizeFilter", activity.Text.Split('_').Last());
                }
                else if (activity.Text.Contains("gender"))
                {
                    if (activity.Text.EndsWith("any"))
                        context.ConversationData.SetValue("GenderFilter", string.Empty);
                    else
                        context.ConversationData.SetValue("GenderFilter", activity.Text.Split('_').Last() == "girl" ? "women" : "men");
                }
            }
            else
            {
                await context.Forward(new AiLuisDialog(), ContinueAfterLuisDialogAsync, activity);
                return;
            }

            await ContinueWithSearchResultsAsync(context, result);
        }

        private async Task ContinueWithSearchResultsAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply("");
            replyMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            List<Product> products = new List<Product>();

            string[] filters = new string[8];
            context.PrivateConversationData.TryGetValue("StoreFilter", out filters[0]);
            context.ConversationData.TryGetValue("ManufacturerFilter", out filters[1]);
            context.ConversationData.TryGetValue("GenderFilter", out filters[2]);
            context.ConversationData.TryGetValue("TypeFilter", out filters[3]);
            context.ConversationData.TryGetValue("ColorFilter", out filters[4]);
            context.ConversationData.TryGetValue("MinPriceFilter", out filters[5]);
            context.ConversationData.TryGetValue("MaxPriceFilter", out filters[6]);
            context.ConversationData.TryGetValue("SizeFilter", out filters[7]);

            StringBuilder requestParameters = new StringBuilder("?");
            requestParameters.Append($"shop={filters[0]}&");
            requestParameters.Append($"manufacturer={filters[1]}&");
            requestParameters.Append($"gender={filters[2] ?? context.UserData.GetValue<string>("Gender").Replace('a', 'e')}&");
            requestParameters.Append($"type={filters[3]}&");
            requestParameters.Append($"color={filters[4]}&");
            requestParameters.Append($"min_price={filters[5]}&");
            requestParameters.Append($"max_price={filters[6]}&");
            requestParameters.Append($"size={filters[7]}");

            StringBuilder searchMessage = new StringBuilder("");
            context.ConversationData.TryGetValue("ResultsPage", out int resultsPage);
            if (resultsPage == 0)
                searchMessage.Append("Searching for");
            else
                searchMessage.Append("More results for");
            DialogsHelper.ShoppingDialogHelper.SearchMessageBuilder(ref searchMessage, filters[2] ?? context.UserData.GetValue<string>("Gender").Replace('a', 'e'));
            DialogsHelper.ShoppingDialogHelper.SearchMessageBuilder(ref searchMessage, filters[4]);
            DialogsHelper.ShoppingDialogHelper.SearchMessageBuilder(ref searchMessage, filters[1]);
            DialogsHelper.ShoppingDialogHelper.SearchMessageBuilder(ref searchMessage, filters[3] ?? "apparels");
            DialogsHelper.ShoppingDialogHelper.SearchMessageBuilder(ref searchMessage, filters[7], startWith: " of size");
            DialogsHelper.ShoppingDialogHelper.SearchMessageBuilder(ref searchMessage, filters[0], startWith: " in", endWith: " store");
            DialogsHelper.ShoppingDialogHelper.SearchMessageBuilder(ref searchMessage, filters[5], startWith: " more than", endWith: "€");
            DialogsHelper.ShoppingDialogHelper.SearchMessageBuilder(ref searchMessage, filters[6], startWith: " and less than", endWith: "€");
            searchMessage.Append("...");

            replyMessage.Text = searchMessage.ToString();
            await context.PostAsync(replyMessage);
            replyMessage.Type = ActivityTypes.Typing;
            await context.PostAsync(replyMessage);
            replyMessage.Type = ActivityTypes.Message;

            HttpClient client = new HttpClient { BaseAddress = new Uri("http://zoie.io/API/products-results.php") };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = await client.GetAsync(requestParameters.ToString());
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(json))
                {
                    List<Product> allProducts = JsonConvert.DeserializeObject<RootObject>(json).Products;

                    //Antidummy
                    int rand = new Random().Next(0, allProducts.Count < 9 ? allProducts.Count : allProducts.Count - 9);
                    products = allProducts.GetRange(rand, allProducts.Count < 9 ? allProducts.Count : 9);

                    //Dummy
                    /*int tries = 0;
                    do
                    {
                        int rand = new Random().Next(0, allProducts.Count < 9 ? allProducts.Count : allProducts.Count - 9);
                        products = allProducts.GetRange(rand, allProducts.Count < 9 ? allProducts.Count : 9);
                        for (int i = 0; i < products.Count; i++)
                        {
                            if (!await Dummies.CheckProductUrlsAsync(products[i]))
                            {
                                products.RemoveAt(i);
                                i--;
                            }
                        }
                        tries++;
                    } while (products.Count < 2 && allProducts.Count >= 2 && tries < 2);*/
                }
                context.ConversationData.SetValue("TotalProducts", products.Count);

                if (products.Count == 0)
                {
                    replyMessage.Text = "There were no products for your search.";
                    replyMessage.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = "New search", Type = ActionTypes.PostBack, Value = "__reset"}
                        }
                    };
                    await context.PostAsync(replyMessage);
                    context.Wait(ContinueAfterSearchResultsAsync);
                    return;
                }
            }
            else
            {
                replyMessage.Text = "An error occured while searching for requested products. Please try again with a new search.";
                await context.PostAsync(replyMessage);

                activity.Text = "__";
                await MessageReceivedAsync(context, Awaitable.FromItem(activity));
                return;
            }

            //Dummy - Να σβήσω το resultsPage = 0
            //resultsPage = 0;
            for (int i = 0; i < 9 && resultsPage * 9 + i < products.Count; i++)
            {
                var product = products[resultsPage * 9 + i];

                List<CardImage> cardImages = new List<CardImage> { new CardImage(url: product.ImageUrl, alt: "Image not available") };

                List<CardAction> cardButtons = new List<CardAction>
                {
                    new CardAction(title: "Buy", type: "openUrl", value: product.ProductUrl),
                    new CardAction(title: "Add to wishlist", type: "postBack", value: "__wishlist_add_" + product.Id)
                };

                Attachment carouselAttachment = new HeroCard()
                {
                    Title = product.Name,
                    Subtitle = product.Price + "€",
                    Images = cardImages,
                    Buttons = cardButtons,
                    Tap = cardButtons.First()
                }.ToAttachment();

                replyMessage.Attachments.Add(carouselAttachment);
            }

            //Dummy - Να σβήσω το && false
            if (++resultsPage * 9 >= products.Count)
                resultsPage = 0;
            context.ConversationData.SetValue("ResultsPage", resultsPage);

            replyMessage.Text = "Here's what I found:";
            replyMessage.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>(5)
                {
                    new CardAction(){ Title = "Add filter", Type = ActionTypes.PostBack, Value = "__results_add_filter"},
                    new CardAction(){ Title = "New search", Type = ActionTypes.PostBack, Value = "__reset"}
                }
            };

            //Dummy - Να φύγει το true || 
            if (products.Count > 9)
            {
                (replyMessage.SuggestedActions.Actions as List<CardAction>).AddRange(
                    new List<CardAction>
                    {
                        new CardAction(){ Title = "See more", Type = ActionTypes.PostBack, Value = "__results_page_next" },
                        new CardAction(){ Title = "First page", Type = ActionTypes.PostBack, Value = "__results_page_first"},
                        new CardAction(){ Title = "Last page", Type = ActionTypes.PostBack, Value = "__results_page_last"}
                    }
                );
            }

            await context.PostAsync(replyMessage);
            context.Wait(ContinueAfterSearchResultsAsync);
        }

        private async Task ContinueAfterSearchResultsAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            switch (activity.Text)
            {
                case "__results_page_next":
                    await ContinueWithSearchResultsAsync(context, result);
                    return;
                case "__results_page_first":
                    context.ConversationData.SetValue("ResultsPage", 0);
                    await ContinueWithSearchResultsAsync(context, result);
                    return;
                case "__results_page_last":
                    //Dummy - Να φύγει το σχόλιο
                    context.ConversationData.SetValue("ResultsPage", context.ConversationData.GetValue<int>("TotalProducts") / 9);
                    await ContinueWithSearchResultsAsync(context, result);
                    return;
                case "__results_add_filter":
                    replyMessage.Text = "Make your search more specific with one of the filters following:";
                    replyMessage.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = "Category", Type = ActionTypes.PostBack, Value = "__filter_category"},
                            new CardAction(){ Title = "Brand", Type = ActionTypes.PostBack, Value = "__filter_brand"},
                            new CardAction(){ Title = "Color", Type = ActionTypes.PostBack, Value = "__filter_color"},
                            new CardAction(){ Title = "Price range", Type = ActionTypes.PostBack, Value = "__filter_price"},
                            new CardAction(){ Title = "Size", Type = ActionTypes.PostBack, Value = "__filter_size"},
                            new CardAction(){ Title = "Gender", Type = ActionTypes.PostBack, Value = "__filter_gender"},
                            new CardAction(){ Title = "Store", Type = ActionTypes.PostBack, Value = "__filter_store"}
                        }
                    };

                    await context.PostAsync(replyMessage);
                    context.Wait(ContinueWithFilterSelectionAsync);
                    return;
                case "__reset":
                    context.ConversationData.Clear();
                    await MessageReceivedAsync(context, result);
                    return;
                case "__top_products_page_next":
                    activity.Text = "__top_products";
                    await ContinueWithSearchTypeSelectionAsync(context, Awaitable.FromItem(activity));
                    return;
                case "__top_products_page_first":
                    activity.Text = "__top_products";
                    context.ConversationData.SetValue("TopProductsPage", 0);
                    await ContinueWithSearchTypeSelectionAsync(context, Awaitable.FromItem(activity));
                    return;
                case "__top_products_page_last":
                    activity.Text = "__top_products";
                    context.ConversationData.SetValue("TopProductsPage", (await Dummies.GetTopProductsAsync()).Count / 9);
                    await ContinueWithSearchTypeSelectionAsync(context, Awaitable.FromItem(activity));
                    return;
                default:
                    if (activity.Text.StartsWith("__wishlist_add_"))
                    {
                        if (!context.PrivateConversationData.TryGetValue("Wishlist", out List<string> wishes))
                            wishes = new List<string>() { activity.Text.Split('_').Last() };
                        else
                            wishes.Add(activity.Text.Split('_').Last());

                        context.PrivateConversationData.SetValue("Wishlist", wishes);

                        replyMessage.Text = "Product added to wishlist successfully.";
                        if (activity.Text.Contains("top"))
                        {
                            replyMessage.SuggestedActions = new SuggestedActions()
                            {
                                Actions = new List<CardAction>()
                                {
                                    new CardAction(){ Title = "See more", Type = ActionTypes.PostBack, Value = "__top_products_page_next"},
                                    new CardAction(){ Title = "First page", Type = ActionTypes.PostBack, Value = "__top_products_page_first"},
                                    new CardAction(){ Title = "Last page", Type = ActionTypes.PostBack, Value = "__top_products_page_last"}
                                }
                            };
                        }
                        else
                        {
                            replyMessage.SuggestedActions = new SuggestedActions()
                            {
                                Actions = new List<CardAction>()
                                {
                                    new CardAction(){ Title = "See more", Type = ActionTypes.PostBack, Value = "__results_page_next"},
                                    new CardAction(){ Title = "Add filter", Type = ActionTypes.PostBack, Value = "__results_add_filter"},
                                    new CardAction(){ Title = "First page", Type = ActionTypes.PostBack, Value = "__results_page_first"},
                                    new CardAction(){ Title = "Last page", Type = ActionTypes.PostBack, Value = "__results_page_last"},
                                    new CardAction(){ Title = "New search", Type = ActionTypes.PostBack, Value = "__reset"}
                                }
                            };
                        }

                        await context.PostAsync(replyMessage);
                        context.Wait(ContinueAfterSearchResultsAsync);
                    }
                    else
                    {
                        context.ConversationData.Clear();

                        await context.Forward(new AiLuisDialog(), ContinueAfterLuisDialogAsync, activity);
                    }
                    return;
            }
        }

        private async Task ContinueWithFilterSelectionAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var replyMessage = activity.CreateReply();

            switch (activity.Text)
            {
                case "__filter_color":
                    replyMessage.Text = "What color are you looking for? (Select one or type another)";
                    replyMessage.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = "Any", Type = ActionTypes.PostBack, Value = "__color_any"},
                            new CardAction(){ Title = "Blue", Type = ActionTypes.PostBack, Value = "__color_blue"},
                            new CardAction(){ Title = "Red", Type = ActionTypes.PostBack, Value = "__color_red"},
                            new CardAction(){ Title = "Green", Type = ActionTypes.PostBack, Value = "__color_green"},
                            new CardAction(){ Title = "White", Type = ActionTypes.PostBack, Value = "__color_white"},
                            new CardAction(){ Title = "Black", Type = ActionTypes.PostBack, Value = "__color_black"},
                            new CardAction(){ Title = "Yellow", Type = ActionTypes.PostBack, Value = "__color_yellow"},
                            new CardAction(){ Title = "Purple", Type = ActionTypes.PostBack, Value = "__color_purple"}
                        }
                    };
                    break;
                case "__filter_category":
                    replyMessage.Text = "What type of apparel are you looking for? (Select one or type another)";
                    replyMessage.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = "Any", Type = ActionTypes.PostBack, Value = "__category_any"},
                            new CardAction(){ Title = "Shoes", Type = ActionTypes.PostBack, Value = "__category_shoes"},
                            new CardAction(){ Title = "Shirts", Type = ActionTypes.PostBack, Value = "__category_shirts"},
                            new CardAction(){ Title = "Pants", Type = ActionTypes.PostBack, Value = "__category_pants"},
                            new CardAction(){ Title = "Shorts", Type = ActionTypes.PostBack, Value = "__category_shorts"}
                        }
                    };
                    break;
                case "__filter_brand":
                    replyMessage.Text = "What brand are you looking for? (Select one or type another)";
                    replyMessage.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = "Any", Type = ActionTypes.PostBack, Value = "__brand_any"},
                            new CardAction(){ Title = "Nike", Type = ActionTypes.PostBack, Value = "__brand_nike"},
                            new CardAction(){ Title = "Lacoste", Type = ActionTypes.PostBack, Value = "__brand_lacoste"},
                            new CardAction(){ Title = "Addidas", Type = ActionTypes.PostBack, Value = "__brand_addidas"},
                            new CardAction(){ Title = "Puma", Type = ActionTypes.PostBack, Value = "__brand_puma"},
                            new CardAction(){ Title = "Levi's", Type = ActionTypes.PostBack, Value = "__brand_levi's"}
                        }
                    };
                    break;
                case "__filter_price":
                    replyMessage.Text = "What price range would you like? (Select one or type another)";
                    replyMessage.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = "Any", Type = ActionTypes.PostBack, Value = "__price_any"},
                            new CardAction(){ Title = "< 50€", Type = ActionTypes.PostBack, Value = "__price_max50"},
                            new CardAction(){ Title = "50 - 100 €", Type = ActionTypes.PostBack, Value = "__price_min50_max100"},
                            new CardAction(){ Title = "< 100€", Type = ActionTypes.PostBack, Value = "__price_max100"},
                            new CardAction(){ Title = "< 200€", Type = ActionTypes.PostBack, Value = "__price_max200"},
                            new CardAction(){ Title = "100 - 200 €", Type = ActionTypes.PostBack, Value = "__price_min100_max200"}
                        }
                    };
                    break;
                case "__filter_size":
                    replyMessage.Text = "What size fits you best? (Select one or type another)";
                    if (context.ConversationData.ContainsKey("TypeFilter") && context.ConversationData.GetValue<string>("TypeFilter").Contains("shoe"))
                    {
                        replyMessage.SuggestedActions = new SuggestedActions()
                        {
                            Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = "Any", Type = ActionTypes.PostBack, Value = "__size_any"},
                            new CardAction(){ Title = "34 - 35", Type = ActionTypes.PostBack, Value = "__size_34"},
                            new CardAction(){ Title = "36 - 37", Type = ActionTypes.PostBack, Value = "__size_36"},
                            new CardAction(){ Title = "38 - 39", Type = ActionTypes.PostBack, Value = "__size_38"},
                            new CardAction(){ Title = "40 - 41", Type = ActionTypes.PostBack, Value = "__size_40"},
                            new CardAction(){ Title = "42 - 44", Type = ActionTypes.PostBack, Value = "__size_42"}
                        }
                        };
                    }
                    else
                    {
                        replyMessage.SuggestedActions = new SuggestedActions()
                        {
                            Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = "Any", Type = ActionTypes.PostBack, Value = "__size_any"},
                            new CardAction(){ Title = "x-small", Type = ActionTypes.PostBack, Value = "__size_x-small"},
                            new CardAction(){ Title = "small", Type = ActionTypes.PostBack, Value = "__size_small"},
                            new CardAction(){ Title = "medium", Type = ActionTypes.PostBack, Value = "__size_medium"},
                            new CardAction(){ Title = "large", Type = ActionTypes.PostBack, Value = "__size_large"},
                            new CardAction(){ Title = "x-large", Type = ActionTypes.PostBack, Value = "__size_x-large"}
                        }
                        };
                    }
                    break;
                case "__filter_gender":
                    replyMessage.Text = "Is that you're looking for, for a boy or a girl?";
                    replyMessage.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>()
                        {
                            new CardAction(){ Title = "Girl", Type = ActionTypes.PostBack, Value = "__gender_girl"},
                            new CardAction(){ Title = "Boy", Type = ActionTypes.PostBack, Value = "__gender_boy"},
                            new CardAction(){ Title = "Unisex", Type = ActionTypes.PostBack, Value = "__gender_any"}
                        }
                    };
                    break;
                case "__filter_store":
                    context.ConversationData.SetValue("StorePage", 0);
                    activity.Text = "__store";
                    await ContinueWithSearchTypeSelectionAsync(context, Awaitable.FromItem(activity));
                    return;
                default:
                    await context.Forward(new AiLuisDialog(), ContinueAfterLuisDialogAsync, activity);
                    return;
            }

            await context.PostAsync(replyMessage);
            context.Wait(ContinueWithFilterAdditionAsync);
        }

        private async Task ContinueAfterLuisDialogAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            switch (activity.Text)
            {
                case "__luis_neutral":
                    context.Wait(ContinueWithSearchTypeSelectionAsync);
                    return;
                case "__luis_shopping":
                    await ContinueWithSearchResultsAsync(context, result);
                    return;
                case "__luis_delete_personal_data":
                    activity.Text = "__menu_options_delete_personal_data__";
                    context.Done(activity);
                    return;
                case "__luis_top_products":
                    activity.Text = "__top_products";
                    await ContinueWithSearchTypeSelectionAsync(context, Awaitable.FromItem(activity));
                    return;
                default:
                case "__luis_goodbye":
                    context.Wait(MessageReceivedAsync);
                    return;
            }
        }
    }
}