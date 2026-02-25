using UnityEngine;

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

    private void Start()
    {
        ShowCategory(ThemeCategory.Bottle);
        highlight.position = bottleBtn.position; // snap on start
    }

    // ---------- Button Calls ----------
    public void SelectBottle()
    {
        ShowCategory(ThemeCategory.Bottle);
    }

    public void SelectBackground()
    {
        ShowCategory(ThemeCategory.Background);
    }

    public void SelectColor()
    {
        ShowCategory(ThemeCategory.Color);
    }

    // ---------- Core Logic ----------
    private void ShowCategory(ThemeCategory category)
    {
        bottleScroll.SetActive(false);
        bgScroll.SetActive(false);
        colorScroll.SetActive(false);

        switch (category)
        {
            case ThemeCategory.Bottle:
                bottleScroll.SetActive(true);
                targetHighlight = bottleBtn;
                break;

            case ThemeCategory.Background:
                bgScroll.SetActive(true);
                targetHighlight = bgBtn;
                break;

            case ThemeCategory.Color:
                colorScroll.SetActive(true);
                targetHighlight = colorBtn;
                break;
        }
    }

    private void Update()
    {
        if (targetHighlight == null) return;

        highlight.position = Vector3.Lerp(
            highlight.position,
            targetHighlight.position,
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