using Newtonsoft.Json;

namespace Zoie.Helpers.Channels.Facebook.Library
{
    public class FacebookGenericTemplateContent
    {
        public FacebookGenericTemplateContent()
        {
            this.Title = "This is a title.";
            this.Subtitle = "";
            this.ImageUrl = "";
        }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("subtitle")]
        public string Subtitle { get; set; }

        [JsonProperty("image_url")]
        public string ImageUrl { get; set; }

        [JsonProperty("buttons")]
        public object[] Buttons { get; set; }

        [JsonProperty("default_action")]
        public FacebookDefaultAction Tap { get; set; }
    }
}