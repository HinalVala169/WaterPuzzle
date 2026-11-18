using System;
using UnityEngine;

public static class AdManager
{
    private const string NoAdsKey = "NoAds";

    public static event Action OnNoAdsChanged;

    public static bool IsNoAdsPurchased => PlayerPrefs.GetInt(NoAdsKey, 0) == 1;

    public static void PurchaseNoAds()
    {
        PlayerPrefs.SetInt(NoAdsKey, 1);
        PlayerPrefs.Save();
        OnNoAdsChanged?.Invoke();
    }

    public static void ShowRewardedAd(Action onRewardEarned)
    {
#if UNITY_EDITOR
        Debug.Log("[AdManager] Editor Rewarded Ad simulated -> granting reward.");
        onRewardEarned?.Invoke();
#else
        // TODO: Hook your AdMob / Unity Ads / LevelPlay rewarded ad callback here
        // and invoke onRewardEarned?.Invoke() when the user finishes watching.
        onRewardEarned?.Invoke();
#endif
    }

    public static void ShowInterstitialIfAllowed()
    {
        if (IsNoAdsPurchased) return;

        // TODO: Show interstitial ad via your Ad SDK
    }
}
