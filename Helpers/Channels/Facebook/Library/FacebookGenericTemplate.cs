using Newtonsoft.Json;

namespace Zoie.Helpers.Channels.Facebook.Library
{
    public class FacebookGenericTemplate
    {
        public FacebookGenericTemplate()
        {
            this.TemplateType = "generic";
        }

        [JsonProperty("template_type")]
        public string TemplateType { get; set; }

        [JsonProperty("top_element_style")]
        public string TopElementStyle { get; set; }

        [JsonProperty("elements")]
        public object[] Elements { get; set; }

        [JsonProperty("buttons")]
        public object[] Buttons { get; set; }
    }
}