using UnityEngine;
using UnityEngine.UI;

public class BGItemUI : MonoBehaviour
{
    [SerializeField] private Image frame;

    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;

    [SerializeField] private int index; // must match manager list

    // Optional: cache button (better performance/clean)
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (BGSelectionManager.Instance != null)
        {
            BGSelectionManager.Instance.SelectBG(index, this);
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (frame != null)
            frame.sprite = isSelected ? selectedSprite : normalSprite;
    }

    // ✅ (Optional but useful)
    public int GetIndex()
    {
        return index;
    }

    // ✅ (Optional: auto-assign index from manager if needed later)
    public void SetIndex(int i)
    {
        index = i;
    }
}