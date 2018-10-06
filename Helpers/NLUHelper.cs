using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zoie.Helpers
{
    public static class NLUHelper
    {
        public class WitEntities
        {
            public const string ApparelType                 = "apparel_type";
            public const string ApparelSize                 = "apparel_size";
            public const string ApparelColor                = "apparel_color";
            public const string ApparelManufacturer         = "apparel_manufacturer";
            public const string OccasionType                = "occasion_type";
            public const string AmountOfMoney               = "amount_of_money";
            public const string Number                      = "number";
            public const string Gender                      = "gender";
            public const string Word                        = "word";
        }

        public static string CalculateAgeRange(int age)
        {
            string ageGroup;
            if (age < 5)
                ageGroup = "Unknown";
            else if (age < 16)
                ageGroup = "<16";
            else if (age <= 22)
                ageGroup = "16-22";
            else if (age <= 29)
                ageGroup = "23-29";
            else if (age <= 36)
                ageGroup = "30-36";
            else if (age <= 45)
                ageGroup = "37-45";
            else if (age <= 52)
                ageGroup = "46-52";
            else
                ageGroup = ">53";

            return ageGroup;
        }
    }
}
