using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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

        // Restore saved selections
        SetDefaultSelections();
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
        List<Color> rawColors = new List<Color>();
        List<Color> uniqueColors = new List<Color>();

        foreach (var img in liquidImages)
        {
            Material mat = img.material;
            foreach (string prop in shaderProps)
            {
                if (mat.HasProperty(prop))
                {
                    Color c = mat.GetColor(prop);
                    c.a = 1f;
                    rawColors.Add(c);

                    if (!ContainsSimilarColor(uniqueColors, c))
                    {
                        uniqueColors.Add(c);
                    }
                }
            }
        }

        if (uniqueColors.Count == 0) return null;

        // Build the 12-slot palette using ONLY the colors present on this card,
        // placing unique colors into the exact slot indices LevelBuilder uses first.
        Color[] palette = new Color[12];
        int uCount = uniqueColors.Count;

        for (int p = 0; p < LevelBuilderSlotPriority.Length; p++)
        {
            int slot = LevelBuilderSlotPriority[p];
            palette[slot] = uniqueColors[p % uCount];
        }

        // Ensure slot 3 (used in Level 0 with slot 0, and Level 1 with slots 0 & 5) is distinct from slots 0 and 5
        if (uCount <= 9 && uCount >= 2)
        {
            palette[3] = uniqueColors[1 % uCount];
        }

        return palette;
    }

    private static bool ContainsSimilarColor(List<Color> list, Color candidate)
    {
        const float threshold = 0.02f;
        for (int i = 0; i < list.Count; i++)
        {
            Color existing = list[i];
            if (Mathf.Abs(existing.r - candidate.r) < threshold &&
                Mathf.Abs(existing.g - candidate.g) < threshold &&
                Mathf.Abs(existing.b - candidate.b) < threshold)
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
        foreach (var item in bgItems) item.SetSelected(false);
        currentBGIndex = -1;
        ApplyBG(idx, bgItems[idx]);
    }

    private void RestoreDefaultBottle()
    {
        if (bottleItems == null || bottleItems.Count == 0) return;
        int idx = Mathf.Clamp(PlayerPrefs.GetInt("SelectedBottle", 0), 0, bottleItems.Count - 1);
        foreach (var item in bottleItems) item.SetSelected(false);
        currentBottleIndex = -1;
        ApplyBottle(idx, bottleItems[idx]);
    }

    private void RestoreDefaultColor()
    {
        if (colorItems == null || colorItems.Count == 0) return;
        int idx = Mathf.Clamp(PlayerPrefs.GetInt("SelectedColor", 0), 0, colorItems.Count - 1);
        foreach (var item in colorItems) item.SetSelected(false);
        // Force apply on startup
        ApplyColorPalette(idx, colorItems[idx], forceApply: true);
    }

    // ── Public entry point called by ThemeItemUI.OnClick() ──────────────────────
    public void SelectItem(ThemeCategory category, int index, ThemeItemUI selectedItem)
    {
        switch (category)
        {
            case ThemeCategory.Background: ApplyBG(index, selectedItem);                        break;
            case ThemeCategory.Bottle:     ApplyBottle(index, selectedItem);                    break;
            case ThemeCategory.Color:      ApplyColorPalette(index, selectedItem, false); break;
        }
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
            bottlesController.ChangeBottleTheme(bottleUpSprites[index], bottleFillSprites[index]);
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
