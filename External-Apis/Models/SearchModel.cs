﻿using System;
using System.Collections.Generic;

namespace Zoie.Apis.Models
{
    [Serializable]
    public class SearchModel
    {
        public int? Page { get; set; }
        public string Manufacturer { get; set; }
        public string Type { get; set; }
        public string Gender { get; set; }
        public string Color { get; set; }
        public float? Min_Price { get; set; }
        public float? Max_Price { get; set; }
        public string Size { get; set; }
        public string Style { get; set; }
        public string Shop { get; set; }

        public Dictionary<string, string> GetAttributesDictionary()
        {
            var modelProperties = this.GetType().GetProperties();
            var tore = new Dictionary<string, string>(modelProperties.Length);

            foreach (var property in modelProperties)
                tore.Add(property.Name.ToLower(), property.GetValue(this)?.ToString());

            return tore;
        }
    }
}
