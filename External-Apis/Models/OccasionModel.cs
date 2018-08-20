using Newtonsoft.Json;
using System.Collections.Generic;

namespace Zoie.Apis.Models
{
    public class Occasion
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "occasion_name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "occasion_image")]
        public string ImageUrl { get; set; }
    }

    public class OccasionsRoot
    {
        [JsonProperty(PropertyName = "occasions")]
        public List<Occasion> Occasions { get; set; }
    }
}
