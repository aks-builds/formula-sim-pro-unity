using System;
using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS
using Unity.Notifications.Android;
using Unity.Notifications.iOS;
#endif

namespace FormulaSim.Notifications
{
    public static class PushSystem
    {
        const string RACE_CHANNEL_ID    = "race_reminder";
        const string DAILY_CHANNEL_ID   = "daily_bonus";
        const string CONTRACT_CHANNEL_ID= "contract_update";

        public static void Init()
        {
#if UNITY_ANDROID
            var raceChannel = new AndroidNotificationChannel
            {
                Id          = RACE_CHANNEL_ID,
                Name        = "Race Reminders",
                Description = "Alerts before each Formula Sim Pro race weekend.",
                Importance  = Importance.High,
            };
            AndroidNotificationCenter.RegisterNotificationChannel(raceChannel);
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
            {
                Id = DAILY_CHANNEL_ID, Name = "Daily Bonus", Importance = Importance.Default,
                Description = "Notify when daily bonus is ready."
            });
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
            {
                Id = CONTRACT_CHANNEL_ID, Name = "Contract Offers", Importance = Importance.Default,
                Description = "New team contract offers in your career."
            });
#elif UNITY_IOS
            iOSNotificationCenter.RequestAuthorization(
                AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound);
#endif
        }

        public static void ScheduleRace(string circuitName, DateTime raceTime)
        {
            var now = DateTime.Now;

            // 24h reminder
            if (raceTime - now > TimeSpan.FromHours(24))
                Schedule($"Race weekend — {circuitName}",
                    $"Qualifying ahead. Set your strategy before you leave.",
                    raceTime.AddHours(-24), RACE_CHANNEL_ID);

            // 1h reminder
            if (raceTime - now > TimeSpan.FromHours(1))
                Schedule($"Race day! {circuitName} Grand Prix",
                    "Your car is ready. Lights out in 1 hour.",
                    raceTime.AddHours(-1), RACE_CHANNEL_ID);
        }

        public static void ScheduleDailyBonus()
        {
            Schedule("Your daily bonus is waiting",
                "Log in to collect credits and tyre tokens.",
                DateTime.Now.AddHours(22), DAILY_CHANNEL_ID);
        }

        public static void NotifyContract(string teamName)
        {
            Schedule($"{teamName} wants to sign you",
                "A new contract offer has arrived. Review before it expires.",
                DateTime.Now.AddSeconds(2), CONTRACT_CHANNEL_ID);
        }

        public static void CancelAll()
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
#endif
        }

        static void Schedule(string title, string body, DateTime fireAt, string channelId)
        {
            var delay = fireAt - DateTime.Now;
            if (delay.TotalSeconds <= 0) return;

#if UNITY_ANDROID
            var n = new AndroidNotification
            {
                Title       = title,
                Text        = body,
                FireTime    = fireAt,
                SmallIcon   = "icon_small",
                LargeIcon   = "icon_large",
            };
            AndroidNotificationCenter.SendNotification(n, channelId);
#elif UNITY_IOS
            var n = new iOSNotification
            {
                Title    = title,
                Body     = body,
                ShowInForeground = false,
                Trigger  = new iOSNotificationTimeIntervalTrigger
                {
                    TimeInterval = delay,
                    Repeats = false,
                }
            };
            iOSNotificationCenter.ScheduleNotification(n);
#endif
        }
    }
}
