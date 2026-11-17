using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [Header("Canvases")]
    [SerializeField] private GameObject notificationCanvas;
    [SerializeField] private GameObject aboutCanvas;
    [SerializeField] private GameObject feedbackCanvas;
    [SerializeField] private GameObject removeAdCanvas;
    [SerializeField] private GameObject languageCanvas;

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

    [Header("Language & Links")]
    [SerializeField] private Button languageButton;
    [SerializeField] private string supportEmail = "support@example.com";
    [SerializeField] private string privacyPolicyUrl = "https://example.com/privacy";
    [SerializeField] private string userRightsUrl = "https://example.com/user-rights";

    private bool isSoundOn;
    private bool isVibrationOn;
    private bool isNotificationOn;
    private bool isBottleTopOn;

    private TMP_InputField feedbackInputField;
    private static readonly string[] SupportedLanguages =
    {
        "English", "Español", "Français", "Deutsch", "Português", "हिन्दी"
    };
    private TextMeshProUGUI[] languageCheckmarks;

    private void Start()
    {
        isSoundOn = PlayerPrefs.GetInt("Sound", 1) == 1;
        isVibrationOn = PlayerPrefs.GetInt("Vibration", 1) == 1;
        isNotificationOn = PlayerPrefs.GetInt("Notification", 1) == 1;
        isBottleTopOn = PlayerPrefs.GetInt("BottleTop", 1) == 1;

        WireButtons();
        WireNotificationPopup();
        WireFeedbackPopup();
        WireAboutPopup();
        ApplyAudioState();
        ApplyBottleTopToCompletedBottles();
        UpdateUI();
    }

    private void WireButtons()
    {
        EnsureButtonClick(soundImg, OnSoundClick);
        EnsureButtonClick(vibrationImg, OnVibrationClick);
        EnsureButtonClick(notificationImg, OnNotificationClick);
        EnsureButtonClick(bottleTopImg, OnBottleTopClick);

        if (languageButton == null)
        {
            foreach (Button btn in GetComponentsInChildren<Button>(true))
            {
                if (btn.gameObject.name.Contains("Language"))
                {
                    languageButton = btn;
                    break;
                }
            }
        }

        if (languageButton != null && languageButton.onClick.GetPersistentEventCount() == 0)
        {
            languageButton.onClick.RemoveListener(OnLanguageClick);
            languageButton.onClick.AddListener(OnLanguageClick);
        }
    }

    private void EnsureButtonClick(Image targetImg, UnityAction action)
    {
        if (targetImg == null) return;

        Button btn = targetImg.GetComponent<Button>();
        if (btn == null)
        {
            btn = targetImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = targetImg;
        }

        if (btn.onClick.GetPersistentEventCount() == 0)
        {
            btn.onClick.RemoveListener(action);
            btn.onClick.AddListener(action);
        }
    }

    private void WireFeedbackPopup()
    {
        if (feedbackCanvas == null) return;

        feedbackInputField = feedbackCanvas.GetComponentInChildren<TMP_InputField>(true);
        if (feedbackInputField == null)
        {
            TextMeshProUGUI placeholderTmp = null;
            foreach (TextMeshProUGUI tmp in feedbackCanvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.text.IndexOf("Describe", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    placeholderTmp = tmp;
                    break;
                }
            }

            if (placeholderTmp != null && placeholderTmp.transform.parent != null)
            {
                GameObject boxObj = placeholderTmp.transform.parent.gameObject;
                Image boxImg = boxObj.GetComponent<Image>();
                if (boxImg == null)
                {
                    boxImg = boxObj.AddComponent<Image>();
                    boxImg.color = new Color(1f, 1f, 1f, 0.01f);
                }
                boxImg.raycastTarget = true;

                // Create actual input text sibling matching placeholder layout
                GameObject textObj = new GameObject("InputText", typeof(RectTransform));
                textObj.transform.SetParent(boxObj.transform, false);
                RectTransform textRt = textObj.GetComponent<RectTransform>();
                RectTransform phRt = placeholderTmp.rectTransform;
                textRt.anchorMin = phRt.anchorMin;
                textRt.anchorMax = phRt.anchorMax;
                textRt.pivot = phRt.pivot;
                textRt.anchoredPosition = phRt.anchoredPosition;
                textRt.sizeDelta = phRt.sizeDelta;

                TextMeshProUGUI inputTmp = textObj.AddComponent<TextMeshProUGUI>();
                inputTmp.font = placeholderTmp.font;
                inputTmp.fontSize = placeholderTmp.fontSize;
                inputTmp.color = new Color(0.15f, 0.15f, 0.2f, 1f);
                inputTmp.alignment = placeholderTmp.alignment;
                inputTmp.textWrappingMode = TextWrappingModes.Normal;
                inputTmp.raycastTarget = false;

                placeholderTmp.raycastTarget = false;

                feedbackInputField = boxObj.AddComponent<TMP_InputField>();
                feedbackInputField.targetGraphic = boxImg;
                feedbackInputField.textViewport = boxObj.GetComponent<RectTransform>();
                feedbackInputField.textComponent = inputTmp;
                feedbackInputField.placeholder = placeholderTmp;
                feedbackInputField.lineType = TMP_InputField.LineType.MultiLineNewline;
                feedbackInputField.characterLimit = 500;
            }
        }

        foreach (Button btn in feedbackCanvas.GetComponentsInChildren<Button>(true))
        {
            if (btn.gameObject.name.IndexOf("Submit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (btn.onClick.GetPersistentEventCount() == 0)
                {
                    btn.onClick.RemoveListener(OnFeedbackSubmit);
                    btn.onClick.AddListener(OnFeedbackSubmit);
                }
                break;
            }
        }
    }

    private void WireAboutPopup()
    {
        if (aboutCanvas == null) return;

        foreach (TextMeshProUGUI tmp in aboutCanvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string label = tmp.text.Trim();
            if (label.Equals("Privacy Policy", StringComparison.OrdinalIgnoreCase))
            {
                AttachRowClick(tmp, () => OpenSafeUrl(privacyPolicyUrl));
            }
            else if (label.Equals("User Rights", StringComparison.OrdinalIgnoreCase))
            {
                AttachRowClick(tmp, () => OpenSafeUrl(userRightsUrl));
            }
        }
    }

    private void AttachRowClick(TextMeshProUGUI tmp, UnityAction action)
    {
        if (tmp == null) return;

        Button existingBtn = tmp.GetComponentInParent<Button>(true);
        if (existingBtn != null)
        {
            if (existingBtn.onClick.GetPersistentEventCount() == 0)
            {
                existingBtn.onClick.RemoveListener(action);
                existingBtn.onClick.AddListener(action);
            }
            return;
        }

        tmp.raycastTarget = true;
        Button btn = tmp.GetComponent<Button>();
        if (btn == null)
        {
            btn = tmp.gameObject.AddComponent<Button>();
        }

        if (btn != null)
        {
            btn.targetGraphic = tmp;
            btn.onClick.RemoveListener(action);
            btn.onClick.AddListener(action);
        }
    }

    private void OpenSafeUrl(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
        }
    }

    #region Toggle Buttons

    public static bool IsSoundEnabled => PlayerPrefs.GetInt("Sound", 1) == 1;
    public static bool IsVibrationEnabled => PlayerPrefs.GetInt("Vibration", 1) == 1;
    public static bool IsNotificationEnabled => PlayerPrefs.GetInt("Notification", 1) == 1;
    public static string CurrentLanguage => PlayerPrefs.GetString("Language", "English");

    public static bool IsBottleTopEnabled
    {
        get
        {
            return PlayerPrefs.GetInt("BottleTop", 1) == 1;
        }
    }

    public static void Vibrate()
    {
        if (!IsVibrationEnabled) return;
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    private void ApplyAudioState()
    {
        AudioListener.volume = isSoundOn ? 1f : 0f;
        AudioListener.pause = !isSoundOn;
    }

    private void ApplyBottleTopToCompletedBottles()
    {
        BottleController[] bottles = FindObjectsByType<BottleController>(FindObjectsSortMode.None);
        foreach (BottleController bottle in bottles)
        {
            if (bottle != null && bottle.stopper != null && bottle.CheckIfItsDone())
            {
                if (isBottleTopOn)
                    bottle.stopper.Animate();
                else
                    bottle.stopper.gameObject.SetActive(false);
            }
        }
    }

    public void OnSoundClick()
    {
        isSoundOn = !isSoundOn;
        PlayerPrefs.SetInt("Sound", isSoundOn ? 1 : 0);
        PlayerPrefs.Save();
        ApplyAudioState();
        UpdateUI();
    }

    public void OnVibrationClick()
    {
        isVibrationOn = !isVibrationOn;
        PlayerPrefs.SetInt("Vibration", isVibrationOn ? 1 : 0);
        PlayerPrefs.Save();
        UpdateUI();

        if (isVibrationOn)
            Vibrate();
    }

    private void WireNotificationPopup()
    {
        if (notificationCanvas == null) return;

        foreach (Button btn in notificationCanvas.GetComponentsInChildren<Button>(true))
        {
            TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null && label.text.IndexOf("Turn On", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (btn.onClick.GetPersistentEventCount() == 0)
                {
                    btn.onClick.RemoveListener(OnEnableNotificationsConfirm);
                    btn.onClick.AddListener(OnEnableNotificationsConfirm);
                }
                break;
            }
        }
    }

    public void OnNotificationClick()
    {
        if (!isNotificationOn)
        {
            CloseAllPopups();
            if (notificationCanvas != null)
            {
                notificationCanvas.SetActive(true);
            }
            else
            {
                OnEnableNotificationsConfirm();
            }
            return;
        }

        isNotificationOn = false;
        PlayerPrefs.SetInt("Notification", 0);
        PlayerPrefs.Save();
        UpdateUI();
    }

    public void OnEnableNotificationsConfirm()
    {
        isNotificationOn = true;
        PlayerPrefs.SetInt("Notification", 1);
        PlayerPrefs.Save();

        if (notificationCanvas != null)
            notificationCanvas.SetActive(false);

        UpdateUI();
    }

    public void OnBottleTopClick()
    {
        isBottleTopOn = !isBottleTopOn;
        PlayerPrefs.SetInt("BottleTop", isBottleTopOn ? 1 : 0);
        PlayerPrefs.Save();

        ApplyBottleTopToCompletedBottles();
        UpdateUI();
    }

    public void OnLanguageClick()
    {
        CloseAllPopups();
        if (languageCanvas == null)
        {
            CreateRuntimeLanguagePopup();
        }
        RefreshLanguageCheckmarks();
        languageCanvas.SetActive(true);
    }

    public void OnAboutClick()
    {
        CloseAllPopups();
        if (aboutCanvas != null)
            aboutCanvas.SetActive(true);
    }

    public void OnFeedbackClick()
    {
        CloseAllPopups();
        if (feedbackCanvas != null)
            feedbackCanvas.SetActive(true);
    }

    public void OnFeedbackSubmit()
    {
        string message = feedbackInputField != null ? feedbackInputField.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("Feedback message is empty.");
            return;
        }

        Debug.Log("Feedback submitted: " + message);

        if (!string.IsNullOrEmpty(supportEmail))
        {
            string subject = Uri.EscapeDataString("Happy Water Sort Feedback");
            string body = Uri.EscapeDataString(message);
            Application.OpenURL($"mailto:{supportEmail}?subject={subject}&body={body}");
        }

        if (feedbackInputField != null)
            feedbackInputField.text = string.Empty;

        if (feedbackCanvas != null)
            feedbackCanvas.SetActive(false);
    }

    public void OnRemoveAdsClick()
    {
        CloseAllPopups();
        if (removeAdCanvas != null)
            removeAdCanvas.SetActive(true);
    }

    #endregion

    #region Runtime Language Popup

    private void CreateRuntimeLanguagePopup()
    {
        GameObject canvasObj = new GameObject("LanguageCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        // Dimmed backdrop
        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdrop.transform.SetParent(canvasObj.transform, false);
        RectTransform bdRt = backdrop.GetComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero;
        bdRt.anchorMax = Vector2.one;
        bdRt.sizeDelta = Vector2.zero;
        backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        backdrop.GetComponent<Button>().onClick.AddListener(() => canvasObj.SetActive(false));

        // Card panel
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.sizeDelta = new Vector2(780f, 920f);
        panel.GetComponent<Image>().color = new Color(0.92f, 0.96f, 1f, 1f);

        // Header bar
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(panel.transform, false);
        RectTransform headerRt = header.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = Vector2.zero;
        headerRt.sizeDelta = new Vector2(0f, 130f);
        header.GetComponent<Image>().color = new Color(0.18f, 0.65f, 0.98f, 1f);

        // Title
        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(header.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = Vector2.zero;
        titleRt.anchorMax = Vector2.one;
        titleRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "Language";
        titleTmp.fontSize = 54;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = Color.white;
        titleTmp.alignment = TextAlignmentOptions.Center;

        // Close button
        GameObject closeObj = new GameObject("CloseBTN", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(header.transform, false);
        RectTransform closeRt = closeObj.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 0.5f);
        closeRt.anchorMax = new Vector2(1f, 0.5f);
        closeRt.pivot = new Vector2(1f, 0.5f);
        closeRt.anchoredPosition = new Vector2(-28f, 0f);
        closeRt.sizeDelta = new Vector2(72f, 72f);
        closeObj.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);
        closeObj.GetComponent<Button>().onClick.AddListener(() => canvasObj.SetActive(false));

        GameObject closeTxt = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
        closeTxt.transform.SetParent(closeObj.transform, false);
        RectTransform closeTxtRt = closeTxt.GetComponent<RectTransform>();
        closeTxtRt.anchorMin = Vector2.zero;
        closeTxtRt.anchorMax = Vector2.one;
        closeTxtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI closeTmp = closeTxt.GetComponent<TextMeshProUGUI>();
        closeTmp.text = "X";
        closeTmp.fontSize = 40;
        closeTmp.fontStyle = FontStyles.Bold;
        closeTmp.color = Color.white;
        closeTmp.alignment = TextAlignmentOptions.Center;

        // Language rows
        languageCheckmarks = new TextMeshProUGUI[SupportedLanguages.Length];
        float startY = -180f;
        float rowHeight = 105f;
        float spacing = 14f;

        for (int i = 0; i < SupportedLanguages.Length; i++)
        {
            string lang = SupportedLanguages[i];
            GameObject row = new GameObject("Lang_" + lang, typeof(RectTransform), typeof(Image), typeof(Button));
            row.transform.SetParent(panel.transform, false);
            RectTransform rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 1f);
            rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, startY - i * (rowHeight + spacing));
            rowRt.sizeDelta = new Vector2(680f, rowHeight);
            row.GetComponent<Image>().color = Color.white;

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(row.transform, false);
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(36f, 0f);
            labelRt.offsetMax = new Vector2(-100f, 0f);
            TextMeshProUGUI labelTmp = labelObj.GetComponent<TextMeshProUGUI>();
            labelTmp.text = lang;
            labelTmp.fontSize = 42;
            labelTmp.color = new Color(0.12f, 0.35f, 0.65f, 1f);
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject checkObj = new GameObject("Check", typeof(RectTransform), typeof(TextMeshProUGUI));
            checkObj.transform.SetParent(row.transform, false);
            RectTransform checkRt = checkObj.GetComponent<RectTransform>();
            checkRt.anchorMin = new Vector2(1f, 0f);
            checkRt.anchorMax = new Vector2(1f, 1f);
            checkRt.pivot = new Vector2(1f, 0.5f);
            checkRt.anchoredPosition = new Vector2(-28f, 0f);
            checkRt.sizeDelta = new Vector2(70f, 0f);
            TextMeshProUGUI checkTmp = checkObj.GetComponent<TextMeshProUGUI>();
            checkTmp.text = "✓";
            checkTmp.fontSize = 44;
            checkTmp.fontStyle = FontStyles.Bold;
            checkTmp.color = new Color(0.12f, 0.65f, 0.35f, 1f);
            checkTmp.alignment = TextAlignmentOptions.Center;

            languageCheckmarks[i] = checkTmp;

            row.GetComponent<Button>().onClick.AddListener(() =>
            {
                PlayerPrefs.SetString("Language", lang);
                PlayerPrefs.Save();
                RefreshLanguageCheckmarks();
                canvasObj.SetActive(false);
            });
        }

        languageCanvas = canvasObj;
    }

    private void RefreshLanguageCheckmarks()
    {
        if (languageCheckmarks == null) return;
        string current = CurrentLanguage;
        for (int i = 0; i < SupportedLanguages.Length && i < languageCheckmarks.Length; i++)
        {
            if (languageCheckmarks[i] != null)
            {
                languageCheckmarks[i].gameObject.SetActive(
                    SupportedLanguages[i].Equals(current, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    #endregion

    #region UI Update

    private void UpdateUI()
    {
        if (soundImg != null)
            soundImg.sprite = isSoundOn ? soundOnSprite : soundOffSprite;
        if (vibrationImg != null)
            vibrationImg.sprite = isVibrationOn ? vibrationOnSprite : vibrationOffSprite;
        if (notificationImg != null)
            notificationImg.sprite = isNotificationOn ? notificationOnSprite : notificationOffSprite;
        if (bottleTopImg != null)
            bottleTopImg.sprite = isBottleTopOn ? bottleTopOnSprite : bottleTopOffSprite;
    }

    #endregion

    #region Close

    public void CloseAllPopups()
    {
        if (notificationCanvas != null) notificationCanvas.SetActive(false);
        if (aboutCanvas != null) aboutCanvas.SetActive(false);
        if (feedbackCanvas != null) feedbackCanvas.SetActive(false);
        if (removeAdCanvas != null) removeAdCanvas.SetActive(false);
        if (languageCanvas != null) languageCanvas.SetActive(false);
    }

    public void ClosePopup(GameObject panel)
    {
        Debug.Log("closeee");
        if (panel != null)
            panel.SetActive(false);
    }

    #endregion
}