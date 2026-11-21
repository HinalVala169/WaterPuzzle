using UnityEngine;
using UnityEngine.UI;

public class Reload : MonoBehaviour
{
    private Button resetButton;
    private Image resetImage;
    private Color baseColor = Color.white;

    private void Awake()
    {
        resetButton = GetComponent<Button>();
        resetImage = GetComponent<Image>();
        if (resetImage != null)
            baseColor = resetImage.color;

        UpdateResetState();
    }

    private void Update()
    {
        UpdateResetState();
    }

    private void UpdateResetState()
    {
        bool hasMoves = GameController.Instance != null && GameController.Instance.HasMoves;
        if (resetButton != null)
            resetButton.interactable = hasMoves;

        if (resetImage != null)
        {
            Color c = baseColor;
            c.a = hasMoves ? baseColor.a : baseColor.a * 0.45f;
            resetImage.color = c;
        }
    }

    public void ReloadButton()
    {
        if (GameManager.Instance != null && GameManager.Instance.gamePause)
            return;

        if (GameController.Instance != null && !GameController.Instance.HasMoves)
            return;

        AudioManager.PlayUIClick();
        if (GameManager.Instance != null)
            GameManager.Instance.Reload();
    }
}
