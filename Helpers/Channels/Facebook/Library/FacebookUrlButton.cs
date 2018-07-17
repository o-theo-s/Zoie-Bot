using Newtonsoft.Json;

namespace Zoie.Helpers.Channels.Facebook.Library
{
    public class FacebookUrlButton : FacebookDefaultAction
    {
        public FacebookUrlButton() : base() { }

        public FacebookUrlButton(string url, string title) : base(url)
        {
            this.Title = title;
        }

        [JsonProperty("title")]
        public string Title { get; set; }
    }
}
