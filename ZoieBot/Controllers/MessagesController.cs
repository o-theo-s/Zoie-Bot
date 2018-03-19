using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using ZoieBot.Models;
using ZoieBot.Dialogs;
using ZoieBot.Helpers;
using Newtonsoft.Json;
using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace ZoieBot
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));

            if (activity.Type == ActivityTypes.Message && activity.Text != null)
            {
                Activity typingReply = activity.CreateReply();
                typingReply.Type = ActivityTypes.Typing;
                await connector.Conversations.ReplyToActivityAsync(typingReply);

                if (activity.Text != null)
                {
                    #region Facebook Settings (dynamic)
                    /*dynamic messageData = new JObject();
                    messageData.attachment = new JObject();
                    messageData.attachment.type = "template";
                    messageData.attachment.payload = new JObject();
                    messageData.attachment.payload.template_type = "generic";
                    //messageData.attachment.payload.sharable = "true";
                    messageData.attachment.payload.image_aspect_ratio = "square";


                    messageData.attachment.payload.elements
                        = new JArray(
                            new JObject(
                                new JProperty("title", "hola"),
                                new JProperty("image_url", "https://www.paperinos.gr/images/detailed/81/freaky-nation-dragon-andriko-dermatino-biker-jacket-mpornto-414202__2_.jpg"),
                                new JProperty("subtitle", "Mundo"),
                                new JProperty("buttons",
                                    new JArray(
                                        new JObject(
                                            new JProperty("type", "web_url"),
                                            new JProperty("url", "https://www.crossoverfashion.gr/"),
                                            new JProperty("title", "View")
                                        )
                                    )
                                )
                            )
                        );

                    Activity messageReply = activity.CreateReply();
                    messageReply.ChannelData = messageData;
                    await connector.Conversations.ReplyToActivityAsync(messageReply);
                    return Request.CreateResponse(HttpStatusCode.OK);*/
                    #endregion

                    if (activity.ChannelId == "facebook")
                    {
                        var fbChannelData = JsonConvert.DeserializeObject<FbChannelData>(activity.ChannelData.ToString());
                        string payload = fbChannelData.message?.quick_reply?.payload;
                        if (payload != null)
                            activity.Text = payload;
                    }

                    if (activity.Text.ToLower().Contains("exit"))
                        activity.Text = "__menu_exit_store__";
                    if (activity.Text.StartsWith("__menu"))
                        await DialogsHelper.ResetConversationAsync(activity);
                }
                else
                {
                    //Process Attachments
                }

                await Conversation.SendAsync(activity, () => new RootDialog());
            }
            else
            {
                await HandleSystemMessageAsync(activity, connector);
            }
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        private async Task HandleSystemMessageAsync(Activity activity, ConnectorClient connector)
        {
            var replyMessage = activity.CreateReply();

            if (activity.Type == ActivityTypes.DeleteUserData)
            {
                await DialogsHelper.DeleteConversationAndUserDataAsync(activity);
                replyMessage.Text = "Allright! I no longer have any of your personal information.";
                await connector.Conversations.ReplyToActivityAsync(replyMessage);
            }
            else if (activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (activity.MembersAdded?.FirstOrDefault()?.Id?.StartsWith("ZoieBot") ?? false)
                {
                    replyMessage.Text = "Hi there!";
                    await connector.Conversations.ReplyToActivityAsync(replyMessage);
                }
            }
            else if (activity.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (activity.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (activity.Type == ActivityTypes.Ping)
            {
            }

            
            return;
        }
    }
}