using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Builder.Internals.Fibers;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Builder.Scorables.Internals;
using Newtonsoft.Json;

#pragma warning disable 1998

namespace Zoie.Petrichor.Dialogs.Scorables
{
    public class SettingsScorable : ScorableBase<IActivity, bool, double>
    {
        private readonly IDialogTask task;

        public SettingsScorable(IDialogTask task)
        {
            SetField.NotNull(out this.task, nameof(task), task);
        }

        protected override async Task<bool> PrepareAsync(IActivity item, CancellationToken token)
        {
            var activity = item as Activity;

            return activity.Text?.StartsWith("__menu_settings") ?? false;
        }

        protected override bool HasScore(IActivity item, bool state)
        {
            return state;
        }

        protected override double GetScore(IActivity item, bool state)
        {
            return state ? 1.0 : 0.0;
        }

        protected override async Task PostAsync(IActivity item, bool state, CancellationToken token)
        {
            var activity = item as Activity;
            var cloneActivity = JsonConvert.DeserializeObject<Activity>(JsonConvert.SerializeObject(activity));
            cloneActivity.Text = "__continue";

            await this.task.Forward(new SettingsDialog().PostEvent(cloneActivity).Void<object, IMessageActivity>(), null, activity);
            await this.task.PollAsync(token);
        }

        protected override Task DoneAsync(IActivity item, bool state, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}