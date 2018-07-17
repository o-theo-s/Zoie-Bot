using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Zoie.Bot.Models
{
    public class CollectionApparel : TableEntity
    {
        public CollectionApparel(string occasion, string choiceSerial)
        {
            this.PartitionKey = occasion;
            this.RowKey = choiceSerial;
        }

        public CollectionApparel() { }

        
        public string Link { get; set; }
        public string ImageLink { get; set; }
        public double Price { get; set; }
        public string Category { get; set; }
    }
}