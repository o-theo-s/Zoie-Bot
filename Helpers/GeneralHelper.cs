using System;
using System.Linq;
using System.Web;
using System.Text;
using System.Runtime.CompilerServices;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Builder.Dialogs;
using Autofac;
using static Zoie.Helpers.DialogsHelper;
using Zoie.Resources.DialogReplies;
using Microsoft.WindowsAzure.Storage.Table;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Zoie.Helpers
{
    public static class GeneralHelper
    {
        public static string CapitalizeFirstLetter(string str) => char.ToUpper(str.First()) + string.Concat(str.Skip(1));

        public static async Task<string> GetDaytimeAsync(IMessageActivity activity, DateTimeOffset? timestamp = null)
        {
            DateTimeOffset datetimeToUse = timestamp.HasValue ? timestamp.Value : activity.Timestamp.Value;
            string resourceToUse = datetimeToUse.Hour >= 17 ? nameof(GeneralReplies.Tonight) : nameof(GeneralReplies.Today);
            return GetResourceValue<GeneralReplies>(resourceToUse, await GetLocaleAsync(activity));
        }

        public static string Hashify(string str) => HttpUtility.UrlEncode( Convert.ToBase64String( Encoding.ASCII.GetBytes(str) ) );

        public static string Dehashify(string str) => Encoding.ASCII.GetString( Convert.FromBase64String( HttpUtility.UrlDecode(str) ) );

        public static string GetActualAsyncMethodName([CallerMemberName] string name = null) => name;

        public static async Task<string> GetLocaleAsync(IMessageActivity activity, bool update = false)
        {
            string locale = "en_US";
            using (var scope = DialogModule.BeginLifetimeScope(Conversation.Container, activity))
            {
                var botData = scope.Resolve<IBotData>();
                await botData.LoadAsync(new System.Threading.CancellationToken());
                
                if (update)
                {
                    var result = TablesHelper.GetTableReference(TablesHelper.TableNames.UsersData)
                        .Execute(TableOperation.Retrieve(activity.From.Name, activity.From.Id, new List<string>(1) { "Locale" }));
                    if (result.Result != null)
                    {
                        botData.UserData.SetValue("Locale", (result.Result as DynamicTableEntity).Properties["Locale"].StringValue);
                        await botData.FlushAsync(new System.Threading.CancellationToken());
                    }
                }

                botData.UserData.TryGetValue("Locale", out locale);
            }

            return locale;
        }
    }
}