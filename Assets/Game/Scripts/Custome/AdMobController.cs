using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Initializes Google Mobile Ads (AdMob), preloads Rewarded & Interstitial ads,
/// and connects them to <see cref="AdManager"/>.
/// </summary>
public class AdMobController : MonoBehaviour
{
    public static AdMobController Instance { get; private set; }

    [Header("Android Ad Unit IDs (Test IDs by default)")]
    [SerializeField] private string androidBannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
    [SerializeField] private string androidRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    [SerializeField] private string androidInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";

    [Header("iOS Ad Unit IDs (Test IDs by default)")]
    [SerializeField] private string iosBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
    [SerializeField] private string iosRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
    [SerializeField] private string iosInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";

    private BannerView bannerView;
    private RewardedAd rewardedAd;
    private InterstitialAd interstitialAd;
    private bool isLoadingRewarded;
    private bool isLoadingInterstitial;
    private bool isInitialized;
    private bool isBannerLoaded;
    private float currentBannerPixelHeight;

    private readonly Queue<Action> mainThreadQueue = new Queue<Action>();
    private readonly Dictionary<RectTransform, float> originalBottomPanelY = new Dictionary<RectTransform, float>();
    private readonly Dictionary<RectTransform, Vector2> originalMiddlePos = new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, Vector2> originalMiddleSize = new Dictionary<RectTransform, Vector2>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null) return;

        AdMobController existing = FindObjectOfType<AdMobController>();
        if (existing != null) return;

        GameObject go = new GameObject("AdMobController");
        go.AddComponent<AdMobController>();
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

        AdManager.RewardedAdHandler = HandleShowRewardedAd;
        AdManager.InterstitialAdHandler = HandleShowInterstitialAd;
        AdManager.OnNoAdsChanged += HandleNoAdsChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;

        MobileAds.Initialize(initStatus =>
        {
            EnqueueMainThread(() =>
            {
                isInitialized = true;
                LoadRewardedAd();
                if (!AdManager.IsNoAdsPurchased)
                {
                    LoadBannerAd();
                    LoadInterstitialAd();
                }
            });
        });
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            AdManager.OnNoAdsChanged -= HandleNoAdsChanged;
            if (AdManager.RewardedAdHandler == HandleShowRewardedAd)
                AdManager.RewardedAdHandler = null;
            if (AdManager.InterstitialAdHandler == HandleShowInterstitialAd)
                AdManager.InterstitialAdHandler = null;
        }

        DestroyBannerAd();
        rewardedAd?.Destroy();
        interstitialAd?.Destroy();
    }

    private void Update()
    {
        lock (mainThreadQueue)
        {
            while (mainThreadQueue.Count > 0)
            {
                Action action = mainThreadQueue.Dequeue();
                action?.Invoke();
            }
        }
    }

    private void EnqueueMainThread(Action action)
    {
        if (action == null) return;
        lock (mainThreadQueue)
        {
            mainThreadQueue.Enqueue(action);
        }
    }

    private string GetBannerAdUnitId()
    {
#if UNITY_IOS
        return iosBannerAdUnitId;
#else
        return androidBannerAdUnitId;
#endif
    }

    public void LoadBannerAd()
    {
        if (!isInitialized || AdManager.IsNoAdsPurchased)
        {
            DestroyBannerAd();
            return;
        }

        string adUnitId = GetBannerAdUnitId();
        if (string.IsNullOrEmpty(adUnitId)) return;

        DestroyBannerAd();
        AdSize adaptiveSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
        bannerView = new BannerView(adUnitId, adaptiveSize, AdPosition.Bottom);
        bannerView.OnBannerAdLoaded += () =>
        {
            EnqueueMainThread(() =>
            {
                isBannerLoaded = true;
                float px = bannerView != null ? bannerView.GetHeightInPixels() : 0f;
                if (px <= 0f)
                {
                    float dpi = Screen.dpi > 0f ? Screen.dpi : 320f;
                    px = adaptiveSize.Height * (dpi / 160f);
                }
                currentBannerPixelHeight = Mathf.Max(px, 120f);
                ApplyBannerSafeOffset();
#if UNITY_EDITOR
                EnsureEditorBannerOnTop();
#endif
                Debug.Log($"[AdMobController] Adaptive full-width banner loaded (height={currentBannerPixelHeight:0}px).");
            });
        };
        bannerView.OnBannerAdLoadFailed += (LoadAdError err) =>
        {
            EnqueueMainThread(() =>
            {
                isBannerLoaded = false;
                RestoreBottomPanels();
                Debug.LogWarning("[AdMobController] Banner ad failed to load: " + err);
            });
        };
        bannerView.LoadAd(new AdRequest());
    }

    public void DestroyBannerAd()
    {
        isBannerLoaded = false;
        currentBannerPixelHeight = 0f;
        RestoreBottomPanels();

        if (bannerView != null)
        {
            bannerView.Hide();
            bannerView.Destroy();
            bannerView = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        originalBottomPanelY.Clear();
        originalMiddlePos.Clear();
        originalMiddleSize.Clear();
        if (isBannerLoaded && !AdManager.IsNoAdsPurchased)
        {
            ApplyBannerSafeOffset();
#if UNITY_EDITOR
            EnsureEditorBannerOnTop();
#endif
        }
    }

#if UNITY_EDITOR
    private static void EnsureEditorBannerOnTop()
    {
        foreach (Canvas c in FindObjectsOfType<Canvas>(true))
        {
            string n = c.gameObject.name;
            if (n.IndexOf("BANNER", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("ADAPTIVE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("LEADERBOARD", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                c.sortingOrder = 1000;
            }
        }
    }
#endif

    private void ApplyBannerSafeOffset()
    {
        if (!isBannerLoaded || AdManager.IsNoAdsPurchased || currentBannerPixelHeight <= 0f)
            return;

        foreach (RectTransform rt in FindObjectsOfType<RectTransform>(true))
        {
            if (!rt.name.Equals("BottomPanel", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!originalBottomPanelY.ContainsKey(rt))
                originalBottomPanelY[rt] = rt.anchoredPosition.y;

            Canvas rootCanvas = rt.GetComponentInParent<Canvas>();
            float scale = (rootCanvas != null && rootCanvas.scaleFactor > 0f) ? rootCanvas.scaleFactor : 1f;
            float offsetInCanvas = (currentBannerPixelHeight / scale) + 16f;

            Vector2 pos = rt.anchoredPosition;
            pos.y = originalBottomPanelY[rt] + offsetInCanvas;
            rt.anchoredPosition = pos;

            // If this BottomPanel sits below a sibling "middle content" scroll container,
            // shrink its bottom edge by the same offset so BottomPanel never overlaps the cards.
            if (rt.parent != null)
            {
                Transform middleTf = rt.parent.Find("middle content");
                if (middleTf != null && middleTf is RectTransform middleRt)
                {
                    if (!originalMiddlePos.ContainsKey(middleRt))
                    {
                        originalMiddlePos[middleRt] = middleRt.anchoredPosition;
                        originalMiddleSize[middleRt] = middleRt.sizeDelta;
                    }

                    Vector2 origPos = originalMiddlePos[middleRt];
                    Vector2 origSize = originalMiddleSize[middleRt];
                    middleRt.sizeDelta = new Vector2(origSize.x, Mathf.Max(200f, origSize.y - offsetInCanvas));
                    middleRt.anchoredPosition = new Vector2(origPos.x, origPos.y + offsetInCanvas * 0.5f);
                }
            }
        }
    }

    private void RestoreBottomPanels()
    {
        foreach (KeyValuePair<RectTransform, float> kvp in originalBottomPanelY)
        {
            if (kvp.Key == null) continue;
            Vector2 pos = kvp.Key.anchoredPosition;
            pos.y = kvp.Value;
            kvp.Key.anchoredPosition = pos;
        }
        originalBottomPanelY.Clear();

        foreach (KeyValuePair<RectTransform, Vector2> kvp in originalMiddlePos)
        {
            if (kvp.Key == null) continue;
            kvp.Key.anchoredPosition = kvp.Value;
            if (originalMiddleSize.TryGetValue(kvp.Key, out Vector2 origSize))
                kvp.Key.sizeDelta = origSize;
        }
        originalMiddlePos.Clear();
        originalMiddleSize.Clear();
    }

    private void HandleNoAdsChanged()
    {
        if (AdManager.IsNoAdsPurchased)
        {
            DestroyBannerAd();
            if (interstitialAd != null)
            {
                interstitialAd.Destroy();
                interstitialAd = null;
            }
            Debug.Log("[AdMobController] Remove Ads active -> Banner & Interstitial ads disabled.");
        }
    }

    private string GetRewardedAdUnitId()
    {
#if UNITY_IOS
        return iosRewardedAdUnitId;
#else
        return androidRewardedAdUnitId;
#endif
    }

    private string GetInterstitialAdUnitId()
    {
#if UNITY_IOS
        return iosInterstitialAdUnitId;
#else
        return androidInterstitialAdUnitId;
#endif
    }

    public void LoadRewardedAd()
    {
        if (!isInitialized || isLoadingRewarded) return;

        string adUnitId = GetRewardedAdUnitId();
        if (string.IsNullOrEmpty(adUnitId)) return;

        isLoadingRewarded = true;
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        AdRequest request = new AdRequest();
        RewardedAd.Load(adUnitId, request, (RewardedAd ad, LoadAdError error) =>
        {
            EnqueueMainThread(() =>
            {
                isLoadingRewarded = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[AdMobController] Rewarded ad failed to load: " + error);
                    return;
                }

                rewardedAd = ad;
                Debug.Log("[AdMobController] Rewarded ad loaded.");
            });
        });
    }

    public void LoadInterstitialAd()
    {
        if (!isInitialized || isLoadingInterstitial || AdManager.IsNoAdsPurchased) return;

        string adUnitId = GetInterstitialAdUnitId();
        if (string.IsNullOrEmpty(adUnitId)) return;

        isLoadingInterstitial = true;
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }

        AdRequest request = new AdRequest();
        InterstitialAd.Load(adUnitId, request, (InterstitialAd ad, LoadAdError error) =>
        {
            EnqueueMainThread(() =>
            {
                isLoadingInterstitial = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[AdMobController] Interstitial ad failed to load: " + error);
                    return;
                }

                interstitialAd = ad;
                Debug.Log("[AdMobController] Interstitial ad loaded.");
            });
        });
    }

    private void HandleShowRewardedAd(Action onRewardEarned, Action onAdClosedOrFailed)
    {
        if (rewardedAd == null || !rewardedAd.CanShowAd())
        {
            Debug.LogWarning("[AdMobController] Rewarded ad not ready yet. Loading now...");
            LoadRewardedAd();
            onAdClosedOrFailed?.Invoke();
            return;
        }

        bool rewardEarned = false;
        bool finished = false;
        RewardedAd activeAd = rewardedAd;

        void CompleteWithoutReward()
        {
            if (finished) return;
            finished = true;
            if (!rewardEarned)
                onAdClosedOrFailed?.Invoke();
            LoadRewardedAd();
        }

        activeAd.OnAdFullScreenContentClosed += () => EnqueueMainThread(CompleteWithoutReward);
        activeAd.OnAdFullScreenContentFailed += (AdError err) =>
        {
            EnqueueMainThread(() =>
            {
                Debug.LogWarning("[AdMobController] Rewarded ad failed to show: " + err);
                CompleteWithoutReward();
            });
        };

        activeAd.Show((Reward reward) =>
        {
            EnqueueMainThread(() =>
            {
                if (finished && rewardEarned) return;
                rewardEarned = true;
                finished = true;
                onRewardEarned?.Invoke();
                LoadRewardedAd();
            });
        });
    }

    private void HandleShowInterstitialAd()
    {
        if (AdManager.IsNoAdsPurchased) return;

        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            InterstitialAd activeAd = interstitialAd;
            activeAd.OnAdFullScreenContentClosed += () => EnqueueMainThread(LoadInterstitialAd);
            activeAd.OnAdFullScreenContentFailed += (AdError err) => EnqueueMainThread(LoadInterstitialAd);
            activeAd.Show();
        }
        else
        {
            LoadInterstitialAd();
        }
    }
}
