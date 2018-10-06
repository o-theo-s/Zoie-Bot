using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
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
            public static string UsersData { get { return ConfigurationManager.AppSettings["BotId"] == "Petrichor-Beta" ? "BetaUsersdata" : "usersdata"; } }
            public static string BotData { get { return ConfigurationManager.AppSettings["BotId"] == "Petrichor-Beta" ? "BetaBotdata" : "botdata"; } }
        }
    }
}
