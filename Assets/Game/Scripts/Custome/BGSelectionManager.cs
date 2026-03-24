using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BGSelectionManager : MonoBehaviour
{
    public static BGSelectionManager Instance;

    [SerializeField] private Image gameplayBG;
    [SerializeField] private List<Sprite> bgSprites; // 30 BGs
    [SerializeField] private List<BGItemUI> items;   // assign manually

    private int currentIndex = -1;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetDefaultSelection();
    }

    void SetDefaultSelection()
    {
        if (items.Count == 0 || bgSprites.Count == 0)
            return;

        int defaultIndex = PlayerPrefs.GetInt("SelectedBG", 0);

        // ✅ Safety checks
        if (defaultIndex < 0 || defaultIndex >= items.Count || defaultIndex >= bgSprites.Count)
            defaultIndex = 0;

        SelectBG(defaultIndex, items[defaultIndex]);
    }

    public void SelectBG(int index, BGItemUI selectedItem)
    {
        // ✅ Prevent unnecessary re-selection
        if (currentIndex == index)
            return;

        // ✅ Safety check
        if (index < 0 || index >= items.Count || index >= bgSprites.Count)
            return;

        // Reset all
        foreach (var item in items)
            item.SetSelected(false);

        // Set selected
        selectedItem.SetSelected(true);

        // Apply BG
        gameplayBG.sprite = bgSprites[index];

        // Save selection
        PlayerPrefs.SetInt("SelectedBG", index);
        PlayerPrefs.Save();

        currentIndex = index;
    }
}