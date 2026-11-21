using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ThemeSelectionManager : MonoBehaviour
{
    public static ThemeSelectionManager Instance;

    // ── Gameplay preview images shown in the popup header ──────────────────────
    [Header("Gameplay Preview")]
    [SerializeField] private Image gameplayBG;
    [SerializeField] private Image gameplayBottle;

    // ── Sprites ─────────────────────────────────────────────────────────────────
    [Header("Sprites – Background")]
    [SerializeField] private List<Sprite> bgSprites;

    [Header("Sprites – Bottle")]
    [SerializeField] private List<Sprite> bottlePreviewSprites;
    [SerializeField] private List<Sprite> bottleUpSprites;
    [SerializeField] private List<Sprite> bottleFillSprites;

    // ── Color Palette Cards ──────────────────────────────────────────────────────
    // Add one entry per card shown in the Color scroll.
    // Index 0 = first card, 1 = second card, etc.
    // The 'previewSprite' is what's shown ON the card in the UI.
    // The 'colors' array is what gets applied to the game bottles when selected.
    [System.Serializable]
    public class ColorPaletteCard
    {
        [Tooltip("Thumbnail image shown on the card in the color picker")]
        public Sprite previewSprite;

        [Tooltip("12 colors — same slot order as colorsIndex in LevelBuilder / BottleController")]
        public Color[] colors = new Color[12];
    }

    [Header("Color Palette Cards (one per card, in card order)")]
    [SerializeField] private List<ColorPaletteCard> colorPaletteCards = new List<ColorPaletteCard>();

    // ── UI item lists ────────────────────────────────────────────────────────────
    [Header("UI Items")]
    [SerializeField] private List<ThemeItemUI> bgItems;
    [SerializeField] private List<ThemeItemUI> bottleItems;
    [SerializeField] private List<ThemeItemUI> colorItems;

    // ── Preview bottle (small bottle displayed inside the selection UI) ──────────
    [Header("Preview Bottle")]
    [SerializeField] private Image bottleTop;
    [SerializeField] private Image bottleFill;

    // ── Game-scene reference ─────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private BottlesController bottlesController;

    [Header("Economy & Unlock Settings")]
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private Button watchAdCoinsButton;
    [SerializeField] private Button removeAdsButton;
    [SerializeField] private int adCoinReward = 150;
    [SerializeField] private float adCooldownSeconds = 30f;
    [SerializeField] private int maxAdsPerDay = 5;
    [SerializeField] private int defaultCoinCost = 500;
    [SerializeField] private int freeItemsPerCategory = 2;
    [SerializeField] private int bottleLevelUnlockIndex = 2;
    [SerializeField] private int bottleRequiredLevel = 30;
    [SerializeField] private int bgLevelUnlockIndex = 2;
    [SerializeField] private int bgRequiredLevel = 15;

    [Header("Theme UI Assets (Auto-loaded if empty)")]
    [SerializeField] private Sprite lockIconSprite;
    [SerializeField] private Sprite coinPillSprite;
    [SerializeField] private Sprite coinIconSprite;
    [SerializeField] private Sprite headerCoinPanelSprite;
    [SerializeField] private TMP_FontAsset lockFont;

    private Coroutine adButtonPulseRoutine;
    private readonly Dictionary<ThemeCategory, HashSet<int>> freeIndicesByCategory = new Dictionary<ThemeCategory, HashSet<int>>();
    private readonly List<Button> allWatchAdButtons = new List<Button>();
    private readonly List<Button> allRemoveAdsButtons = new List<Button>();

    // Tracks current selections
    private int currentBGIndex     = -1;
    private int currentBottleIndex = -1;
    private int currentColorIndex  = -1;
    private bool isInitialized     = false;

    // Priority order of slot indices used by LevelBuilder across 2-, 3-, 5-, 7-, 9-, and 12-bottle levels
    private static readonly int[] LevelBuilderSlotPriority = new int[]
    {
        0, 1, 7, 5, 9, 6, 10, 4, 8, 3, 2, 11
    };

    // ─────────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        EnsureInitialized();
    }

    private void Start()
    {
        EnsureInitialized();

        // Auto-find BottlesController if not assigned
        if (bottlesController == null)
            bottlesController = FindObjectOfType<BottlesController>();

        CoinManager.OnCoinsChanged += UpdateCoinsUI;
        AdManager.OnNoAdsChanged += UpdateRemoveAdsButton;

        WireEconomyButtons();
        EnsureThemeVisualAssets();
        UpdateCoinsUI(CoinManager.Coins);
        UpdateRemoveAdsButton();
        RefreshAllLockStates();

        // Restore saved selections
        SetDefaultSelections();
    }

    private void OnDestroy()
    {
        CoinManager.OnCoinsChanged -= UpdateCoinsUI;
        AdManager.OnNoAdsChanged -= UpdateRemoveAdsButton;
    }

    // Ensures colorItems indices and colorPaletteCards are populated before any bottle spawns
    public void EnsureInitialized()
    {
        Instance = this;

        if (colorItems == null || colorItems.Count == 0)
            return;

        if (colorPaletteCards == null)
            colorPaletteCards = new List<ColorPaletteCard>();

        while (colorPaletteCards.Count < colorItems.Count)
            colorPaletteCards.Add(new ColorPaletteCard());

        for (int i = 0; i < colorItems.Count; i++)
        {
            if (colorItems[i] == null) continue;

            colorItems[i].SetIndex(i);

            if (colorPaletteCards[i] == null)
                colorPaletteCards[i] = new ColorPaletteCard();

            if (colorPaletteCards[i].previewSprite != null)
                colorItems[i].SetPreviewSprite(colorPaletteCards[i].previewSprite);

            // Auto-extract the 12-slot palette from the card's 3 preview bottle materials in the UI
            if (!HasValidColors(colorPaletteCards[i].colors))
            {
                Color[] extracted = ExtractColorsFromCard(colorItems[i]);
                if (extracted != null && extracted.Length > 0)
                {
                    colorPaletteCards[i].colors = extracted;
                }
            }
        }

        if (currentColorIndex < 0)
        {
            currentColorIndex = Mathf.Clamp(
                PlayerPrefs.GetInt("SelectedColor", 0),
                0,
                colorItems.Count - 1);
        }

        isInitialized = true;
    }

    private static bool HasValidColors(Color[] colors)
    {
        if (colors == null || colors.Length == 0) return false;
        for (int i = 0; i < colors.Length; i++)
        {
            if (colors[i].a > 0.01f && (colors[i].r > 0.01f || colors[i].g > 0.01f || colors[i].b > 0.01f))
                return true;
        }
        return false;
    }

    // Reads _C1, _C2, _C3, _C4 directly from the 3 preview bottles (col1, col2, col3) on the UI card
    private Color[] ExtractColorsFromCard(ThemeItemUI cardItem)
    {
        if (cardItem == null) return null;

        Image[] childImages = cardItem.GetComponentsInChildren<Image>(true);
        List<Image> liquidImages = new List<Image>();

        foreach (var img in childImages)
        {
            if (img != null && img.material != null && img.material.HasProperty("_C1"))
            {
                liquidImages.Add(img);
            }
        }

        if (liquidImages.Count == 0) return null;

        // Sort left-to-right (bottle 1, bottle 2, bottle 3)
        liquidImages.Sort((a, b) =>
            a.rectTransform.anchoredPosition.x.CompareTo(b.rectTransform.anchoredPosition.x));

        string[] shaderProps = new string[] { "_C1", "_C2", "_C3", "_C4" };
        List<Color> uniqueColors = new List<Color>();

        foreach (var img in liquidImages)
        {
            Material mat = img.material;
            foreach (string prop in shaderProps)
            {
                if (mat.HasProperty(prop))
                {
                    Color raw = mat.GetColor(prop);
                    raw.a = 1f;

                    // Keep original card colors intact; only lift ultra-dark navy so it doesn't vanish into the dark blue BG
                    Color visible = EnsureVisibleAgainstBlueBG(raw);

                    if (!ContainsSimilarColor(uniqueColors, visible, 0.05f))
                    {
                        uniqueColors.Add(visible);
                    }
                }
            }
        }

        if (uniqueColors.Count == 0) return null;

        // If a card's 3 preview bottles have fewer unique colors than the level needs (e.g. 4 colors on a 5- or 7-bottle level),
        // generate rich, saturated companion shades in the same color family so bottles never look chalky or duplicate a color.
        ExpandPaletteWithinTheme(uniqueColors, 12);

        // Build the 12-slot palette using ONLY the colors from this card's theme,
        // placing unique colors into the exact slot indices LevelBuilder uses first.
        Color[] palette = new Color[12];
        int uCount = uniqueColors.Count;

        for (int p = 0; p < LevelBuilderSlotPriority.Length; p++)
        {
            int slot = LevelBuilderSlotPriority[p];
            palette[slot] = uniqueColors[p % uCount];
        }

        return palette;
    }

    // Keeps the card's deep/bright contrast intact while lifting only the ultra-dark navy shade
    // (V < 0.42 in the blue range) to a rich Deep Ocean Blue so it doesn't blend into the night-sky background.
    private static Color EnsureVisibleAgainstBlueBG(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);

        // Only adjust dark navy-blue shades (H: 0.52..0.72, V < 0.45) that match the dark blue background (#0B285B)
        if (h >= 0.52f && h <= 0.72f && v < 0.45f)
        {
            // Keep it a deep, rich ocean blue (high saturation, medium-deep value) so it preserves the card's dark contrast
            // while remaining clearly visible against the dark background
            h = Mathf.Clamp(h, 0.56f, 0.58f);
            s = Mathf.Max(s, 0.86f);
            v = 0.66f;
            Color lifted = Color.HSVToRGB(h, s, v);
            lifted.a = 1f;
            return lifted;
        }

        c.a = 1f;
        return c;
    }

    // Expands a 4-color themed card with rich, high-saturation companion shades in the same hue family
    // (no pale/chalky pastels) so 5-, 7-, and 9-bottle levels look vibrant and cohesive.
    private static void ExpandPaletteWithinTheme(List<Color> uniqueColors, int targetCount)
    {
        if (uniqueColors == null || uniqueColors.Count == 0 || uniqueColors.Count >= targetCount)
            return;

        int initialCount = uniqueColors.Count;
        float sumH = 0f;
        for (int i = 0; i < initialCount; i++)
        {
            Color.RGBToHSV(uniqueColors[i], out float h, out _, out _);
            sumH += h;
        }
        float avgH = sumH / initialCount;

        // High-saturation, vibrant companion shades around the card's hue family
        float[] hueOffsets = new float[] {  0.07f, -0.06f,  0.10f, -0.10f,  0.04f, -0.04f,  0.13f, -0.13f };
        float[] satValues  = new float[] {  0.86f,  0.88f,  0.82f,  0.90f,  0.78f,  0.85f,  0.84f,  0.88f };
        float[] valValues  = new float[] {  0.96f,  0.78f,  0.88f,  0.64f,  0.72f,  0.92f,  0.82f,  0.70f };

        for (int i = 0; i < hueOffsets.Length && uniqueColors.Count < targetCount; i++)
        {
            float candH = Mathf.Repeat(avgH + hueOffsets[i], 1f);
            Color candidate = EnsureVisibleAgainstBlueBG(Color.HSVToRGB(candH, satValues[i], valValues[i]));
            if (!ContainsSimilarColor(uniqueColors, candidate, 0.07f))
            {
                uniqueColors.Add(candidate);
            }
        }
    }

    private static bool ContainsSimilarColor(List<Color> list, Color candidate, float threshold = 0.02f)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Color existing = list[i];
            float dr = existing.r - candidate.r;
            float dg = existing.g - candidate.g;
            float db = existing.b - candidate.b;
            if (Mathf.Sqrt(dr * dr + dg * dg + db * db) < threshold * 1.732f)
            {
                return true;
            }
        }
        return false;
    }

    // ── Restore saved selections on scene load ───────────────────────────────────
    private void SetDefaultSelections()
    {
        RestoreDefaultBG();
        RestoreDefaultBottle();
        RestoreDefaultColor();
    }

    private void RestoreDefaultBG()
    {
        if (bgItems == null || bgItems.Count == 0 || bgSprites == null || bgSprites.Count == 0) return;
        int idx = Mathf.Clamp(PlayerPrefs.GetInt("SelectedBG", 0), 0, Mathf.Min(bgItems.Count, bgSprites.Count) - 1);
        if (!IsItemUnlocked(ThemeCategory.Background, idx))
            idx = GetFirstUnlockedIndex(ThemeCategory.Background, bgItems.Count);
        foreach (var item in bgItems) item.SetSelected(false);
        currentBGIndex = -1;
        ApplyBG(idx, bgItems[idx]);
    }

    private void RestoreDefaultBottle()
    {
        if (bottleItems == null || bottleItems.Count == 0) return;
        int idx = Mathf.Clamp(PlayerPrefs.GetInt("SelectedBottle", 0), 0, bottleItems.Count - 1);
        if (!IsItemUnlocked(ThemeCategory.Bottle, idx))
            idx = GetFirstUnlockedIndex(ThemeCategory.Bottle, bottleItems.Count);
        foreach (var item in bottleItems) item.SetSelected(false);
        currentBottleIndex = -1;
        ApplyBottle(idx, bottleItems[idx]);
    }

    private void RestoreDefaultColor()
    {
        if (colorItems == null || colorItems.Count == 0) return;
        int idx = Mathf.Clamp(PlayerPrefs.GetInt("SelectedColor", 0), 0, colorItems.Count - 1);
        if (!IsItemUnlocked(ThemeCategory.Color, idx))
            idx = GetFirstUnlockedIndex(ThemeCategory.Color, colorItems.Count);
        foreach (var item in colorItems) item.SetSelected(false);
        // Force apply on startup
        ApplyColorPalette(idx, colorItems[idx], forceApply: true);
    }

    private int GetFirstUnlockedIndex(ThemeCategory category, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (IsItemUnlocked(category, i))
                return i;
        }
        return 0;
    }

    // ── Public entry point called by ThemeItemUI.OnClick() ──────────────────────
    public void SelectItem(ThemeCategory category, int index, ThemeItemUI selectedItem)
    {
        if (!IsItemUnlocked(category, index))
        {
            GetUnlockRequirement(category, index, out bool isLevelLocked, out int value);

            if (isLevelLocked)
            {
                if (selectedItem != null) selectedItem.PlayShakeAnimation();
                return;
            }

            if (CoinManager.TrySpendCoins(value))
            {
                UnlockItem(category, index);
                RefreshAllLockStates();
                if (selectedItem != null) selectedItem.PlayUnlockPopAnimation();
            }
            else
            {
                if (selectedItem != null) selectedItem.PlayShakeAnimation();
                PulseWatchAdButton();
                return;
            }
        }

        switch (category)
        {
            case ThemeCategory.Background: ApplyBG(index, selectedItem);                        break;
            case ThemeCategory.Bottle:     ApplyBottle(index, selectedItem);                    break;
            case ThemeCategory.Color:      ApplyColorPalette(index, selectedItem, false);       break;
        }
    }

    // ── Economy & Lock Helpers ───────────────────────────────────────────────────
    private Transform GetSelectionRoot()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>(true);
        if (parentCanvas != null)
            return parentCanvas.rootCanvas != null ? parentCanvas.rootCanvas.transform : parentCanvas.transform;

        Transform sampleItem = GetFirstItemTransform();
        if (sampleItem != null)
        {
            Canvas itemCanvas = sampleItem.GetComponentInParent<Canvas>(true);
            if (itemCanvas != null)
                return itemCanvas.rootCanvas != null ? itemCanvas.rootCanvas.transform : itemCanvas.transform;
        }

        return transform;
    }

    private Transform GetFirstItemTransform()
    {
        if (bottleItems != null && bottleItems.Count > 0 && bottleItems[0] != null) return bottleItems[0].transform;
        if (bgItems != null && bgItems.Count > 0 && bgItems[0] != null) return bgItems[0].transform;
        if (colorItems != null && colorItems.Count > 0 && colorItems[0] != null) return colorItems[0].transform;
        return null;
    }

    private static bool HasRemoveAdsLabel(Transform root)
    {
        foreach (TextMeshProUGUI tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp != null && !string.IsNullOrEmpty(tmp.text) &&
                tmp.text.IndexOf("Remove", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private void WireEconomyButtons()
    {
        allWatchAdButtons.Clear();
        allRemoveAdsButtons.Clear();

        if (watchAdCoinsButton != null)
            allWatchAdButtons.Add(watchAdCoinsButton);
        if (removeAdsButton != null)
            allRemoveAdsButtons.Add(removeAdsButton);

        Transform searchRoot = GetSelectionRoot();
        foreach (Transform child in searchRoot.GetComponentsInChildren<Transform>(true))
        {
            string childName = child.name;
            bool isRemoveAd = childName.IndexOf("RemoveAd", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                              (childName.IndexOf("ADBTN", System.StringComparison.OrdinalIgnoreCase) >= 0 && HasRemoveAdsLabel(child));

            if (isRemoveAd)
            {
                Button btn = EnsureButtonComponent(child.gameObject);
                if (btn != null && !allRemoveAdsButtons.Contains(btn))
                    allRemoveAdsButtons.Add(btn);
                if (removeAdsButton == null)
                    removeAdsButton = btn;
            }
            else if (childName.IndexOf("ADBTN", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Button btn = EnsureButtonComponent(child.gameObject);
                if (btn != null && !allWatchAdButtons.Contains(btn))
                    allWatchAdButtons.Add(btn);
                if (watchAdCoinsButton == null)
                    watchAdCoinsButton = btn;
            }
        }

        if (coinsText == null)
            CreateRuntimeCoinsBadge();

        foreach (Button btn in allWatchAdButtons)
        {
            if (btn != null && btn.onClick.GetPersistentEventCount() == 0)
            {
                btn.onClick.RemoveListener(OnWatchAdForCoinsClick);
                btn.onClick.AddListener(OnWatchAdForCoinsClick);
            }
        }

        foreach (Button btn in allRemoveAdsButtons)
        {
            if (btn != null && btn.onClick.GetPersistentEventCount() == 0)
            {
                btn.onClick.RemoveListener(OnRemoveAdsClick);
                btn.onClick.AddListener(OnRemoveAdsClick);
            }
        }

        RefreshWatchAdButtonsUI();
    }

    private static Button EnsureButtonComponent(GameObject go)
    {
        Button btn = go.GetComponent<Button>();
        if (btn == null)
        {
            btn = go.AddComponent<Button>();
            Graphic g = go.GetComponent<Graphic>();
            if (g != null)
            {
                g.raycastTarget = true;
                btn.targetGraphic = g;
            }
        }
        return btn;
    }

    private static float nextRewardedAdAllowedTime = -1f;
    private const string DailyAdsDateKey = "RewardedAdsDate";
    private const string DailyAdsCountKey = "RewardedAdsCountToday";

    private void Update()
    {
        if (allWatchAdButtons.Count > 0)
            RefreshWatchAdButtonsUI();
    }

    private int GetDailyAdsWatched()
    {
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        if (PlayerPrefs.GetString(DailyAdsDateKey, "") != today)
        {
            PlayerPrefs.SetString(DailyAdsDateKey, today);
            PlayerPrefs.SetInt(DailyAdsCountKey, 0);
            PlayerPrefs.Save();
            return 0;
        }
        return PlayerPrefs.GetInt(DailyAdsCountKey, 0);
    }

    private void RecordDailyAdWatched()
    {
        int count = GetDailyAdsWatched() + 1;
        PlayerPrefs.SetInt(DailyAdsCountKey, count);
        PlayerPrefs.Save();
        nextRewardedAdAllowedTime = Time.realtimeSinceStartup + adCooldownSeconds;
    }

    private float GetRemainingAdCooldown()
    {
        if (nextRewardedAdAllowedTime < 0f) return 0f;
        return Mathf.Max(0f, nextRewardedAdAllowedTime - Time.realtimeSinceStartup);
    }

    private bool CanWatchRewardedAd(out string buttonLabel)
    {
        if (maxAdsPerDay > 0 && GetDailyAdsWatched() >= maxAdsPerDay)
        {
            buttonLabel = "Limit";
            return false;
        }

        float cooldown = GetRemainingAdCooldown();
        if (cooldown > 0f)
        {
            buttonLabel = Mathf.CeilToInt(cooldown) + "s";
            return false;
        }

        buttonLabel = "+" + adCoinReward;
        return !AdManager.IsShowingRewardedAd;
    }

    private void RefreshWatchAdButtonsUI()
    {
        bool canWatch = CanWatchRewardedAd(out string label);
        foreach (Button btn in allWatchAdButtons)
        {
            if (btn == null) continue;
            if (btn.interactable != canWatch)
                btn.interactable = canWatch;

            TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && tmp.text != label)
                tmp.text = label;
        }
    }

    public void OnWatchAdForCoinsClick()
    {
        if (AdManager.IsShowingRewardedAd || !CanWatchRewardedAd(out _))
        {
            RefreshWatchAdButtonsUI();
            return;
        }

        SetWatchAdButtonsInteractable(false);
        AdManager.ShowRewardedAd(
            onRewardEarned: () =>
            {
                int startCoins = CoinManager.Coins;
                isPlayingCoinRewardAnim = true;
                CoinManager.AddCoins(adCoinReward);
                RecordDailyAdWatched();
                RefreshWatchAdButtonsUI();
                StartCoinCollectAnimation(startCoins, CoinManager.Coins);
            },
            onAdClosedOrFailed: () =>
            {
                RefreshWatchAdButtonsUI();
            });
    }

    private void SetWatchAdButtonsInteractable(bool interactable)
    {
        foreach (Button btn in allWatchAdButtons)
        {
            if (btn != null)
                btn.interactable = interactable;
        }
    }

    public void OnRemoveAdsClick()
    {
        Debug.Log("[ThemeSelectionManager] Remove AD button clicked.");
        UIController ui = FindObjectOfType<UIController>(true);
        if (ui != null)
            ui.OnRemoveAdsClick();
        else
        {
            Debug.LogWarning("[ThemeSelectionManager] UIController not found; purchasing NoAds directly.");
            AdManager.PurchaseNoAds();
        }
    }

    private void EnsureThemeVisualAssets()
    {
        if (lockIconSprite == null) lockIconSprite = Resources.Load<Sprite>("ThemeUI/Lock");
        if (coinPillSprite == null) coinPillSprite = Resources.Load<Sprite>("ThemeUI/Coin_Box");
        if (coinIconSprite == null) coinIconSprite = Resources.Load<Sprite>("ThemeUI/Coin");
        if (headerCoinPanelSprite == null) headerCoinPanelSprite = Resources.Load<Sprite>("ThemeUI/Coin_Panel");

        if (lockFont == null)
        {
            Transform searchRoot = GetSelectionRoot();
            foreach (TextMeshProUGUI tmp in searchRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.font != null && tmp.font.name.IndexOf("Freude", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    lockFont = tmp.font;
                    break;
                }
            }
            if (lockFont == null)
            {
                TextMeshProUGUI anyTmp = searchRoot.GetComponentInChildren<TextMeshProUGUI>(true);
                if (anyTmp != null) lockFont = anyTmp.font;
            }
        }
    }

    private void CreateRuntimeCoinsBadge()
    {
        EnsureThemeVisualAssets();

        Transform searchRoot = GetSelectionRoot();
        Transform targetParent = searchRoot.Find("Image") ?? searchRoot;

        GameObject badge = new GameObject("RuntimeCoinsBadge", typeof(RectTransform), typeof(Image));
        badge.transform.SetParent(targetParent, false);
        badge.transform.SetAsLastSibling();
        RectTransform rt = badge.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-28f, -40f);
        rt.sizeDelta = new Vector2(220f, 68f);

        Image bgImg = badge.GetComponent<Image>();
        if (headerCoinPanelSprite != null)
        {
            bgImg.sprite = headerCoinPanelSprite;
            bgImg.color = Color.white;
            bgImg.type = Image.Type.Sliced;
        }
        else
        {
            bgImg.color = new Color(0.05f, 0.10f, 0.28f, 0.88f);
        }

        if (coinIconSprite != null)
        {
            GameObject iconObj = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(badge.transform, false);
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.05f, 0.12f);
            iconRt.anchorMax = new Vector2(0.32f, 0.88f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            Image cImg = iconObj.GetComponent<Image>();
            cImg.sprite = coinIconSprite;
            cImg.preserveAspect = true;
            cImg.raycastTarget = false;
        }

        GameObject txtObj = new GameObject("CoinsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(badge.transform, false);
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = coinIconSprite != null ? new Vector2(0.32f, 0f) : Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(6f, 0f);
        txtRt.offsetMax = new Vector2(-14f, 0f);
        coinsText = txtObj.GetComponent<TextMeshProUGUI>();
        if (lockFont != null) coinsText.font = lockFont;
        coinsText.fontSize = 34;
        coinsText.fontStyle = FontStyles.Bold;
        coinsText.color = Color.white;
        coinsText.alignment = TextAlignmentOptions.Center;
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
                return child;
            Transform nested = FindChildRecursive(child, targetName);
            if (nested != null) return nested;
        }
        return null;
    }

    private Coroutine coinCollectRoutine;
    private bool isPlayingCoinRewardAnim;

    private void UpdateCoinsUI(int currentCoins)
    {
        if (isPlayingCoinRewardAnim)
            return;

        if (coinsText != null)
            coinsText.text = currentCoins.ToString();
    }

    private void StartCoinCollectAnimation(int startCoins, int targetCoins)
    {
        if (coinCollectRoutine != null)
            StopCoroutine(coinCollectRoutine);

        coinCollectRoutine = StartCoroutine(PlayCoinCollectAnimation(startCoins, targetCoins));
    }

    private IEnumerator PlayCoinCollectAnimation(int startCoins, int targetCoins)
    {
        isPlayingCoinRewardAnim = true;
        EnsureThemeVisualAssets();

        if (coinsText == null)
        {
            isPlayingCoinRewardAnim = false;
            yield break;
        }

        Transform badgeTransform = coinsText.transform.parent != null ? coinsText.transform.parent : coinsText.transform;
        Transform animParent = GetSelectionRoot();
        Vector3 startWorldPos = watchAdCoinsButton != null ? watchAdCoinsButton.transform.position : badgeTransform.position + Vector3.down * 400f;
        Vector3 targetWorldPos = badgeTransform.position;

        const int coinCount = 10;
        const float spawnStagger = 0.045f;
        const float flyDuration = 0.52f;
        int arrivedCount = 0;

        for (int i = 0; i < coinCount; i++)
        {
            GameObject coinObj = new GameObject("FlyingCoin_" + i, typeof(RectTransform), typeof(Image));
            coinObj.transform.SetParent(animParent, false);
            coinObj.transform.SetAsLastSibling();

            RectTransform rt = coinObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(56f, 56f);

            Vector3 burstOffset = new Vector3(
                UnityEngine.Random.Range(-70f, 70f),
                UnityEngine.Random.Range(-25f, 55f),
                0f);
            rt.position = startWorldPos;

            Image img = coinObj.GetComponent<Image>();
            if (coinIconSprite != null)
                img.sprite = coinIconSprite;
            else
                img.color = new Color(1f, 0.82f, 0.15f, 1f);
            img.preserveAspect = true;
            img.raycastTarget = false;

            StartCoroutine(AnimateSingleFlyingCoin(
                rt,
                startWorldPos,
                startWorldPos + burstOffset,
                badgeTransform,
                flyDuration,
                () =>
                {
                    arrivedCount++;
                    float progress = Mathf.Clamp01((float)arrivedCount / coinCount);
                    int displayValue = Mathf.RoundToInt(Mathf.Lerp(startCoins, targetCoins, progress));
                    if (coinsText != null)
                        coinsText.text = displayValue.ToString();
                    StartCoroutine(PulseCoinBadge(badgeTransform));
                }));

            yield return new WaitForSecondsRealtime(spawnStagger);
        }

        float timeout = flyDuration + 0.35f;
        float waitElapsed = 0f;
        while (arrivedCount < coinCount && waitElapsed < timeout)
        {
            waitElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        isPlayingCoinRewardAnim = false;
        if (coinsText != null)
            coinsText.text = CoinManager.Coins.ToString();
        coinCollectRoutine = null;
    }

    private IEnumerator AnimateSingleFlyingCoin(
        RectTransform coinRt,
        Vector3 originPos,
        Vector3 burstPos,
        Transform targetTransform,
        float duration,
        System.Action onArrived)
    {
        if (coinRt == null)
        {
            onArrived?.Invoke();
            yield break;
        }

        // Phase 1: quick burst outward
        float burstDuration = 0.14f;
        float elapsed = 0f;
        while (elapsed < burstDuration && coinRt != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / burstDuration);
            float easeOut = 1f - (1f - t) * (1f - t);
            coinRt.position = Vector3.Lerp(originPos, burstPos, easeOut);
            coinRt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.05f, easeOut);
            yield return null;
        }

        // Phase 2: curve into the coin badge
        elapsed = 0f;
        Vector3 endPos = targetTransform != null ? targetTransform.position : burstPos;
        Vector3 controlPos = (burstPos + endPos) * 0.5f + new Vector3(UnityEngine.Random.Range(-120f, 120f), 80f, 0f);

        while (elapsed < duration && coinRt != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = t * t * (3f - 2f * t);

            if (targetTransform != null)
                endPos = targetTransform.position;

            // Quadratic Bezier curve
            float u = 1f - ease;
            Vector3 pos = (u * u) * burstPos + (2f * u * ease) * controlPos + (ease * ease) * endPos;
            coinRt.position = pos;
            coinRt.localScale = Vector3.one * Mathf.Lerp(1.05f, 0.7f, ease);
            yield return null;
        }

        if (coinRt != null)
            Destroy(coinRt.gameObject);

        onArrived?.Invoke();
    }

    private IEnumerator PulseCoinBadge(Transform badgeTransform)
    {
        if (badgeTransform == null) yield break;

        badgeTransform.localScale = Vector3.one * 1.14f;
        float elapsed = 0f;
        const float duration = 0.12f;
        while (elapsed < duration && badgeTransform != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            badgeTransform.localScale = Vector3.Lerp(Vector3.one * 1.14f, Vector3.one, t);
            yield return null;
        }

        if (badgeTransform != null)
            badgeTransform.localScale = Vector3.one;
    }

    private void UpdateRemoveAdsButton()
    {
        bool show = !AdManager.IsNoAdsPurchased;
        foreach (Button btn in allRemoveAdsButtons)
        {
            if (btn != null)
                btn.gameObject.SetActive(show);
        }
    }

    public bool IsItemUnlocked(ThemeCategory category, int index)
    {
        if (IsFreeIndex(category, index))
            return true;

        GetUnlockRequirement(category, index, out bool isLevelLocked, out int value);
        if (isLevelLocked)
        {
            int playerDisplayLevel = GameManager.Instance != null ? GameManager.Instance.level + 1 : 1;
            return playerDisplayLevel >= value;
        }

        return PlayerPrefs.GetInt(GetUnlockKey(category, index), 0) == 1;
    }

    private void GetUnlockRequirement(ThemeCategory category, int index, out bool isLevelLocked, out int value)
    {
        if (category == ThemeCategory.Bottle && index == bottleLevelUnlockIndex)
        {
            isLevelLocked = true;
            value = bottleRequiredLevel;
            return;
        }

        if (category == ThemeCategory.Background && index == bgLevelUnlockIndex)
        {
            isLevelLocked = true;
            value = bgRequiredLevel;
            return;
        }

        isLevelLocked = false;
        value = defaultCoinCost;
    }

    private static void UnlockItem(ThemeCategory category, int index)
    {
        PlayerPrefs.SetInt(GetUnlockKey(category, index), 1);
        PlayerPrefs.Save();
    }

    private static string GetUnlockKey(ThemeCategory category, int index)
    {
        return $"Unlocked_{category}_{index}";
    }

    private bool IsFreeIndex(ThemeCategory category, int index)
    {
        if (!freeIndicesByCategory.TryGetValue(category, out HashSet<int> freeSet))
        {
            freeSet = ComputeVisualTopRowFreeIndices(GetItemsForCategory(category));
            freeIndicesByCategory[category] = freeSet;
        }
        return freeSet.Contains(index);
    }

    private List<ThemeItemUI> GetItemsForCategory(ThemeCategory category)
    {
        switch (category)
        {
            case ThemeCategory.Background: return bgItems;
            case ThemeCategory.Bottle: return bottleItems;
            case ThemeCategory.Color: return colorItems;
            default: return null;
        }
    }

    private HashSet<int> ComputeVisualTopRowFreeIndices(List<ThemeItemUI> items)
    {
        HashSet<int> freeSet = new HashSet<int>();
        if (items == null || items.Count == 0) return freeSet;

        RectTransform parentRt = items[0] != null ? items[0].transform.parent as RectTransform : null;
        if (parentRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);

        List<(int idx, Vector2 pos)> positioned = new List<(int, Vector2)>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;
            RectTransform rt = items[i].GetComponent<RectTransform>();
            Vector2 pos = rt != null ? rt.anchoredPosition : Vector2.zero;
            positioned.Add((i, pos));
        }

        // Sort top-to-bottom (Y descending), then left-to-right (X ascending)
        positioned.Sort((a, b) =>
        {
            if (Mathf.Abs(a.pos.y - b.pos.y) > 15f)
                return b.pos.y.CompareTo(a.pos.y);
            return a.pos.x.CompareTo(b.pos.x);
        });

        int take = Mathf.Min(freeItemsPerCategory, positioned.Count);
        for (int i = 0; i < take; i++)
            freeSet.Add(positioned[i].idx);

        return freeSet;
    }

    public void RefreshAllLockStates()
    {
        freeIndicesByCategory.Clear();
        RefreshCategoryLockStates(ThemeCategory.Background, bgItems);
        RefreshCategoryLockStates(ThemeCategory.Bottle, bottleItems);
        RefreshCategoryLockStates(ThemeCategory.Color, colorItems);
    }

    private void RefreshCategoryLockStates(ThemeCategory category, List<ThemeItemUI> items)
    {
        if (items == null) return;
        EnsureThemeVisualAssets();

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;
            bool unlocked = IsItemUnlocked(category, i);
            GetUnlockRequirement(category, i, out bool isLevelLocked, out int value);
            items[i].SetLockState(unlocked, isLevelLocked, value, lockIconSprite, coinPillSprite, coinIconSprite, lockFont);
        }
    }

    private void PulseWatchAdButton()
    {
        if (watchAdCoinsButton == null || !watchAdCoinsButton.gameObject.activeInHierarchy) return;

        if (adButtonPulseRoutine != null)
            StopCoroutine(adButtonPulseRoutine);
        adButtonPulseRoutine = StartCoroutine(PulseButtonRoutine(watchAdCoinsButton.transform));
    }

    private IEnumerator PulseButtonRoutine(Transform target)
    {
        Vector3 orig = Vector3.one;
        float duration = 0.35f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.1f * (1f - t * 0.5f);
            target.localScale = orig * scale;
            yield return null;
        }

        target.localScale = orig;
        adButtonPulseRoutine = null;
    }

    // ── BACKGROUND ───────────────────────────────────────────────────────────────
    private void ApplyBG(int index, ThemeItemUI selectedItem)
    {
        if (currentBGIndex == index) return;
        if (bgItems == null || bgSprites == null) return;
        if (index < 0 || index >= bgItems.Count || index >= bgSprites.Count) return;

        Highlight(bgItems, selectedItem);
        if (gameplayBG != null) gameplayBG.sprite = bgSprites[index];
        SavePref("SelectedBG", index);
        currentBGIndex = index;
    }

    // ── BOTTLE THEME ─────────────────────────────────────────────────────────────
    private void ApplyBottle(int index, ThemeItemUI selectedItem)
    {
        if (currentBottleIndex == index) return;
        if (bottleItems == null || bottlePreviewSprites == null) return;
        if (index < 0 || index >= bottleItems.Count || index >= bottlePreviewSprites.Count) return;

        Highlight(bottleItems, selectedItem);
        if (gameplayBottle != null) gameplayBottle.sprite = bottlePreviewSprites[index];
        SavePref("SelectedBottle", index);
        currentBottleIndex = index;

        if (index < bottleUpSprites.Count && index < bottleFillSprites.Count)
        {
            if (bottleTop  != null) bottleTop.sprite  = bottleUpSprites[index];
            if (bottleFill != null) bottleFill.sprite = bottleFillSprites[index];
        }

        if (bottlesController != null &&
            index < bottleUpSprites.Count &&
            index < bottleFillSprites.Count)
        {
            bottlesController.ChangeBottleTheme(bottleUpSprites[index], bottleFillSprites[index], index);
        }
    }

    // ── COLOR PALETTE ─────────────────────────────────────────────────────────────
    // This is the core: pushes the exact colors from the selected card to all bottles.
    private void ApplyColorPalette(int index, ThemeItemUI selectedItem, bool forceApply)
    {
        if (!isInitialized)
            EnsureInitialized();

        if (colorItems == null || colorItems.Count == 0) return;

        // If selectedItem was clicked directly, use its exact index in colorItems
        if (selectedItem != null)
        {
            int itemIdx = colorItems.IndexOf(selectedItem);
            if (itemIdx >= 0) index = itemIdx;
        }

        index = Mathf.Clamp(index, 0, colorItems.Count - 1);
        if (selectedItem == null)
            selectedItem = colorItems[index];

        if (!forceApply && currentColorIndex == index) return;

        // Ensure palette for this card is extracted if not yet populated
        if (colorPaletteCards == null || index >= colorPaletteCards.Count || !HasValidColors(colorPaletteCards[index].colors))
        {
            EnsureInitialized();
        }

        if (colorPaletteCards == null || index >= colorPaletteCards.Count || colorPaletteCards[index].colors == null || colorPaletteCards[index].colors.Length == 0)
        {
            return;
        }

        // Highlight the card
        Highlight(colorItems, selectedItem);
        SavePref("SelectedColor", index);
        currentColorIndex = index;

        // Save index to GameManager so scene reloads restore correctly
        if (GameManager.Instance != null)
        {
            GameManager.Instance.selectedColorPalette = index;
            GameManager.Instance.SaveColorPallet();
        }

        // Push the colors to BottlesController → all BottleControllers
        if (bottlesController == null)
            bottlesController = FindObjectOfType<BottlesController>();

        if (bottlesController != null)
        {
            bottlesController.ChangeColorSystem();
        }
    }

    // ── Shared helpers ───────────────────────────────────────────────────────────
    private void Highlight(List<ThemeItemUI> items, ThemeItemUI selected)
    {
        foreach (var item in items)
        {
            if (item != null) item.SetSelected(false);
        }
        if (selected != null) selected.SetSelected(true);
    }

    private void SavePref(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }

    // ── Public helpers called by BottlesController on bottle spawn ───────────────
    public int GetCurrentBottleIndex() => currentBottleIndex;

    public Sprite GetBottleUpSprite(int index)
        => (index >= 0 && bottleUpSprites != null && index < bottleUpSprites.Count)
            ? bottleUpSprites[index] : null;

    public Sprite GetBottleFillSprite(int index)
        => (index >= 0 && bottleFillSprites != null && index < bottleFillSprites.Count)
            ? bottleFillSprites[index] : null;

    // Returns the colors for the currently selected palette
    // (called by BottleController.LoadColorsPallet on scene reload / bottle spawn)
    public Color[] GetCurrentPaletteColors()
    {
        if (!isInitialized || colorPaletteCards == null || colorPaletteCards.Count == 0)
            EnsureInitialized();

        if (colorPaletteCards == null || colorPaletteCards.Count == 0)
            return null;

        int idx = Mathf.Clamp(currentColorIndex, 0, colorPaletteCards.Count - 1);
        return colorPaletteCards[idx].colors;
    }
}
