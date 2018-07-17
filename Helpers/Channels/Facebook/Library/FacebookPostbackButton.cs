using Newtonsoft.Json;

namespace Zoie.Helpers.Channels.Facebook.Library
{
    public class FacebookPostbackButton : FacebookButton
    {
        public FacebookPostbackButton()
        {
            this.Type = "postback";
            this.Title = "Postback Title";
            this.Payload = "Postback Payload";
        }

        public FacebookPostbackButton(string title, string payload)
        {
            this.Type = "postback";
            this.Title = title;
            this.Payload = payload;
        }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("payload")]
        public string Payload { get; set; }
    }
}