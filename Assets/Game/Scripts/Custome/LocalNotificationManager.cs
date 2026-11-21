using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

/// <summary>
/// Manages local reminder notifications for Android and iOS.
/// Respects UIController.IsNotificationEnabled and auto-bootstraps at runtime.
/// </summary>
public class LocalNotificationManager : MonoBehaviour
{
    public static LocalNotificationManager Instance { get; private set; }

    private const string AndroidChannelId = "water_puzzle_daily_reminder";
    private const string ReminderTitle = "Happy Water Sort";
    private const string ReminderText = "Your colorful bottles are waiting! Ready for a relaxing puzzle?";

    private bool isInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null) return;

        LocalNotificationManager existing = FindFirstObjectByType<LocalNotificationManager>();
        if (existing != null) return;

        GameObject go = new GameObject("LocalNotificationManager");
        go.AddComponent<LocalNotificationManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeChannel();

        if (UIController.IsNotificationEnabled)
        {
            ClearDisplayedNotifications();
            ScheduleDailyReminder();
        }
        else
        {
            CancelAll();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            if (UIController.IsNotificationEnabled)
                ScheduleDailyReminder();
            else
                CancelAll();
        }
        else
        {
            ClearDisplayedNotifications();
        }
    }

    private void OnApplicationQuit()
    {
        if (UIController.IsNotificationEnabled)
            ScheduleDailyReminder();
        else
            CancelAll();
    }

    public static void EnableNotifications()
    {
        if (Instance == null) return;
        Instance.StartCoroutine(Instance.RequestPermissionAndScheduleRoutine());
    }

    public static void DisableNotifications()
    {
        if (Instance == null) return;
        Instance.CancelAll();
    }

    private void InitializeChannel()
    {
        if (isInitialized) return;
        isInitialized = true;

#if UNITY_ANDROID
        var channel = new AndroidNotificationChannel
        {
            Id = AndroidChannelId,
            Name = "Daily Reminders",
            Importance = Importance.Default,
            Description = "Daily reminders to play Happy Water Sort"
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
#endif
    }

    private IEnumerator RequestPermissionAndScheduleRoutine()
    {
        InitializeChannel();

#if UNITY_ANDROID
        var request = new PermissionRequest();
        while (request.Status == PermissionStatus.RequestPending)
            yield return null;

        if (request.Status == PermissionStatus.Allowed)
            ScheduleDailyReminder();
#elif UNITY_IOS
        using (var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, true))
        {
            while (!req.IsFinished)
                yield return null;

            if (req.Granted)
                ScheduleDailyReminder();
        }
#else
        ScheduleDailyReminder();
        yield break;
#endif
    }

    private void ScheduleDailyReminder()
    {
        InitializeChannel();

#if UNITY_ANDROID
        AndroidNotificationCenter.CancelAllScheduledNotifications();

        var notification = new AndroidNotification
        {
            Title = ReminderTitle,
            Text = ReminderText,
            FireTime = DateTime.Now.AddHours(24),
            RepeatInterval = TimeSpan.FromHours(24)
        };
        AndroidNotificationCenter.SendNotification(notification, AndroidChannelId);
#elif UNITY_IOS
        iOSNotificationCenter.RemoveAllScheduledNotifications();

        var timeTrigger = new iOSNotificationTimeIntervalTrigger
        {
            TimeInterval = TimeSpan.FromHours(24),
            Repeats = true
        };

        var notification = new iOSNotification
        {
            Identifier = AndroidChannelId,
            Title = ReminderTitle,
            Body = ReminderText,
            ShowInForeground = false,
            Trigger = timeTrigger
        };
        iOSNotificationCenter.ScheduleNotification(notification);
#endif
    }

    private void ClearDisplayedNotifications()
    {
#if UNITY_ANDROID
        AndroidNotificationCenter.CancelAllDisplayedNotifications();
#elif UNITY_IOS
        iOSNotificationCenter.RemoveAllDeliveredNotifications();
#endif
    }

    private void CancelAll()
    {
#if UNITY_ANDROID
        AndroidNotificationCenter.CancelAllNotifications();
#elif UNITY_IOS
        iOSNotificationCenter.RemoveAllScheduledNotifications();
        iOSNotificationCenter.RemoveAllDeliveredNotifications();
#endif
    }
}
