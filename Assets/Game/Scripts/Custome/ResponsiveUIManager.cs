using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ensures all Canvases scale cleanly across phones, tall displays, and tablets,
/// and shifts top/bottom edge-anchored HUD elements clear of device notches and gesture bars.
/// </summary>
public class ResponsiveUIManager : MonoBehaviour
{
    public static ResponsiveUIManager Instance { get; private set; }

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private Rect lastSafeArea = Rect.zero;

    private readonly Dictionary<RectTransform, Vector2> originalAnchoredPos = new Dictionary<RectTransform, Vector2>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null) return;

        ResponsiveUIManager existing = FindFirstObjectByType<ResponsiveUIManager>();
        if (existing != null) return;

        GameObject go = new GameObject("ResponsiveUIManager");
        go.AddComponent<ResponsiveUIManager>();
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
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyResponsiveAdjustments(force: true);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        originalAnchoredPos.Clear();
        ApplyResponsiveAdjustments(force: true);
    }

    private void Update()
    {
        if (Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight ||
            Screen.safeArea != lastSafeArea)
        {
            ApplyResponsiveAdjustments(force: false);
        }
    }

    public void ApplyResponsiveAdjustments(bool force)
    {
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastSafeArea = Screen.safeArea;

        UpdateCanvasScalers();
        ApplySafeAreaToEdgeHUD();
    }

    private void UpdateCanvasScalers()
    {
        float aspect = (float)Screen.width / Screen.height;
        // On wider/tablet screens (e.g. 3:4 = 0.75, 4:3 = 1.33), favor height matching
        // so tall portrait popups fit without clipping top/bottom.
        float tabletBlend = Mathf.InverseLerp(0.58f, 0.80f, aspect);

        CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler scaler = scalers[i];
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                continue;

            Vector2 refRes = scaler.referenceResolution;
            if (refRes.y >= refRes.x)
            {
                // Portrait reference canvas: 0.5 on phones, smoothly up to 1.0 on tablets
                scaler.matchWidthOrHeight = Mathf.Lerp(0.5f, 1f, tabletBlend);
            }
        }
    }

    private void ApplySafeAreaToEdgeHUD()
    {
        Rect safe = Screen.safeArea;
        float topPixels = Mathf.Max(0f, Screen.height - (safe.y + safe.height));
        float bottomPixels = Mathf.Max(0f, safe.y);

        if (topPixels < 1f && bottomPixels < 1f && !Application.isEditor)
            return;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int c = 0; c < canvases.Length; c++)
        {
            Canvas canvas = canvases[c];
            if (canvas == null || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            float topUnits = topPixels / scale;
            float bottomUnits = bottomPixels / scale;

            Transform root = canvas.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform rt = root.GetChild(i) as RectTransform;
                if (rt == null) continue;

                // Skip backgrounds and AdMob-managed panels
                string n = rt.name;
                if (n.Equals("BG", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("Bg", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("BottomPanel", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isTopAnchored = Mathf.Approximately(rt.anchorMin.y, 1f) && Mathf.Approximately(rt.anchorMax.y, 1f);
                bool isBottomAnchored = Mathf.Approximately(rt.anchorMin.y, 0f) && Mathf.Approximately(rt.anchorMax.y, 0f);

                if (!isTopAnchored && !isBottomAnchored)
                    continue;

                if (!originalAnchoredPos.ContainsKey(rt))
                    originalAnchoredPos[rt] = rt.anchoredPosition;

                Vector2 basePos = originalAnchoredPos[rt];
                if (isTopAnchored)
                    rt.anchoredPosition = new Vector2(basePos.x, basePos.y - topUnits);
                else if (isBottomAnchored)
                    rt.anchoredPosition = new Vector2(basePos.x, basePos.y + bottomUnits);
            }
        }
    }
}
