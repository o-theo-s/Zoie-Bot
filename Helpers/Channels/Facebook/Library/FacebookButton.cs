using Newtonsoft.Json;

namespace Zoie.Helpers.Channels.Facebook.Library
{
    public class FacebookButton
    {
        public FacebookButton() { }

        [JsonProperty("type")]
        public string Type { get; set; }

        public override string ToString()
        {
            return $"type: {this.Type}";
        }

    }
}