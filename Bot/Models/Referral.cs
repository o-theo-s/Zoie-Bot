using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace Zoie.Bot.Models
{
    [Serializable]
    public class Referral
    {
        public Referral() { }

        private string type = string.Empty;

        public string Type
        {
            get
            {
                return this.type;
            }
            set
            {
                if (Types.ConfirmType(value))
                    this.type = value;
                else
                    throw new IncorrectReferralTypeException("Not supported type of referral.");
            }
        }
        public string SharedFrom { get; set; }
        public string Item { get; set; }

        public static class Types
        {
            public const string Store = "store";
            public const string Invitation = "invitation";
            public const string Collection = "collection";

            public static bool ConfirmType(string type)
            {
                return
                    type == Types.Store ||
                    type == Types.Invitation ||
                    type == Types.Collection;
            }
        }
    }

    public class IncorrectReferralTypeException : Exception
    {
        public IncorrectReferralTypeException() { }
        public IncorrectReferralTypeException(string message) : base(message) { }
    }
}