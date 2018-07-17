using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apis.Models
{
    public class Apparel
    {
        [JsonProperty(PropertyName = "product_id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "product_name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "product_price")]
        public string PriceString { get; set; }

        [JsonIgnore]
        public int Price { get { int.TryParse(this.PriceString, out int price); return price; } }

        [JsonProperty(PropertyName = "product_link")]
        public string Link { get; set; }

        [JsonProperty(PropertyName = "product_image")]
        public string ImageUrl { get; set; }
    }

    public class CollectionApparelsRoot
    {
        [JsonProperty(PropertyName = "collection")]
        public List<Apparel> Apparels { get; set; }
    }
}
