using Newtonsoft.Json;
using System.Collections.Generic;

namespace Zoie.Apis.Models
{
    public class Collection
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "collection_title")]
        public string Title { get; set; }

        private string imageUrl;
        [JsonProperty(PropertyName = "collection_image")]
        public string ImageUrl { get { return imageUrl; } set { imageUrl = string.IsNullOrWhiteSpace(value) ? null : value; } }

        [JsonProperty(PropertyName = "business_name")]
        public string StoreName { get; set; }
    }

    public class CollectionsRoot
    {
        [JsonProperty(PropertyName = "collections")]
        public List<Collection> Collections { get; set; }

        [JsonProperty(PropertyName = "remaining_pages")]
        public string RemainingPagesString { get; set; }

        [JsonIgnore]
        public int RemainingPages { get { int.TryParse(this.RemainingPagesString, out int remPages); return remPages; } }
    }
}
