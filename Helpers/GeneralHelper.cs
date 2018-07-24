using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Text;
using System.Runtime.CompilerServices;

namespace Zoie.Helpers
{
    public static class GeneralHelper
    {
        public static string CapitalizeFirstLetter(string str)
        {
            return char.ToUpper(str.First()) + String.Concat(str.Skip(1));
        }

        public static string GetDaytime(DateTimeOffset? timestamp)
        {
            string daytime = "today";

            if (timestamp.HasValue && timestamp.Value.Hour >= 17)
                daytime = "tonight";

            return daytime;
        }

        public static string Hashify(string str)
        {
            return HttpUtility.UrlEncode( Convert.ToBase64String( Encoding.ASCII.GetBytes(str) ) );
        }

        public static string Dehashify(string str)
        {
            return Encoding.ASCII.GetString( Convert.FromBase64String( HttpUtility.UrlDecode(str) ) );
        }

        public static string GetActualAsyncMethodName([CallerMemberName] string name = null) => name;
    }
}