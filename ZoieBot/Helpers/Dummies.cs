using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using ZoieBot.Models.SearchAPI;

namespace ZoieBot.Helpers
{
    public static class Dummies
    {
        private static List<DummyStore> Stores;
        private static List<Product> Products;

        public static List<DummyStore> GetStores()
        {
            if (Stores != null)
                return Stores;

            Stores = new List<DummyStore>(9)
                {
                    new DummyStore("All Zoie Stores", ResolveServerUrl(VirtualPathUtility.ToAbsolute("~/Files/Merchants_Logos/all_stores.png")), "http://zoie.io/"),
                    new DummyStore("Bagiota Shoes", ResolveServerUrl(VirtualPathUtility.ToAbsolute("~/Files/Merchants_Logos/Bagiota_shoes.png")), "https://www.bagiotashoes.gr/"),
                    new DummyStore("Chica Clothing", ResolveServerUrl(VirtualPathUtility.ToAbsolute("~/Files/Merchants_Logos/Chicaclothing.png")), "https://www.chicaclothing.com/"),
                    new DummyStore("Crossover Fashion", ResolveServerUrl(VirtualPathUtility.ToAbsolute("~/Files/Merchants_Logos/Crossover_logo.png")), "https://www.crossoverfashion.gr/"),
                    new DummyStore("My Shoe", ResolveServerUrl(VirtualPathUtility.ToAbsolute("~/Files/Merchants_Logos/myshoestore-logo.png")), "https://www.myshoe.gr/"),
                    new DummyStore("Nespo Athletics", ResolveServerUrl(VirtualPathUtility.ToAbsolute("~/Files/Merchants_Logos/nespo_atletics.png")), "https://www.nespo.gr/"),
                    new DummyStore("Paperino's", ResolveServerUrl(VirtualPathUtility.ToAbsolute("~/Files/Merchants_Logos/paperinos_logo-green-homepage.png")), "https://www.paperinos.gr/"),
                    new DummyStore("Tshoes", ResolveServerUrl(VirtualPathUtility.ToAbsolute("~/Files/Merchants_Logos/tshoes-logo.png")), "https://www.tshoes.gr/"),
                    new DummyStore("Xinos Fashion", ResolveServerUrl(VirtualPathUtility.ToAbsolute("~/Files/Merchants_Logos/xinosfashion-logo-1.png")), "https://www.xinosfashion.gr/")
                };

            return Stores;
        }

        public static async Task<List<Product>> GetTopProductsAsync()
        {
            if (Products != null)
                return Products;

            string json = File.ReadAllText(HostingEnvironment.MapPath(@"~/App_Data/Dummy_Data/products20.json"));
            Products = JsonConvert.DeserializeObject<RootObject>(json).Products;
            for (int i = 0; i < Products.Count; i++)
            {
                if (!await CheckProductUrlsAsync(Products[i]))
                {
                    Products.RemoveAt(i);
                    i--;
                }
            }

            return Products;
        }

        public static async Task<bool> CheckProductUrlsAsync(Product product)
        {
            HttpWebRequest request;
            HttpWebResponse response;

            try
            {
                request = WebRequest.Create(product.ImageUrl) as HttpWebRequest;
                request.Method = "HEAD";
                response = await request.GetResponseAsync() as HttpWebResponse;

                request = WebRequest.Create(product.ProductUrl) as HttpWebRequest;
                request.Method = "HEAD";
                response = await request.GetResponseAsync() as HttpWebResponse;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveServerUrl(string serverUrl, bool forceHttps = false)
        {
            if (serverUrl.IndexOf("://") > -1)
                return serverUrl;

            string newUrl = serverUrl;
            Uri originalUri = HttpContext.Current.Request.Url;
            newUrl = (forceHttps ? "https" : originalUri.Scheme) + "://" + originalUri.Authority + newUrl;
            return newUrl;
        }


        public class DummyStore
        {
            public string Name { get; }
            public string ImageUrl { get; }
            public string Url { get; set; }

            public DummyStore() { }

            public DummyStore(string name, string imageUrl, string Url)
            {
                this.Name = name;
                this.ImageUrl = imageUrl;
                this.Url = Url;
            }
        }
    }
}