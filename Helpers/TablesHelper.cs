using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace Zoie.Helpers
{
    public static class TablesHelper
    {
        private static CloudStorageAccount StorageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);
        private static CloudTableClient TableClient = StorageAccount.CreateCloudTableClient();

        public static CloudTable GetTableReference(string tableName)
        {
            return TableClient.GetTableReference(tableName);
        }

        public static class TableNames
        {
            public const string UsersData = "usersdata";
        }
    }
}
