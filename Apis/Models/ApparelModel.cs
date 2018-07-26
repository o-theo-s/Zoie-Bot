using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Zoie.Apis.Models
{
    public class Apparel
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "pid")]
        public string ProductId { get; set; }

        [JsonProperty(PropertyName = "business_id")]
        public string BusinessId { get; set; }

        [JsonProperty(PropertyName = "category")]
        public string Category { get; set; }

        [JsonProperty(PropertyName = "style_mapping")]
        public string Style { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "price")]
        public string Price { get; set; }

        [JsonProperty(PropertyName = "product_link")]
        public string ProductUrl { get; set; }

        [JsonProperty(PropertyName = "image_url")]
        public string ImageUrl { get; set; }

        [JsonProperty(PropertyName = "size")]
        public string SizesString { get; set; }
        //public List<string> Sizes { get { return this.SizesString.Split(',').ToList(); } }

        [JsonProperty(PropertyName = "manufacturer")]
        public string Manufacturer { get; set; }

        [JsonProperty(PropertyName = "description")]
        public string Description { get; set; }

        [JsonProperty(PropertyName = "color")]
        public string Color { get; set; }

        [JsonProperty(PropertyName = "sku")]
        public string Sku { get; set; }

        [JsonProperty(PropertyName = "mpn")]
        public string Mpn { get; set; }

        [JsonProperty(PropertyName = "instock")]
        public string InStock { get; set; }

        [JsonProperty(PropertyName = "availability")]
        public string Availability { get; set; }

        [JsonProperty(PropertyName = "date_added")]
        public string AddedAt { get; set; }
    }

    public class ApparelsRoot
    {
        [JsonProperty(PropertyName = "products")]
        public List<Apparel> Apparels { get; set; }
    }
}