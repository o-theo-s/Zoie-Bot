using Zoie.Apis;
using Zoie.Apis.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zoie.TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            ApiUtilitiesAsync().Wait();

            Console.WriteLine("End");
            Console.ReadKey();
        }

        static async Task ApiUtilitiesAsync()
        {
            var productsApi = new API<ApparelsRoot>();

            SearchModel searchAttributes = new SearchModel
            {
                Type = "jacket",
                Color = Resources.Dictionaries.Apparels.Colors.black
            };

            ApparelsRoot apparelsRoot = await productsApi.CallAsync(searchAttributes.GetAttributesDictionary());
        }

        static async Task WitUtilitiesAsync()
        {
            var wit = new Wit();
            await Wit.PostEntityAsync("apparel_type", @"Wit Dictionary\types.txt", metadataBase: "__filters_type_");
        }
    }
}
