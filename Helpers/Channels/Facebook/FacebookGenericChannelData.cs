using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Zoie.Helpers
{
    public static partial class ChannelsHelper
    {
        public static partial class Facebook
        {
            [Serializable]
            public class ChannelData
            {
                public Sender sender { get; set; }
                public Recipient recipient { get; set; }
                public long timestamp { get; set; }
                public Referral referral { get; set; }
                public Postback postback { get; set; }
                public Message message { get; set; }


                public class Sender
                {
                    public string id { get; set; }
                }

                public class Recipient
                {
                    public string id { get; set; }
                }

                public class Coordinates
                {
                    public double lat { get; set; }
                    public double @long { get; set; }
                }

                public class Payload
                {
                    public Coordinates coordinates { get; set; }
                }

                public class Attachment
                {
                    public string type { get; set; }
                    public Payload payload { get; set; }
                    public string title { get; set; }
                    public string url { get; set; }
                }

                public class QuickReply
                {
                    public string payload { get; set; }
                }

                public class Postback
                {
                    public string payload { get; set; }
                    public Referral referral { get; set; }
                }

                public class Referral
                {
                    public string @ref { get; set; }
                    public string source { get; set; }
                    public string type { get; set; }
                }

                public class Message
                {
                    public string mid { get; set; }
                    public int seq { get; set; }
                    public string text { get; set; }
                    public bool is_echo { get; set; }
                    public QuickReply quick_reply { get; set; }
                    public List<Attachment> attachments { get; set; }
                }
            }
        }
    }
}
