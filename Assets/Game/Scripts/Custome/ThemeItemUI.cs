using UnityEngine;
using UnityEngine.UI;

public class ThemeItemUI : MonoBehaviour
{
    [SerializeField] private Image frame;

    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;

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
            ThemeSelectionManager.Instance.SelectItem(
                category,
                index,
                this
            );
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (frame != null)
        {
            frame.sprite = isSelected
                ? selectedSprite
                : normalSprite;
        }
    }

    public int GetIndex()
    {
        return index;
    }

    public void SetIndex(int i)
    {
        index = i;
    }
}