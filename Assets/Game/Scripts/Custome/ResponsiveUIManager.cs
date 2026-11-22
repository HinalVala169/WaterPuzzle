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

    private void Start()
    {
        // Re-apply after all scene objects finish Awake/Start on initial launch
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
        FixStretchedButtons();
    }

    private void FixStretchedButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];
            if (btn == null || btn.transform.parent == null)
                continue;

            string parentName = btn.transform.parent.name;
            bool isHudButton =
                parentName.Equals("BottomPanel", System.StringComparison.OrdinalIgnoreCase) ||
                parentName.Equals("Header", System.StringComparison.OrdinalIgnoreCase) ||
                parentName.Equals("middle content", System.StringComparison.OrdinalIgnoreCase);

            if (!isHudButton)
                continue;

            Image img = btn.targetGraphic as Image ?? btn.GetComponent<Image>();
            if (img == null || img.sprite == null || img.type != Image.Type.Simple)
                continue;

            if (img.sprite.border != Vector4.zero)
                img.type = Image.Type.Sliced;
            else
                img.preserveAspect = true;
        }
    }

    private void UpdateCanvasScalers()
    {
        float aspect = (float)Screen.width / Screen.height;

        CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler scaler = scalers[i];
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                continue;

            Vector2 refRes = scaler.referenceResolution;
            if (refRes.y >= refRes.x && refRes.y > 0f)
            {
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                float refAspect = refRes.x / refRes.y;
                if (aspect <= refAspect)
                {
                    // Narrow/tall phones (e.g. 20:9): match width (0) so UI never overflows horizontally
                    scaler.matchWidthOrHeight = 0f;
                }
                else
                {
                    // Wider screens / tablets (e.g. 3:4, 4:3): blend smoothly toward height matching (1)
                    float tabletBlend = Mathf.InverseLerp(refAspect, 0.80f, aspect);
                    scaler.matchWidthOrHeight = Mathf.Lerp(0f, 1f, tabletBlend);
                }
            }
        }
    }

    private void ApplySafeAreaToEdgeHUD()
    {
        Rect safe = Screen.safeArea;
        float topPixels = Mathf.Max(0f, Screen.height - (safe.y + safe.height));
        float bottomPixels = Mathf.Max(0f, safe.y);
        float leftPixels = Mathf.Max(0f, safe.x);
        float rightPixels = Mathf.Max(0f, Screen.width - (safe.x + safe.width));

        if (topPixels < 1f && bottomPixels < 1f && leftPixels < 1f && rightPixels < 1f && !Application.isEditor)
            return;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int c = 0; c < canvases.Length; c++)
        {
            Canvas canvas = canvases[c];
            if (canvas == null || !canvas.isRootCanvas)
                continue;

            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay &&
                canvas.renderMode != RenderMode.ScreenSpaceCamera)
                continue;

            float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            float topUnits = topPixels / scale;
            float bottomUnits = bottomPixels / scale;
            float leftUnits = leftPixels / scale;
            float rightUnits = rightPixels / scale;

            ApplySafeAreaRecursive(canvas.transform, topUnits, bottomUnits, leftUnits, rightUnits);
        }
    }

    private void ApplySafeAreaRecursive(Transform parent, float topUnits, float bottomUnits, float leftUnits, float rightUnits)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            RectTransform rt = parent.GetChild(i) as RectTransform;
            if (rt == null) continue;

            // Skip nested root/override canvases (they are handled in the outer loop)
            Canvas nestedCanvas = rt.GetComponent<Canvas>();
            if (nestedCanvas != null && nestedCanvas.isRootCanvas)
                continue;

            // Skip backgrounds and AdMob-managed panels
            string n = rt.name;
            if (n.Equals("BG", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Bg", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("BackGround", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("BottomPanel", System.StringComparison.OrdinalIgnoreCase))
                continue;

            bool isFullStretch =
                Mathf.Approximately(rt.anchorMin.x, 0f) && Mathf.Approximately(rt.anchorMax.x, 1f) &&
                Mathf.Approximately(rt.anchorMin.y, 0f) && Mathf.Approximately(rt.anchorMax.y, 1f);

            if (isFullStretch)
            {
                // Recurse into full-screen layout containers (e.g. Image, Parent) without shifting the container itself
                ApplySafeAreaRecursive(rt, topUnits, bottomUnits, leftUnits, rightUnits);
                continue;
            }

            bool isTopAnchored = Mathf.Approximately(rt.anchorMin.y, 1f) && Mathf.Approximately(rt.anchorMax.y, 1f);
            bool isBottomAnchored = Mathf.Approximately(rt.anchorMin.y, 0f) && Mathf.Approximately(rt.anchorMax.y, 0f);
            bool isLeftAnchored = Mathf.Approximately(rt.anchorMin.x, 0f) && Mathf.Approximately(rt.anchorMax.x, 0f);
            bool isRightAnchored = Mathf.Approximately(rt.anchorMin.x, 1f) && Mathf.Approximately(rt.anchorMax.x, 1f);

            if (!isTopAnchored && !isBottomAnchored && !isLeftAnchored && !isRightAnchored)
                continue;

            if (!originalAnchoredPos.ContainsKey(rt))
                originalAnchoredPos[rt] = rt.anchoredPosition;

            Vector2 pos = originalAnchoredPos[rt];
            if (isTopAnchored) pos.y -= topUnits;
            else if (isBottomAnchored) pos.y += bottomUnits;

            if (isLeftAnchored) pos.x += leftUnits;
            else if (isRightAnchored) pos.x -= rightUnits;

            rt.anchoredPosition = pos;
        }
    }
}
