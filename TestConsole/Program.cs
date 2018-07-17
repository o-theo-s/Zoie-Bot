using Apis;
using Apis.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            OccasionsRoot occasionsObject = new API<OccasionsRoot>().CallAsync().Result;
            CollectionsRoot collectionsObject = new API<CollectionsRoot>().CallAsync(
                new Dictionary<string, string> { { "occasion_id", "1" }, { "page", "0" } }).Result;

            Console.WriteLine("End\n");
            Console.ReadKey();
        }
    }
}
