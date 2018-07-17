using Newtonsoft.Json;

namespace Zoie.Helpers.Channels.Facebook.Library
{
    public class FacebookShareButton : FacebookButton
    {
        public FacebookShareButton()
        {
            this.Type = "element_share";
        }

        [JsonProperty("share_contents")]
        public object ShareContents { get; set; }
    }
}
