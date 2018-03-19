using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace HelperConsole
{
    [Serializable()]
    public class SkroutzProduct
    {
        [XmlElement("id")]
        public string Id { get; set; }

        [XmlElement("name")]
        public string Name { get; set; }

        [XmlElement("mpn")]
        public string Mpn { get; set; }

        [XmlElement("link")]
        public string Link { get; set; }

        [XmlElement("price")]
        public float Price { get; set; }

        [XmlElement("category_path")]
        public string Category_path { get; set; }

        [XmlElement("image")]
        public string Image { get; set; }

        [XmlElement("availability")]
        public string Availability { get; set; }

        [XmlElement("Size")]
        public string Size { get; set; }

        [XmlElement("Color")]
        public string Color { get; set; }

        [XmlElement("manufacturer")]
        public string Manufacturer { get; set; }
    }


    [Serializable()]
    [System.Xml.Serialization.XmlRoot("skroutzstore")]
    public class SkroutzStore
    {
        [XmlElement("created_at")]
        public string CreatedAt { get; set; }

        [XmlArray("products")]
        [XmlArrayItem("product", typeof(SkroutzProduct))]
        public SkroutzProduct[] Products { get; set; }
    }
}
