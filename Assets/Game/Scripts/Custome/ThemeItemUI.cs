using UnityEngine;
using UnityEngine.UI;

public class ThemeItemUI : MonoBehaviour
{
    [SerializeField] private Image frame;

    // frame sprites for selected / unselected state
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;

    // preview image inside the card (optional – used for colour-palette cards)
    [SerializeField] private Image previewImage;

    [SerializeField] private int index;
    [SerializeField] private ThemeCategory category;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (ThemeSelectionManager.Instance != null)
        {
            ThemeSelectionManager.Instance.SelectItem(category, index, this);
        }
    }

    // ── Setters ──────────────────────────────────────────────────

    public void SetSelected(bool isSelected)
    {
        if (frame != null)
            frame.sprite = isSelected ? selectedSprite : normalSprite;
    }

    /// <summary>
    /// Swaps the small preview thumbnail shown on the card.
    /// Used by ThemeSelectionManager to auto-populate colour cards from the database.
    /// </summary>
    public void SetPreviewSprite(Sprite sprite)
    {
        if (previewImage != null)
            previewImage.sprite = sprite;
    }

    // ── Getters / setters ────────────────────────────────────────

    public int GetIndex()       => index;
    public void SetIndex(int i) => index = i;
}
