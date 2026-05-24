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
    [SerializeField] private List<Sprite> bottleSprites;
    [SerializeField] private List<Sprite> colorSprites;

    [Header("UI Items")]
    [SerializeField] private List<ThemeItemUI> bgItems;
    [SerializeField] private List<ThemeItemUI> bottleItems;
    [SerializeField] private List<ThemeItemUI> colorItems;

    private int currentBGIndex = -1;
    private int currentBottleIndex = -1;
    private int currentColorIndex = -1;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Invoke(nameof(SetDefaultSelections), 0.1f);
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
            bottleSprites
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

        // Reset selection first
        foreach (var item in items)
        {
            item.SetSelected(false);
        }

        // Force first selection
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
                    bottleSprites,
                    gameplayBottle,
                    ref currentBottleIndex,
                    "SelectedBottle"
                );
                break;

            case ThemeCategory.Color:
                SelectCategory(
                    index,
                    selectedItem,
                    colorItems,
                    colorSprites,
                    gameplayBottle, // apply on bottle if needed
                    ref currentColorIndex,
                    "SelectedColor"
                );
                break;
        }
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

        if (index < 0 || index >= items.Count || index >= sprites.Count)
            return;

        foreach (var item in items)
        {
            item.SetSelected(false);
        }

        selectedItem.SetSelected(true);

        if (targetImage != null)
        {
            targetImage.sprite = sprites[index];
        }

        PlayerPrefs.SetInt(saveKey, index);
        PlayerPrefs.Save();

        currentIndex = index;
    }
}