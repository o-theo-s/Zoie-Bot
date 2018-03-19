using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelperConsole
{
    public class ZoieStore
    {
        public string Sid { get; set; }
        public string Link { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ZoieProduct> Products { get; set; }
    }

    public class ZoieProduct
    {
        public string Gid { get; set; }
        public string Pid { get; set; }
        public string Name { get; set; }
        public string Mpn { get; set; }
        public string Link { get; set; }
        public string ImageLink { get; set; }
        public string AdditionalImageLink { get; set; }
        public float PriceWithVat { get; set; }
        public float Weight { get; set; }
        public string Ean { get; set; }

        public string Manufacturer { get; set; }
        public string Size { get; set; }
        public string Currency { get; set; }
        public string Category { get; set; }
        public string Type { get; set; }
        public string Availability { get; set; }
        public string Color { get; set; }
        public string InStock { get; set; }
    }

    public enum Sizes
    {
        XXXS = -4, XXS, XS, S, M, L, XL, XXL, XXXL
    }
}
