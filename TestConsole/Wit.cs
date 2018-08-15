using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Zoie.TestConsole
{
    public class Wit
    {
        private static HttpClient Client { get; set; } = new HttpClient() { BaseAddress = new Uri("https://api.wit.ai/") };

        public Wit()
        {
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Add("Authorization", "Bearer P5YJ7UKGYMFSSYJ4DQ6VXVH3OYWIN3CB");
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public static async Task PostEntityAsync(string entityId, string filePath, char delim = '\t', string metadataBase = null)
        {
            string[] lines = File.ReadAllLines(filePath).Skip(1).ToArray();
            foreach (var line in lines)
            {
                var cells = line.Split(new char[1] { delim }, StringSplitOptions.RemoveEmptyEntries);

                var entityValue = new WitModel
                {
                    Keyword = cells.First().Trim('\"'),
                    Synonyms = cells.Last().Trim('\"').Split(new string[2] { ", ", "," }, StringSplitOptions.RemoveEmptyEntries),
                    Metadata = metadataBase
                };
                entityValue.Metadata += entityValue.Keyword;

                string requestJson = JsonConvert.SerializeObject(entityValue);
                var response = await Client.PostAsync($"entities/{entityId}/values?v=1", new StringContent(requestJson));
            }
        }

        [Serializable]
        public class WitModel
        {
            [JsonProperty(PropertyName = "value")]
            public string Keyword { get; set; }

            [JsonProperty(PropertyName = "expressions")]
            public string[] Synonyms { get; set; }

            [JsonProperty(PropertyName = "metadata")]
            public string Metadata { get; set; }
        }
    }
}
