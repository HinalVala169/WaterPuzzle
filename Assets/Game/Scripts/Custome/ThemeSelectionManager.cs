using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ThemeSelectionManager : MonoBehaviour
{
    public static ThemeSelectionManager Instance;

    [Header("Gameplay Preview")]
    [SerializeField] private Image gameplayBG;
    [SerializeField] private Image gameplayBottle;

    [Header("Sprites")]
    [SerializeField] private List<Sprite> bgSprites;

    // preview sprite in selection UI
    [SerializeField] private List<Sprite> bottlePreviewSprites;

    // actual gameplay bottle sprites
    [SerializeField] private List<Sprite> bottleUpSprites;
    [SerializeField] private List<Sprite> bottleFillSprites;

    [SerializeField] private List<Sprite> colorSprites;

    [Header("UI Items")]
    [SerializeField] private List<ThemeItemUI> bgItems;
    [SerializeField] private List<ThemeItemUI> bottleItems;
    [SerializeField] private List<ThemeItemUI> colorItems;

    private int currentBGIndex = -1;
    private int currentBottleIndex = -1;
    private int currentColorIndex = -1;

    [Header("Preview Bottle")]
    [SerializeField] private Image bottleTop;
    [SerializeField] private Image bottleFill;

    [Header("References")]
    [SerializeField] private BottlesController bottlesController;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetDefaultSelections();
    }

    private void SetDefaultSelections()
    {
        SelectDefault(
            ThemeCategory.Background,
            "SelectedBG",
            bgItems,
            bgSprites
        );

        SelectDefault(
            ThemeCategory.Bottle,
            "SelectedBottle",
            bottleItems,
            bottlePreviewSprites
        );

        SelectDefault(
            ThemeCategory.Color,
            "SelectedColor",
            colorItems,
            colorSprites
        );
    }

    private void SelectDefault(
        ThemeCategory category,
        string saveKey,
        List<ThemeItemUI> items,
        List<Sprite> sprites)
    {
        if (items.Count == 0 || sprites.Count == 0)
            return;

        int defaultIndex = PlayerPrefs.GetInt(saveKey, 0);

        if (defaultIndex < 0 || defaultIndex >= items.Count)
            defaultIndex = 0;

        foreach (var item in items)
        {
            item.SetSelected(false);
        }

        SelectItem(category, defaultIndex, items[defaultIndex]);
    }

    public void SelectItem(
        ThemeCategory category,
        int index,
        ThemeItemUI selectedItem)
    {
        switch (category)
        {
            case ThemeCategory.Background:

                SelectCategory(
                    index,
                    selectedItem,
                    bgItems,
                    bgSprites,
                    gameplayBG,
                    ref currentBGIndex,
                    "SelectedBG"
                );

                break;

            case ThemeCategory.Bottle:

                SelectCategory(
                    index,
                    selectedItem,
                    bottleItems,
                    bottlePreviewSprites,
                    gameplayBottle,
                    ref currentBottleIndex,
                    "SelectedBottle"
                );

                // Update preview bottle
                SetBottlePreview(index);

                // Update gameplay bottles
                if (bottlesController != null &&
                    index >= 0 &&
                    index < bottleUpSprites.Count &&
                    index < bottleFillSprites.Count)
                {
                    bottlesController.ChangeBottleTheme(
                        bottleUpSprites[index],
                        bottleFillSprites[index]
                    );
                }

                break;

            case ThemeCategory.Color:

                SelectCategory(
                    index,
                    selectedItem,
                    colorItems,
                    colorSprites,
                    gameplayBottle,
                    ref currentColorIndex,
                    "SelectedColor"
                );

                break;
        }
    }

    private void SetBottlePreview(int index)
    {
        if (index < 0 ||
            index >= bottleUpSprites.Count ||
            index >= bottleFillSprites.Count)
            return;

        if (bottleTop != null)
        {
            bottleTop.sprite =
                bottleUpSprites[index];
        }

        if (bottleFill != null)
        {
            bottleFill.sprite =
                bottleFillSprites[index];
        }
    }

    public int GetCurrentBottleIndex()
    {
        return currentBottleIndex;
    }

    private void SelectCategory(
        int index,
        ThemeItemUI selectedItem,
        List<ThemeItemUI> items,
        List<Sprite> sprites,
        Image targetImage,
        ref int currentIndex,
        string saveKey)
    {
        if (currentIndex == index)
            return;

        if (index < 0 ||
            index >= items.Count ||
            index >= sprites.Count)
            return;

        foreach (var item in items)
        {
            item.SetSelected(false);
        }

        selectedItem.SetSelected(true);

        if (targetImage != null)
        {
            targetImage.sprite =
                sprites[index];
        }

        PlayerPrefs.SetInt(saveKey, index);
        PlayerPrefs.Save();

        currentIndex = index;
    }

    public Sprite GetBottleUpSprite(int index)
    {
        if (index >= 0 &&
            index < bottleUpSprites.Count)
            return bottleUpSprites[index];

        return null;
    }

    public Sprite GetBottleFillSprite(int index)
    {
        if (index >= 0 &&
            index < bottleFillSprites.Count)
            return bottleFillSprites[index];

        return null;
    }
}