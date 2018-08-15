using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System;
using System.Linq;
using Zoie.Helpers;
using Zoie.Petrichor.Dialogs.Main;
using Zoie.Petrichor.Models;

#pragma warning disable VSTHRD200

namespace Zoie.Petrichor
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

                    if (activity.Text?.StartsWith("__menu_new") ?? false)
                    {
                        await DialogsHelper.ResetConversationAsync(activity, deletePrivateConversationData: true);
                        if (activity.Text == "__menu_new_store" || activity.Text == "__menu_new_shop_by_filters")
                            await DialogsHelper.SetValueInPrivateConversationDataAsync(activity, "MenuNew", true);
                        //await connector.Conversations.ReplyToActivityAsync(activity.CreateReply("Hope you liked what I showed you! ☺"));
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
            var reply = activity.CreateReply();

            switch (activity.GetActivityType())
            {
                case ActivityTypes.DeleteUserData:
                    await DialogsHelper.DeleteConversationAndUserDataAsync(activity);
                    reply.Text = "Allright! I no longer have any of your personal information.";
                    await connector.Conversations.ReplyToActivityAsync(reply);
                    return;
                case ActivityTypes.ConversationUpdate:
                    if (activity.MembersAdded?.FirstOrDefault()?.Id?.StartsWith("ZoieBot") ?? false)
                    {
                        reply.Text = "Hi there!";
                        await connector.Conversations.ReplyToActivityAsync(reply);
                    }
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