﻿using Zoie.Apis;
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
            SearchModel search = new SearchModel
            {
                Color = "μαύρο",
                Gender = "άντρας",
                Max_Price = 100
            };
            var attr = search.GetAttributesDictionary();

            var searchApparelsApi = new API<ApparelsRoot>();
            var apparelsRoot = searchApparelsApi.CallAsync(attr).Result;

            Console.WriteLine("End\n");
            Console.ReadKey();
        }
    }
}
