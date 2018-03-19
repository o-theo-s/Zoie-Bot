using System;
using System.Collections.Generic;
using System.Data.Entity.SqlServer;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Routing;
using Autofac;
using System.Configuration;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;

namespace ZoieBot
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);

            //SqlServerTypes.Utilities.LoadNativeAssemblies(Server.MapPath("~/bin"));
            //SqlProviderServices.SqlServerTypesAssemblyName = "Microsoft.SqlServer.Types, Version=14.0.314.76, Culture=neutral, PublicKeyToken=89845dcd8080cc91";

            var store = new TableBotDataStore(ConfigurationManager.ConnectionStrings["StorageConnectionString"].ConnectionString);
            Conversation.UpdateContainer( builder =>
              {
                   builder.Register(c => store)
                             .Keyed<IBotDataStore<BotData>>(AzureModule.Key_DataStore)
                             .AsSelf()
                             .SingleInstance();

                  builder.Register(c => new CachingBotDataStore(store, CachingBotDataStoreConsistencyPolicy.ETagBasedConsistency))
                             .As<IBotDataStore<BotData>>()
                             .AsSelf()
                             .InstancePerLifetimeScope();
              });
        }
    }
}
