using UnityEngine;
using UnityEngine.UI;

public class ThemeCategoryController : MonoBehaviour
{
    [Header("Scroll Views")]
    [SerializeField] private GameObject bottleScroll;
    [SerializeField] private GameObject bgScroll;
    [SerializeField] private GameObject colorScroll;

    [Header("Category Buttons")]
    [SerializeField] private RectTransform bottleBtn;
    [SerializeField] private RectTransform bgBtn;
    [SerializeField] private RectTransform colorBtn;

    [Header("Highlight")]
    [SerializeField] private RectTransform highlight;
    [SerializeField] private float highlightMoveSpeed = 15f;

    private RectTransform targetHighlight;

    [Header("Button Views")]
    [SerializeField] private CategoryButtonView bottleView;
    [SerializeField] private CategoryButtonView bgView;
    [SerializeField] private CategoryButtonView colorView;

    private void Start()
    {
        ShowCategory(ThemeCategory.Bottle);
        highlight.anchoredPosition = bottleBtn.anchoredPosition; // snap on start
        ResetData();
    }

    // ---------- Button Calls ----------
    public void SelectBottle()
{
    Debug.Log("Bottle CLICKED");
    ShowCategory(ThemeCategory.Bottle);
}

    public void SelectBackground()
    {
        ShowCategory(ThemeCategory.Background);
    }

    public void ResetData()
    {
        ResetScroll(bottleScroll);
        ResetScroll(bgScroll);
        ResetScroll(colorScroll);
    }

    public void SelectColor()
    {
        ShowCategory(ThemeCategory.Color);
    }

    private void ResetScroll(GameObject scrollObj)
    {
        if (scrollObj == null) return;

        ScrollRect scrollRect = scrollObj.GetComponent<ScrollRect>();

        if (scrollRect == null) return;

        Canvas.ForceUpdateCanvases();

        scrollRect.StopMovement();

        // Top position
        scrollRect.verticalNormalizedPosition = 1f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            scrollRect.content
        );
    }

    // ---------- Core Logic ----------
    private void ShowCategory(ThemeCategory category)
    {
        // Scroll views
        bottleScroll.SetActive(false);
        bgScroll.SetActive(false);
        colorScroll.SetActive(false);

        // Reset icons
        bottleView.SetSelected(false);
        bgView.SetSelected(false);
        colorView.SetSelected(false);

        switch (category)
        {
            case ThemeCategory.Bottle:
                bottleScroll.SetActive(true);
                bottleView.SetSelected(true);
                targetHighlight = bottleBtn;
                ResetScroll(bottleScroll);

                break;

            case ThemeCategory.Background:
                bgScroll.SetActive(true);
                bgView.SetSelected(true);
                targetHighlight = bgBtn;
                ResetScroll(bgScroll);
                break;

            case ThemeCategory.Color:
                colorScroll.SetActive(true);
                colorView.SetSelected(true);
                targetHighlight = colorBtn;
                ResetScroll(colorScroll);
                break;
        }
    }

    private void Update()
    {
        if (targetHighlight == null) return;

        highlight.anchoredPosition = Vector2.Lerp(
            highlight.anchoredPosition,
            targetHighlight.anchoredPosition,
            Time.deltaTime * highlightMoveSpeed
        );
    }
}

public enum ThemeCategory
{
    Bottle,
    Background,
    Color
}