using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace Zoie.Petrichor.Models.Entities
{
    public class UserData : TableEntity
    {
        public UserData(string name, string idInChannel)
        {
            this.PartitionKey = name;
            this.RowKey = idInChannel;

            this.SubscribedAt = DateTime.UtcNow;
        }

        public UserData()
        {
            this.SubscribedAt = DateTime.UtcNow;
        }

        public string Channel { get; set; }
        public string Email { get; set; }
        public string AgeGroup { get; set; }
        public string Locale { get; set; }
        public string Gender { get; set; }
        public DateTime SubscribedAt { get; set; }
        public string City { get; set; }
        public string Location { get; set; }
    }
}