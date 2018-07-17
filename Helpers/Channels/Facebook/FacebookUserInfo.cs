using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Zoie.Helpers.Channels.Facebook
{
    [Serializable]
    public class FacebookUserInfo
    {
        [JsonProperty("first_name")]
        public string FirstName { get; set; }

        [JsonProperty("last_name")]
        public string LastName { get; set; }

        [JsonProperty("profile_pic")]
        public string ProfilePicLink { get; set; }

        [JsonProperty("locale")]
        public string Locale { get; set; }

        [JsonProperty("gender")]
        public string Gender { get; set; }

        [JsonProperty("last_ad_referral")]
        public LastAdReferral lastAdReferral { get; set; }
    }

    [Serializable]
    public class LastAdReferral
    {
        public string source { get; set; }
        public string type { get; set; }
        public string ad_id { get; set; }
    }
}