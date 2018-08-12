using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

#pragma warning disable CS1998

namespace Zoie.Ineffable.Forms
{
    [Serializable]
    public class HotelReservation
    {
        [Prompt("What city are you thinking for your vacations?")]
        public string Location;

        [Prompt("When would you like to travel? Please specify the dates of your check in and check out in your hotel in {Location}.")]
        public string ReservetionDates;

        [IgnoreField]
        public DateTime? StartingDate;
        [IgnoreField]
        public DateTime? EndingDate;

        //Like Wit Reply
        private dynamic channelData;

        public HotelReservation() { }

        public HotelReservation(object channelData)
        {
            this.channelData = channelData;
        }

        public static IForm<HotelReservation> BuildForm()
        {
            return new FormBuilder<HotelReservation>()
                    .Message("Let's start planning your next vacations!")
                    .Field(nameof(Location), validate: LocationValidation)
                    .Build();
        }

        public static async Task<ValidateResult> LocationValidation(HotelReservation state, object value)
        {
            string text = value as string;
            var result = new ValidateResult { IsValid = false };

            dynamic location = state.channelData.message.nlp?.entities?.location;
            if (location != null)
            {
                result.IsValid = true;
                result.Value = location[0].value.ToString();
            }

            return result;
        }

    }
}