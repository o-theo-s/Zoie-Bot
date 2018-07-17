using Newtonsoft.Json;

namespace Zoie.Helpers.Channels.Facebook.Library
{
    public class FacebookShareButtonContents
    {
        public FacebookShareButtonContents()
        {

        }

        [JsonProperty("attachment")]
        public FacebookAttachment Attachment { get; set; }
    }
}