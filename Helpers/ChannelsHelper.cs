using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Zoie.Helpers.Channels.Facebook;

namespace Zoie.Helpers
{
    public static partial class ChannelsHelper
    {
        public static partial class Facebook
        {
            public static Facebook.ChannelData GetChannelData(string channelData)
            {
                Facebook.ChannelData cd = new ChannelData();
                try
                {
                    cd = JsonConvert.DeserializeObject<Facebook.ChannelData>(channelData);
                }
                catch (Exception)
                {
                    cd = null;
                }
                return cd;
            }

            public static async Task<Tuple<string, string>> GetUserDataAsync(string userFbId)
            {
                Tuple<string, string> localeAndGender = null;
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri($"https://graph.facebook.com/v2.6/{userFbId}");
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    HttpResponseMessage response = await client.GetAsync($"?fields=locale,gender&access_token={ConfigurationManager.AppSettings["FacebookPageAccessToken"]}");
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        var fbInfo = JsonConvert.DeserializeObject<FacebookUserInfo>(json);

                        localeAndGender = new Tuple<string, string>(fbInfo.Locale, CultureInfo.CurrentCulture.TextInfo.ToTitleCase(fbInfo.Gender));
                    }
                }

                return localeAndGender;
            }

            public static dynamic AddLocationButton(dynamic channelData)
            {
                if (channelData == null)
                    channelData = new JObject();

                dynamic fbQRButtonLocation = new JObject();
                fbQRButtonLocation.content_type = "location";

                if (channelData.quick_replies != null)
                    (channelData.quick_replies as JArray).Add(fbQRButtonLocation);
                else
                    channelData.quick_replies = new JArray(fbQRButtonLocation);

                return channelData;
            }

            public static dynamic AddQuickReplyButton(dynamic channelData, string title, string payload, string imageUrl = null)
            {
                if (channelData == null)
                    channelData = new JObject();

                dynamic fbQRButtonText = new JObject();
                fbQRButtonText.content_type = "text";

                fbQRButtonText.title = title;
                fbQRButtonText.payload = payload;
                fbQRButtonText.image_url = imageUrl;

                if (channelData.quick_replies != null)
                    (channelData.quick_replies as JArray).Add(fbQRButtonText);
                else
                    channelData.quick_replies = new JArray(fbQRButtonText);

                return channelData;
            }

            public static dynamic AddListTemplate(dynamic channelData)
            {
                if (channelData == null)
                    channelData = new JObject();

                dynamic fbListTemplate = new JObject(
                    new JProperty("type", "template"),
                    new JProperty("payload",
                        new JObject(
                            new JProperty("template_type", "list"),
                            new JProperty("top_element_style", "compact"),
                            new JProperty("elements",
                                new JArray(
                                    new JObject(
                                        new JProperty("title", "Classic T-Shirt Collection"),
                                        new JProperty("subtitle", "See all our colors"),
                                        new JProperty("image_url", "https://peterssendreceiveapp.ngrok.io/img/collection.png"),
                                        new JProperty("buttons",
                                            new JArray(
                                                new JObject(
                                                    new JProperty("title", "View"),
                                                    new JProperty("type", "web_url"),
                                                    new JProperty("url", "https://peterssendreceiveapp.ngrok.io/collection"),
                                                    new JProperty("messenger_extensions", true),
                                                    new JProperty("webview_height_ratio", "tall"),
                                                    new JProperty("fallback_url", "https://peterssendreceiveapp.ngrok.io/")
                                                )
                                            )
                                        )
                                    ),
                                    new JObject(
                                        new JProperty("title", "Classic White T-Shirt"),
                                        new JProperty("subtitle", "See all our colors"),
                                        new JProperty("default_action",
                                            new JObject(
                                                new JProperty("type", "web_url"),
                                                new JProperty("url", "https://peterssendreceiveapp.ngrok.io/view?item=100"),
                                                new JProperty("messenger_extensions", false),
                                                new JProperty("webview_height_ratio", "tall")
                                            )
                                        )
                                    ),
                                    new JObject(
                                        new JProperty("title", "Classic Blue T-Shirt"),
                                        new JProperty("image_url", "https://peterssendreceiveapp.ngrok.io/img/blue-t-shirt.png"),
                                        new JProperty("subtitle", "100 % Cotton, 200 % Comfortable"),
                                        new JProperty("default_action",
                                            new JObject(
                                                new JProperty("type", "web_url"),
                                                new JProperty("url", "https://peterssendreceiveapp.ngrok.io/view?item=101"),
                                                new JProperty("messenger_extensions", true),
                                                new JProperty("webview_height_ratio", "tall"),
                                                new JProperty("fallback_url", "https://peterssendreceiveapp.ngrok.io/")
                                            )
                                        ),
                                        new JProperty("buttons",
                                            new JArray(
                                                new JObject(
                                                    new JProperty("title", "Shop Now"),
                                                    new JProperty("type", "web_url"),
                                                    new JProperty("url", "https://peterssendreceiveapp.ngrok.io/shop?item=101"),
                                                    new JProperty("messenger_extensions", true),
                                                    new JProperty("webview_height_ratio", "tall"),
                                                    new JProperty("fallback_url", "https://peterssendreceiveapp.ngrok.io/")
                                                )
                                            )
                                        )
                                    )
                                )
                            ),
                            new JProperty("buttons",
                                new JArray(
                                    new JObject(
                                        new JProperty("title", "View More"),
                                        new JProperty("type", "postback"),
                                        new JProperty("payload", "payload")
                                    )
                                )
                            )
                        )
                    )
                );

                channelData.attachment = fbListTemplate;

                return channelData;
            }
        }
    }
}