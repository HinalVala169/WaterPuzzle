using System;
using UnityEngine;

public static class AdManager
{
    private const string NoAdsKey = "NoAds";

    public static event Action OnNoAdsChanged;

    /// <summary>
    /// Assign this from your Ad SDK (AdMob / Unity Ads / LevelPlay) initializer:
    /// (onRewardEarned, onAdClosedOrFailed) => { ... }
    /// </summary>
    public static Action<Action, Action> RewardedAdHandler;
    public static Action InterstitialAdHandler;

    public static bool IsShowingRewardedAd { get; private set; }

    public static bool IsNoAdsPurchased => PlayerPrefs.GetInt(NoAdsKey, 0) == 1;

    public static void PurchaseNoAds()
    {
        PlayerPrefs.SetInt(NoAdsKey, 1);
        PlayerPrefs.Save();
        OnNoAdsChanged?.Invoke();
    }

    public static void ShowRewardedAd(Action onRewardEarned, Action onAdClosedOrFailed = null)
    {
        if (IsShowingRewardedAd)
            return;

        if (RewardedAdHandler != null)
        {
            IsShowingRewardedAd = true;
            RewardedAdHandler.Invoke(
                () =>
                {
                    IsShowingRewardedAd = false;
                    onRewardEarned?.Invoke();
                },
                () =>
                {
                    IsShowingRewardedAd = false;
                    onAdClosedOrFailed?.Invoke();
                });
            return;
        }

        Debug.LogWarning("[AdManager] No Rewarded Ad SDK integrated. Import GoogleMobileAds and assign AdManager.RewardedAdHandler.");
        onAdClosedOrFailed?.Invoke();
    }

    public static void ShowInterstitialIfAllowed()
    {
        if (IsNoAdsPurchased) return;

        InterstitialAdHandler?.Invoke();
    }
}
