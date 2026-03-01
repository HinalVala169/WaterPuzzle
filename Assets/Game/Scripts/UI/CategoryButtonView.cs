using UnityEngine;
using UnityEngine.UI;

public class CategoryButtonView : MonoBehaviour
{
    [SerializeField] private GameObject blueIcon;
    [SerializeField] private GameObject whiteIcon;

    public void SetSelected(bool selected)
    {
        whiteIcon.SetActive(selected);
        blueIcon.SetActive(!selected);
    }
}