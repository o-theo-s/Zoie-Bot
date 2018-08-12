using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Zoie.Helpers;

namespace Zoie.Ineffable
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));

            if (activity.GetActivityType() == ActivityTypes.Message)
            {
                Activity typingReply = activity.CreateReply();
                typingReply.Type = ActivityTypes.Typing;
                await connector.Conversations.ReplyToActivityAsync(typingReply);

                if (activity.ChannelId == "facebook")
                {
                    var channelData = ChannelsHelper.Facebook.GetChannelData(activity.ChannelData.ToString());

                    if (channelData.message?.quick_reply != null || channelData.postback?.payload != null)
                    {
                        string payload = channelData.message?.quick_reply?.payload ?? channelData.postback.payload;
                        activity.Text = payload;
                    }
                }

                await Conversation.SendAsync(activity, () => new Dialogs.RootDialog());
            }
            else
            {
                await HandleSystemMessageAsync(activity, connector);
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        private async Task HandleSystemMessageAsync(Activity activity, ConnectorClient connector)
        {
            var reply = activity.CreateReply();

            switch (activity.GetActivityType())
            {
                case ActivityTypes.DeleteUserData:
                    await DialogsHelper.DeleteConversationAndUserDataAsync(activity);
                    reply.Text = "Allright! I no longer have any of your personal information.";
                    await connector.Conversations.ReplyToActivityAsync(reply);
                    return;
                case ActivityTypes.ConversationUpdate:
                    // Handle conversation state changes, like members being added and removed
                    // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                    // Not available in all channels
                    return;
                case ActivityTypes.ContactRelationUpdate:
                    // Handle add/remove from contact lists
                    // Activity.From + Activity.Action represent what happened
                    return;
                case ActivityTypes.Typing:
                    // Handle knowing that the user is typing
                    return;
                case ActivityTypes.Ping:
                    return;
            }
        }
    }
}