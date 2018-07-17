using Apis.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Apis
{
    public class API<T>
    {
        private static HttpClient Client { get; set; } = new HttpClient() { BaseAddress = new Uri("http://zoie.io/API/") };
        private string CallbackURL { get; set; }

        public API()
        {
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<T> CallAsync(Dictionary<string, string> attributes = null)
        {
            string urlAttributes = ApiNames.GetApiNameFromApiModelType<T>();
            if (attributes != null)
            {
                urlAttributes += "?";
                foreach (var attribute in attributes)
                    urlAttributes += $"{attribute.Key}={attribute.Value}&";
                urlAttributes = urlAttributes.Remove(urlAttributes.Length - 1);
            }

            HttpResponseMessage response = await Client.GetAsync(urlAttributes);
            if (response.IsSuccessStatusCode)
                return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync());

            return default(T);
        }
    }

    public static class ApiNames
    {
        public const string OccasionsList = "occasions.php";                            //-
        public const string CollectionsList = "collections.php";                        //occasion_id, page
        public const string Collection = "collection.php";                              //collection_id
        public const string StoresList = "stores.php";                                  //page
        public const string CustomerService = "http://zoie.io/API/store_info.php";      //business_id, service_id

        public static string GetApiNameFromApiModelType<ApiType>()
        {
            if (typeof(ApiType) == typeof(OccasionsRoot))
                return ApiNames.OccasionsList;
            else if (typeof(ApiType) == typeof(CollectionsRoot))
                return ApiNames.CollectionsList;
            else if (typeof(ApiType) == typeof(CollectionApparelsRoot))
                return ApiNames.Collection;
            else if (typeof(ApiType) == typeof(StoresRoot))
                return ApiNames.StoresList;
            else
                return string.Empty;
        }
    }
}
