using System.Collections;
using TMPro;
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

    [Header("Lock / Unlock UI (Optional)")]
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private GameObject coinBadge;
    [SerializeField] private TextMeshProUGUI coinCostText;
    [SerializeField] private GameObject levelBadge;
    [SerializeField] private TextMeshProUGUI levelReqText;
    [SerializeField] private GameObject checkmarkBadge;

    [SerializeField] private int index;
    [SerializeField] private ThemeCategory category;

    private Button button;
    private Coroutine feedbackRoutine;
    private Vector3 originalScale = Vector3.one;
    private Vector2 originalAnchoredPos;
    private bool cachedDefaults;
    private static Sprite cachedRoundedOverlaySprite;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClick);

        CacheDefaults();
    }

    private void CacheDefaults()
    {
        if (cachedDefaults) return;
        cachedDefaults = true;
        originalScale = transform.localScale;
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
            originalAnchoredPos = rt.anchoredPosition;
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

        if (checkmarkBadge != null)
            checkmarkBadge.SetActive(isSelected);
    }

    public void SetLockState(
        bool isUnlocked,
        bool isLevelLocked,
        int costOrLevel,
        Sprite lockSprite = null,
        Sprite coinPillSprite = null,
        Sprite coinIconSprite = null,
        TMP_FontAsset font = null)
    {
        if (!isUnlocked)
            EnsureRuntimeLockUI(lockSprite, coinPillSprite, coinIconSprite, font);

        if (lockOverlay != null)
            lockOverlay.SetActive(!isUnlocked);

        if (coinBadge != null)
            coinBadge.SetActive(!isUnlocked && !isLevelLocked);

        if (!isUnlocked && !isLevelLocked && coinCostText != null)
            coinCostText.text = costOrLevel.ToString();

        if (levelBadge != null)
            levelBadge.SetActive(!isUnlocked && isLevelLocked);

        if (!isUnlocked && isLevelLocked && levelReqText != null)
            levelReqText.text = "Unlock at level\n" + costOrLevel;

        if (!isUnlocked && checkmarkBadge != null)
            checkmarkBadge.SetActive(false);
    }

    private void EnsureRuntimeLockUI(Sprite lockSprite, Sprite coinPillSprite, Sprite coinIconSprite, TMP_FontAsset font)
    {
        if (lockOverlay != null) return;

        // Dark translucent rounded overlay strictly inside / behind the card frame
        GameObject overlayObj = new GameObject("RuntimeLockOverlay", typeof(RectTransform), typeof(Image));
        overlayObj.transform.SetParent(transform, false);
        if (frame != null && frame.transform != transform && frame.transform.parent == transform)
        {
            overlayObj.transform.SetAsLastSibling();
            frame.transform.SetAsLastSibling();
        }
        RectTransform overlayRt = overlayObj.GetComponent<RectTransform>();
        if (frame != null && frame.transform != transform && frame.transform.parent == transform)
        {
            RectTransform frameRt = frame.rectTransform;
            overlayRt.anchorMin = frameRt.anchorMin;
            overlayRt.anchorMax = frameRt.anchorMax;
            overlayRt.pivot = frameRt.pivot;
            overlayRt.anchoredPosition = frameRt.anchoredPosition;
            overlayRt.sizeDelta = frameRt.sizeDelta;
            overlayRt.offsetMin = frameRt.offsetMin + new Vector2(6f, 6f);
            overlayRt.offsetMax = frameRt.offsetMax - new Vector2(6f, 6f);
        }
        else
        {
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = new Vector2(14f, 14f);
            overlayRt.offsetMax = new Vector2(-14f, -14f);
        }
        Image overlayImg = overlayObj.GetComponent<Image>();
        overlayImg.sprite = GetRoundedOverlaySprite();
        overlayImg.type = Image.Type.Sliced;
        overlayImg.color = new Color(0.03f, 0.05f, 0.16f, 0.62f);
        overlayImg.raycastTarget = false;
        lockOverlay = overlayObj;

        // Centered purple padlock icon (falls back to text if sprite not found)
        if (lockSprite != null)
        {
            GameObject lockIconObj = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
            lockIconObj.transform.SetParent(overlayObj.transform, false);
            RectTransform lockIconRt = lockIconObj.GetComponent<RectTransform>();
            lockIconRt.anchorMin = new Vector2(0.24f, 0.36f);
            lockIconRt.anchorMax = new Vector2(0.76f, 0.74f);
            lockIconRt.offsetMin = Vector2.zero;
            lockIconRt.offsetMax = Vector2.zero;
            Image lockImg = lockIconObj.GetComponent<Image>();
            lockImg.sprite = lockSprite;
            lockImg.preserveAspect = true;
            lockImg.raycastTarget = false;
        }
        else
        {
            GameObject lockIconObj = new GameObject("LockLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            lockIconObj.transform.SetParent(overlayObj.transform, false);
            RectTransform lockIconRt = lockIconObj.GetComponent<RectTransform>();
            lockIconRt.anchorMin = new Vector2(0.1f, 0.42f);
            lockIconRt.anchorMax = new Vector2(0.9f, 0.72f);
            lockIconRt.offsetMin = Vector2.zero;
            lockIconRt.offsetMax = Vector2.zero;
            TextMeshProUGUI lockTmp = lockIconObj.GetComponent<TextMeshProUGUI>();
            lockTmp.text = "LOCKED";
            if (font != null) lockTmp.font = font;
            lockTmp.fontSize = 24;
            lockTmp.fontStyle = FontStyles.Bold;
            lockTmp.color = new Color(0.86f, 0.90f, 1f, 0.95f);
            lockTmp.alignment = TextAlignmentOptions.Center;
            lockTmp.raycastTarget = false;
        }

        // Coin price pill at bottom
        if (coinBadge == null)
        {
            GameObject coinObj = new GameObject("RuntimeCoinBadge", typeof(RectTransform), typeof(Image));
            coinObj.transform.SetParent(overlayObj.transform, false);
            RectTransform coinRt = coinObj.GetComponent<RectTransform>();
            coinRt.anchorMin = new Vector2(0.16f, 0.09f);
            coinRt.anchorMax = new Vector2(0.84f, 0.28f);
            coinRt.offsetMin = Vector2.zero;
            coinRt.offsetMax = Vector2.zero;
            Image pillImg = coinObj.GetComponent<Image>();
            if (coinPillSprite != null)
            {
                pillImg.sprite = coinPillSprite;
                pillImg.color = Color.white;
                pillImg.type = Image.Type.Sliced;
            }
            else
            {
                pillImg.color = new Color(0.18f, 0.56f, 0.98f, 0.95f);
            }
            pillImg.raycastTarget = false;
            coinBadge = coinObj;

            if (coinIconSprite != null)
            {
                GameObject iconObj = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(coinObj.transform, false);
                RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.04f, 0.08f);
                iconRt.anchorMax = new Vector2(0.35f, 0.92f);
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;
                Image cImg = iconObj.GetComponent<Image>();
                cImg.sprite = coinIconSprite;
                cImg.preserveAspect = true;
                cImg.raycastTarget = false;
            }

            GameObject txtObj = new GameObject("CostText", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(coinObj.transform, false);
            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = coinIconSprite != null ? new Vector2(0.34f, 0.04f) : Vector2.zero;
            txtRt.anchorMax = new Vector2(0.94f, 0.96f);
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            coinCostText = txtObj.GetComponent<TextMeshProUGUI>();
            if (font != null) coinCostText.font = font;
            coinCostText.enableAutoSizing = true;
            coinCostText.fontSizeMin = 20;
            coinCostText.fontSizeMax = 44;
            coinCostText.fontStyle = FontStyles.Bold;
            coinCostText.color = Color.white;
            coinCostText.alignment = TextAlignmentOptions.Center;
            coinCostText.raycastTarget = false;
        }

        // Level requirement text at bottom
        if (levelBadge == null)
        {
            GameObject lvlObj = new GameObject("RuntimeLevelBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            lvlObj.transform.SetParent(overlayObj.transform, false);
            RectTransform lvlRt = lvlObj.GetComponent<RectTransform>();
            lvlRt.anchorMin = new Vector2(0.06f, 0.06f);
            lvlRt.anchorMax = new Vector2(0.94f, 0.34f);
            lvlRt.offsetMin = Vector2.zero;
            lvlRt.offsetMax = Vector2.zero;
            levelReqText = lvlObj.GetComponent<TextMeshProUGUI>();
            if (font != null) levelReqText.font = font;
            levelReqText.enableAutoSizing = true;
            levelReqText.fontSizeMin = 16;
            levelReqText.fontSizeMax = 32;
            levelReqText.lineSpacing = -8f;
            levelReqText.fontStyle = FontStyles.Bold;
            levelReqText.color = Color.white;
            levelReqText.alignment = TextAlignmentOptions.Center;
            levelReqText.raycastTarget = false;
            levelBadge = lvlObj;
        }
    }

    private static Sprite GetRoundedOverlaySprite()
    {
        if (cachedRoundedOverlaySprite != null)
            return cachedRoundedOverlaySprite;

        const int size = 64;
        const int radius = 26;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cx = x < radius ? radius - x : (x >= size - radius ? x - (size - radius - 1) : 0);
                int cy = y < radius ? radius - y : (y >= size - radius ? y - (size - radius - 1) : 0);
                float dist = Mathf.Sqrt(cx * cx + cy * cy);
                float alpha = dist <= radius ? Mathf.Clamp01((radius - dist) + 0.5f) : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        cachedRoundedOverlaySprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius)
        );
        return cachedRoundedOverlaySprite;
    }

    public void PlayShakeAnimation()
    {
        CacheDefaults();
        if (!gameObject.activeInHierarchy) return;

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(ShakeRoutine());
    }

    public void PlayUnlockPopAnimation()
    {
        CacheDefaults();
        if (!gameObject.activeInHierarchy) return;

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(UnlockPopRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) yield break;

        float duration = 0.28f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float offset = Mathf.Sin(t * Mathf.PI * 5f) * 10f * (1f - t);
            rt.anchoredPosition = originalAnchoredPos + new Vector2(offset, 0f);
            yield return null;
        }

        rt.anchoredPosition = originalAnchoredPos;
        feedbackRoutine = null;
    }

    private IEnumerator UnlockPopRoutine()
    {
        float duration = 0.28f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.16f;
            transform.localScale = originalScale * s;
            yield return null;
        }

        transform.localScale = originalScale;
        feedbackRoutine = null;
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
