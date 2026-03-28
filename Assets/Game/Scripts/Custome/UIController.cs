using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [Header("Canvases")]
    [SerializeField] private GameObject notificationCanvas;
    [SerializeField] private GameObject aboutCanvas;
    [SerializeField] private GameObject feedbackCanvas;
    [SerializeField] private GameObject removeAdCanvas;

    [Header("Sound Button")]
    [SerializeField] private Image soundImg;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;

    [Header("Vibration Button")]
    [SerializeField] private Image vibrationImg;
    [SerializeField] private Sprite vibrationOnSprite;
    [SerializeField] private Sprite vibrationOffSprite;

    [Header("Notification Button")]
    [SerializeField] private Image notificationImg;
    [SerializeField] private Sprite notificationOnSprite;
    [SerializeField] private Sprite notificationOffSprite;

    [Header("Bottle Top Button")]
    [SerializeField] private Image bottleTopImg;
    [SerializeField] private Sprite bottleTopOnSprite;
    [SerializeField] private Sprite bottleTopOffSprite;

    private bool isSoundOn;
    private bool isVibrationOn;
    private bool isNotificationOn;
    private bool isBottleTopOn;

    private void Start()
    {
        isSoundOn = PlayerPrefs.GetInt("Sound", 1) == 1;
        isVibrationOn = PlayerPrefs.GetInt("Vibration", 1) == 1;
        isNotificationOn = PlayerPrefs.GetInt("Notification", 1) == 1;
        isBottleTopOn = PlayerPrefs.GetInt("BottleTop", 1) == 1;
        UpdateUI();
    }

    #region Toggle Buttons

    public static bool IsBottleTopEnabled
    {
        get
        {
            return PlayerPrefs.GetInt("BottleTop", 1) == 1;
        }
    }

    public void OnSoundClick()
    {
        isSoundOn = !isSoundOn;
        PlayerPrefs.SetInt("Sound", isSoundOn ? 1 : 0);
        UpdateUI();
    }

    public void OnVibrationClick()
    {
        isVibrationOn = !isVibrationOn;
        PlayerPrefs.SetInt("Vibration", isVibrationOn ? 1 : 0);
        UpdateUI();
    }

    public void OnNotificationClick()
    {
        isNotificationOn = !isNotificationOn;
        PlayerPrefs.SetInt("Notification", isNotificationOn ? 1 : 0);

        CloseAllPopups();
        notificationCanvas.SetActive(true);

        UpdateUI();
    }

    public void OnBottleTopClick()
    {
        isBottleTopOn = !isBottleTopOn;
        PlayerPrefs.SetInt("BottleTop", isBottleTopOn ? 1 : 0);

        UpdateUI();
    }

    public void OnAboutClick()
    {
        CloseAllPopups();
        aboutCanvas.SetActive(true);
    }

    public void OnFeedbackClick()
    {
        CloseAllPopups();
        feedbackCanvas.SetActive(true);
    }

    public void OnRemoveAdsClick()
    {
        CloseAllPopups();
        removeAdCanvas.SetActive(true);
    }

    #endregion

    #region UI Update

    private void UpdateUI()
    {
        soundImg.sprite = isSoundOn ? soundOnSprite : soundOffSprite;
        vibrationImg.sprite = isVibrationOn ? vibrationOnSprite : vibrationOffSprite;
        notificationImg.sprite = isNotificationOn ? notificationOnSprite : notificationOffSprite;
        bottleTopImg.sprite = isBottleTopOn ? bottleTopOnSprite : bottleTopOffSprite; // ✅ THIS LINE
    }

    #endregion

    #region Close

    public void CloseAllPopups()
    {
        notificationCanvas.SetActive(false);
        aboutCanvas.SetActive(false);
        feedbackCanvas.SetActive(false);
        removeAdCanvas.SetActive(false);
    }

    public void ClosePopup(GameObject panel)
    {
        Debug.Log("closeee");
        panel.SetActive(false);
    }

    #endregion
}