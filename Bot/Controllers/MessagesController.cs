using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Zoie.Bot.Dialogs;
using System;
using System.Linq;
using Zoie.Helpers;
using Zoie.Bot.Dialogs.Main;
using Zoie.Bot.Models;
using Newtonsoft.Json;

namespace Zoie.Bot
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));

            if (activity.Type == ActivityTypes.Message)
            {
                Activity typingReply = activity.CreateReply();
                typingReply.Type = ActivityTypes.Typing;
                await connector.Conversations.ReplyToActivityAsync(typingReply);

                if (activity.ChannelId == "facebook")
                {
                    var channelData = ChannelsHelper.Facebook.GetChannelData(activity.ChannelData.ToString());
                    if (channelData.referral != null || channelData.postback?.referral != null)
                    {
                        string fbRef = GeneralHelper.Dehashify( channelData.referral?.@ref ?? channelData.postback.referral.@ref );
                        await DialogsHelper.ResetConversationAsync(activity);

                        string[] refData = fbRef.Split(new string[1] { "__" }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();

                        try
                        {
                            Referral referral = new Referral()
                            {
                                SharedFrom = refData[0],
                                Type = refData[1],
                                Item = refData[2]
                            };

                            await DialogsHelper.SetValueInPrivateConversationDataAsync(activity, "Referral", referral);
                        }
                        catch (IncorrectReferralTypeException) { }
                    }
                    else if (channelData.message?.quick_reply != null || channelData.postback?.payload != null)
                    {
                        string payload = channelData.message?.quick_reply?.payload ?? channelData.postback.payload;
                        activity.Text = payload;
                    }
                }

                if (activity.Text == null /*&& there are attachments*/)
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