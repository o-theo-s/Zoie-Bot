using Newtonsoft.Json;

namespace Zoie.Helpers.Channels.Facebook.Library
{
    public class FacebookDefaultAction : FacebookButton
    {
        public FacebookDefaultAction()
        {
            this.Type = "web_url";
        }

        public FacebookDefaultAction(string url)
        {
            this.Type = "web_url";
            this.Url = url;
        }

        [JsonProperty("url")]
        public string Url { get; set; }
    }
}