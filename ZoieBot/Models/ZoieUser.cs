using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ZoieBot.Models
{
    [Serializable]
    public class ZoieUser
    {
        public string Name { get; set; }
        public Dictionary<string, string> ChannelIds { get; set; }
        public string Email { get; set; }
        public ushort? Age { get; set; }
        public string Locale { get; set; }
        public Genders? Gender { get; set; }
        public bool IsAdult { get { return (this.Age ?? 0) >= 18; } }
        public bool IsHandshaked { get; set; }
        public DateTime SubscribedAt { get; set; }
    }

    public enum Genders
    {
        Male, Female
    }
}