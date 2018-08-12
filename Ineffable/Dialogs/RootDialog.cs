using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

namespace Zoie.Ineffable.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            if (context.UserData.TryGetValue("HasHandshaked", out bool handshaked) && handshaked)
                context.Wait(MessageReceivedAsync);
            else
                context.Call(new HandshakeDialog(), MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            //var form = new FormDialog<HotelReservation>(new HotelReservation(activity.ChannelData), HotelReservation.BuildForm);
            await context.Forward(new HotelDialog(), MessageReceivedAsync, activity.AsMessageActivity());

            return;
        }
    }
}