using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using PlatformSpellCheck;
using ZoieBot.Models.SearchAPI;
using System.Xml;
using System.Xml.Serialization;

namespace HelperConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var prods = ProductsSearchAPICall().Result;

            Console.WriteLine("OK");
            Console.ReadKey();
        }

        static async Task<List<Product>> ProductsSearchAPICall(string filePath = null)
        {
            List<Product> products = new List<Product>();
            if (string.IsNullOrEmpty(filePath))
            {
                try
                {
                    HttpClient client = new HttpClient
                    {
                        BaseAddress = new Uri("http://zoie.io/API/products-results.php")
                    };
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    HttpResponseMessage response = await client.GetAsync("");//?type=μπλουζα&gender=women&color=μαυρο&max_price=100");
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        File.WriteAllText("products.json", json);

                        products = Newtonsoft.Json.JsonConvert.DeserializeObject<RootObject>(json).Products;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(filePath, Encoding.UTF8);
                    products = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, List<Product>>>(json)["products"];
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            return products;
        }

        static void GreekLUIS()
        {
            string luisModel = File.ReadAllText(@"C:\Users\alfat\Documents\Zoie\Bot\ZoieBot\HelperConsole\Zoie.json");
            Dictionary<string, List<string>> utteranciesPerIntentOriginal = getLuisModelUtteruncies(luisModel);
            Dictionary<string, List<string>> utteranciesPerIntentTransalted = new Dictionary<string, List<string>>(utteranciesPerIntentOriginal.Count);

            foreach (var utterKey in utteranciesPerIntentOriginal.Keys)
            {
                for (int i = 0; i < utteranciesPerIntentOriginal[utterKey].Count; i++)
                {
                    var kati = PlatformSpellCheck.SpellChecker.SupportedLanguages;
                }
            }
        }
        static Dictionary<string, List<string>> getLuisModelUtteruncies(string json)
        {
            Dictionary<string, List<string>> tore = new Dictionary<string, List<string>>();
            List<Utterance> utterances = Newtonsoft.Json.JsonConvert.DeserializeObject<LuisSchema>(json).utterances;
            List<Intent> intents = Newtonsoft.Json.JsonConvert.DeserializeObject<LuisSchema>(json).intents;

            foreach (var intent in intents)
            {
                List<string> utters = new List<string>();
                foreach (var utter in utterances)
                {
                    if (utter.intent == intent.name)
                        utters.Add(utter.text);
                }
                tore.Add(intent.name, utters);
            }

            return tore;
        }
    }
}
