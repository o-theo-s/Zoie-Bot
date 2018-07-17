using Newtonsoft.Json;

namespace Zoie.Helpers.Channels.Facebook.Library
{
    public class FacebookChannelData
    {
        [JsonProperty("attachment")]
        public FacebookAttachment Attachment { get; set; }
    }
}