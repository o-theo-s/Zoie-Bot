using Newtonsoft.Json;
using System.Collections.Generic;

namespace Zoie.Apis.Models
{
    public class Store
    {
        [JsonProperty(PropertyName = "business_id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "business_image")]
        public string ImageUrl { get; set; }

        [JsonProperty(PropertyName = "business_name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "business_link")]
        public string Link { get; set; }
    }

    public class StoresRoot
    {
        [JsonProperty(PropertyName = "stores")]
        public List<Store> Stores { get; set; }

        [JsonProperty(PropertyName = "remaining_pages")]
        public string RemainingPagesString { get; set; }

        [JsonIgnore]
        public int RemainingPages { get { int.TryParse(this.RemainingPagesString, out int remPages); return remPages; } }
    }
}
