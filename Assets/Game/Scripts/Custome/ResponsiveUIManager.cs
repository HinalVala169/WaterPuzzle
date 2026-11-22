using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ensures all Canvases, popups, ad buttons, backgrounds, and gameplay camera scale cleanly
/// across all phones, tall displays, notched screens, and tablets without stretching or clipping.
/// </summary>
public class ResponsiveUIManager : MonoBehaviour
{
    public static ResponsiveUIManager Instance { get; private set; }

    private const float MinGameplayHalfWidth = 1.65f;

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private Rect lastSafeArea = Rect.zero;
    private float baseCameraOrthoSize = -1f;
    private Camera trackedCamera;

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
        trackedCamera = null;
        baseCameraOrthoSize = -1f;
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
        Canvas.ForceUpdateCanvases();
        UpdateCameraProjection();
        FixBackgroundsAndOverlays();
        FixStretchedButtonsAndPopups();
        FixBottomPanelsLayout();
        ApplySafeAreaToEdgeHUD();
        if (AdMobController.Instance != null)
            AdMobController.Instance.RefreshBannerOffset();
        FixSelectionLayout();
    }

    private void UpdateCameraProjection()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
            return;

        if (trackedCamera != cam || baseCameraOrthoSize <= 0f)
        {
            trackedCamera = cam;
            baseCameraOrthoSize = cam.orthographicSize > 0f ? cam.orthographicSize : 3f;
        }

        float aspect = (float)Screen.width / Screen.height;
        float requiredOrtho = MinGameplayHalfWidth / Mathf.Max(0.25f, aspect);
        cam.orthographicSize = Mathf.Max(baseCameraOrthoSize, requiredOrtho);
    }

    private void UpdateCanvasScalers()
    {
        // Ensure every non-AdMob root Canvas (e.g. RemoveADs) has a CanvasScaler
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allCanvases.Length; i++)
        {
            Canvas c = allCanvases[i];
            if (c == null || !c.isRootCanvas) continue;
            if (c.renderMode != RenderMode.ScreenSpaceOverlay && c.renderMode != RenderMode.ScreenSpaceCamera)
                continue;

            string n = c.gameObject.name;
            if (n.IndexOf("BANNER", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("ADAPTIVE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("LEADERBOARD", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            if (c.GetComponent<CanvasScaler>() == null)
            {
                CanvasScaler added = c.gameObject.AddComponent<CanvasScaler>();
                added.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                added.referenceResolution = new Vector2(1080f, 1920f);
                added.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            }
        }

        float aspect = (float)Screen.width / Screen.height;

        CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler scaler = scalers[i];
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                continue;

            Vector2 refRes = scaler.referenceResolution;
            if (refRes.x <= 0f || refRes.y <= 0f)
                continue;

            // Normalize landscape-configured scalers (e.g. 1920x1080) in this portrait game
            // so narrow phones and tablets scale consistently without clipping popups.
            if (refRes.x > refRes.y)
            {
                if (Mathf.Approximately(scaler.matchWidthOrHeight, 1f))
                    refRes = new Vector2(refRes.y * (9f / 16f), refRes.y);
                else
                    refRes = new Vector2(1080f, 1920f);

                scaler.referenceResolution = refRes;
            }

            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            float refAspect = refRes.x / refRes.y;
            if (aspect <= refAspect)
            {
                // Narrow/tall phones (e.g. 20:9, 21:9): match width (0) so UI never overflows horizontally
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

    private void FixBackgroundsAndOverlays()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int c = 0; c < canvases.Length; c++)
        {
            Canvas canvas = canvases[c];
            if (canvas == null) continue;

            RectTransform canvasRt = canvas.GetComponent<RectTransform>();
            if (canvasRt == null) continue;

            float cw = canvasRt.rect.width;
            float ch = canvasRt.rect.height;
            if (cw <= 0f || ch <= 0f) continue;

            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                RectTransform child = canvas.transform.GetChild(i) as RectTransform;
                if (child == null) continue;

                string n = child.name;
                if (n.Equals("overlay", StringComparison.OrdinalIgnoreCase))
                {
                    StretchToFill(child);
                }
                else if (n.Equals("BG", StringComparison.OrdinalIgnoreCase) ||
                         n.Equals("Demo", StringComparison.OrdinalIgnoreCase))
                {
                    EnvelopeFillParent(child, cw, ch);
                }
                else if (n.Equals("Next Level", StringComparison.OrdinalIgnoreCase))
                {
                    Transform overlayTf = child.Find("Overlay");
                    if (overlayTf is RectTransform overlayRt)
                        StretchToFill(overlayRt);
                }
            }
        }
    }

    private static void StretchToFill(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    private static void EnvelopeFillParent(RectTransform rt, float parentW, float parentH)
    {
        Image img = rt.GetComponent<Image>();
        if (img == null || img.sprite == null)
        {
            StretchToFill(rt);
            return;
        }

        Rect spriteRect = img.sprite.rect;
        if (spriteRect.width <= 0f || spriteRect.height <= 0f)
            return;

        float spriteAspect = spriteRect.width / spriteRect.height;
        float parentAspect = parentW / parentH;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        if (parentAspect > spriteAspect)
            rt.sizeDelta = new Vector2(parentW, parentW / spriteAspect);
        else
            rt.sizeDelta = new Vector2(parentH * spriteAspect, parentH);
    }

    private void FixStretchedButtonsAndPopups()
    {
        // 1. Preserve aspect or use 9-slice on all UI Buttons so no button ever stretches
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];
            if (btn == null) continue;

            Image img = btn.targetGraphic as Image ?? btn.GetComponent<Image>();
            MakeImageNonStretched(img);
        }

        // 2. Preserve aspect or use 9-slice on popup cards & badges (BG, BackGround, ProfileIMG, ADS)
        Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null || img.sprite == null) continue;

            string n = img.gameObject.name;
            bool isPopupOrBadge =
                n.Equals("BackGround", StringComparison.OrdinalIgnoreCase) ||
                n.Equals("ADS", StringComparison.OrdinalIgnoreCase) ||
                n.Equals("ProfileIMG", StringComparison.OrdinalIgnoreCase) ||
                (n.Equals("BG", StringComparison.OrdinalIgnoreCase) &&
                 img.transform.parent != null &&
                 img.transform.parent.name.Equals("overlay", StringComparison.OrdinalIgnoreCase));

            if (isPopupOrBadge)
                MakeImageNonStretched(img);
        }
    }

    private static void MakeImageNonStretched(Image img)
    {
        if (img == null || img.sprite == null || img.type != Image.Type.Simple)
            return;

        if (img.sprite.border != Vector4.zero)
            img.type = Image.Type.Sliced;
        else
            img.preserveAspect = true;
    }

    private void FixBottomPanelsLayout()
    {
        foreach (RectTransform rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!rt.name.Equals("BottomPanel", StringComparison.OrdinalIgnoreCase))
                continue;

            bool isBottomStretched =
                Mathf.Approximately(rt.anchorMin.x, 0f) && Mathf.Approximately(rt.anchorMax.x, 1f) &&
                Mathf.Approximately(rt.anchorMin.y, 0f) && Mathf.Approximately(rt.anchorMax.y, 0f);

            if (isBottomStretched)
            {
                // Center the BottomPanel cleanly across full width (fixes -60 / -120 scene offset)
                Vector2 pos = rt.anchoredPosition;
                pos.x = 0f;
                rt.anchoredPosition = pos;

                Vector2 size = rt.sizeDelta;
                size.x = 0f;
                rt.sizeDelta = size;
            }

            float panelW = rt.rect.width > 0f ? rt.rect.width : 1080f;

            // SelectionCanvas BottomPanel: ADBTN & ADBTN (1)
            Transform adBtn1Tf = rt.Find("ADBTN");
            Transform adBtn2Tf = rt.Find("ADBTN (1)");
            if (adBtn1Tf is RectTransform adBtn1 && adBtn2Tf is RectTransform adBtn2)
            {
                float maxBtnW = Mathf.Min(460f, (panelW - 72f) * 0.5f);
                float btnH = maxBtnW * (176f / 500f);
                float halfOffset = maxBtnW * 0.5f + 16f;

                ConfigureCenteredBottomButton(adBtn1, new Vector2(-halfOffset, 10f), new Vector2(maxBtnW, btnH));
                ConfigureCenteredBottomButton(adBtn2, new Vector2(halfOffset, 10f), new Vector2(maxBtnW, btnH));
                continue;
            }

            // Gameplay UICanvas BottomPanel: Restart, Active Undo / InactiveUndo, ActivePlus
            Transform restartTf = rt.Find("Restart");
            Transform undoActiveTf = rt.Find("Active Undo");
            Transform undoInactiveTf = rt.Find("InactiveUndo");
            Transform plusTf = rt.Find("ActivePlus");

            if (restartTf is RectTransform restartRt &&
                undoActiveTf is RectTransform undoRt &&
                plusTf is RectTransform plusRt)
            {
                float spacing = Mathf.Clamp((panelW - 220f) * 0.34f, 200f, 340f);
                float yPos = 36f;

                ConfigureCenteredBottomButton(restartRt, new Vector2(-spacing, yPos), restartRt.sizeDelta);
                ConfigureCenteredBottomButton(undoRt, new Vector2(0f, yPos), undoRt.sizeDelta);
                if (undoInactiveTf is RectTransform undoInactRt)
                    ConfigureCenteredBottomButton(undoInactRt, new Vector2(0f, yPos), undoInactRt.sizeDelta);
                ConfigureCenteredBottomButton(plusRt, new Vector2(spacing, yPos), plusRt.sizeDelta);
            }
        }
    }

    private static void ConfigureCenteredBottomButton(RectTransform rt, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }

    public void FixSelectionLayout()
    {
        Canvas selCanvas = null;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c != null && c.gameObject.name.Equals("SelectionCanvas", StringComparison.Ordinal))
            {
                selCanvas = c;
                break;
            }
        }
        if (selCanvas == null) return;

        Transform imageTf = selCanvas.transform.Find("Image");
        if (imageTf == null) return;

        RectTransform imageRt = imageTf as RectTransform;
        RectTransform middleRt = imageTf.Find("middle content") as RectTransform;
        RectTransform bottomRt = imageTf.Find("BottomPanel") as RectTransform;
        if (imageRt == null || middleRt == null || bottomRt == null) return;

        RectTransform themePanelRt = (imageTf.Find("ThemePanel") ?? middleRt.Find("ThemePanel")) as RectTransform;
        if (themePanelRt == null) return;

        if (themePanelRt.parent != imageTf)
            themePanelRt.SetParent(imageTf, false);

        float scale = (selCanvas != null && selCanvas.scaleFactor > 0f) ? selCanvas.scaleFactor : 1f;
        float topPixels = Mathf.Max(0f, Screen.height - (Screen.safeArea.y + Screen.safeArea.height));
        float topUnits = topPixels / scale;

        float parentW = imageRt.rect.width > 0f ? imageRt.rect.width : 1080f;
        themePanelRt.anchorMin = new Vector2(0.5f, 1f);
        themePanelRt.anchorMax = new Vector2(0.5f, 1f);
        themePanelRt.pivot = new Vector2(0.5f, 1f);
        themePanelRt.anchoredPosition = new Vector2(0f, -(topUnits + 196f));
        themePanelRt.sizeDelta = new Vector2(Mathf.Min(1040f, parentW - 40f), 111f);

        float bottomPanelTop = bottomRt.anchoredPosition.y + bottomRt.sizeDelta.y;
        RectTransform adBtn = bottomRt.Find("ADBTN") as RectTransform;
        if (adBtn != null)
        {
            float adTop = bottomRt.anchoredPosition.y + (bottomRt.sizeDelta.y * 0.5f) + adBtn.anchoredPosition.y + (adBtn.sizeDelta.y * 0.5f);
            bottomPanelTop = Mathf.Max(bottomPanelTop, adTop);
        }

        middleRt.anchorMin = Vector2.zero;
        middleRt.anchorMax = Vector2.one;
        middleRt.pivot = new Vector2(0.5f, 0.5f);
        middleRt.offsetMax = new Vector2(0f, -(topUnits + 328f));
        middleRt.offsetMin = new Vector2(0f, bottomPanelTop + 24f);
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
            if (n.Equals("BG", StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Bg", StringComparison.OrdinalIgnoreCase) ||
                n.Equals("BackGround", StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Demo", StringComparison.OrdinalIgnoreCase) ||
                n.Equals("overlay", StringComparison.OrdinalIgnoreCase) ||
                n.Equals("BottomPanel", StringComparison.OrdinalIgnoreCase))
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
