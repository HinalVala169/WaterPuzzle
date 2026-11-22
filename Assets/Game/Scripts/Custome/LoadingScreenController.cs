using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Controls the Loading Screen UI, animations, responsive layout, and transition into the Title/Game screen.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class LoadingScreenController : MonoBehaviour
{
    public static LoadingScreenController Instance { get; private set; }

    [Header("Core References")]
    [SerializeField] private RectTransform bottleRect;
    [SerializeField] private RectTransform bottomWave;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TitleScreenController titleScreen;
    [SerializeField] private bool showTitleScreenOnComplete = true;

    [Header("Decorations")]
    [SerializeField] private List<RectTransform> stars = new List<RectTransform>();
    [SerializeField] private List<RectTransform> bubbles = new List<RectTransform>();

    [Header("Timing")]
    [SerializeField] private float loadingDuration = 2.4f;
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float dotInterval = 0.35f;
    [SerializeField] private bool showOnlyOncePerSession = true;

    [Header("Idle Animations")]
    [SerializeField] private float bottleFloatAmplitude = 10f;
    [SerializeField] private float bottleFloatSpeed = 2.0f;
    [SerializeField] private float waveBobAmplitudeY = 10f;
    [SerializeField] private float waveBobAmplitudeX = 14f;
    [SerializeField] private float waveBobSpeed = 1.6f;
    [SerializeField] private float starPulseScale = 0.18f;
    [SerializeField] private float starPulseSpeed = 3.2f;
    [SerializeField] private float bubbleRiseSpeed = 60f;
    [SerializeField] private float bubbleSwayAmplitude = 16f;
    [SerializeField] private float bubbleSwaySpeed = 1.4f;

    private struct CachedRect
    {
        public RectTransform rect;
        public Vector3 scale;
        public Vector2 anchoredPos;
        public Quaternion rotation;
    }

    private static bool hasShownInitialLoading;

    private CanvasGroup canvasGroup;
    private RectTransform bgRect;
    private RectTransform starsGroupRect;
    private readonly List<CachedRect> cachedRects = new List<CachedRect>();
    private readonly List<Vector2> originalBubblePositions = new List<Vector2>();
    private readonly List<Coroutine> activeCoroutines = new List<Coroutine>();

    private bool initialized;
    private bool isTransitioning;
    private bool didPauseGameplay;
    private bool previousGameManagerPause;
    private bool previousGameControllerPause;
    private float bubbleWrapLimitY = 1100f;
    private Vector2 lastLayoutSize = Vector2.zero;
    private Action onCompleteCallback;

    private void Awake()
    {
        Instance = this;
        InitializeReferences();
    }

    private void Start()
    {
        RefreshResponsiveLayoutIfNeeded();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (initialized && gameObject.activeInHierarchy)
            RefreshResponsiveLayoutIfNeeded();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        if (showOnlyOncePerSession && hasShownInitialLoading && onCompleteCallback == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!initialized)
            InitializeReferences();

        isTransitioning = false;
        PauseGameplay(true);
        StopAllLoadingAnimations();
        RestoreCachedTransforms();
        RefreshResponsiveLayoutIfNeeded();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        TrackCoroutine(StartCoroutine(PlayLoadingSequence(loadingDuration)));
    }

    private void OnDisable()
    {
        StopAllLoadingAnimations();
        RestoreCachedTransforms();
        if (didPauseGameplay)
        {
            PauseGameplay(false);
            didPauseGameplay = false;
        }
    }

    /// <summary>
    /// Shows the loading screen for a specified duration, then invokes an optional callback.
    /// </summary>
    public void ShowLoading(float duration = 2.0f, Action onComplete = null)
    {
        loadingDuration = duration;
        onCompleteCallback = onComplete;
        hasShownInitialLoading = false;
        gameObject.SetActive(true);
    }

    private void InitializeReferences()
    {
        if (initialized)
            return;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        bgRect = FindRectByName("Bg");
        starsGroupRect = FindRectByName("StarsGroup");
        if (bottleRect == null)
            bottleRect = FindRectByName("Bottle");
        if (bottomWave == null)
            bottomWave = FindRectByName("Up_Water_Bg");

        if (loadingText == null)
        {
            RectTransform txtRt = FindRectByName("LoadingText");
            if (txtRt != null)
                loadingText = txtRt.GetComponent<TextMeshProUGUI>();
        }

        if (titleScreen == null)
            titleScreen = FindFirstObjectByType<TitleScreenController>(FindObjectsInactive.Include);

        if (stars.Count == 0)
        {
            RectTransform starGroup = FindRectByName("StarsGroup");
            if (starGroup != null)
            {
                for (int i = 0; i < starGroup.childCount; i++)
                {
                    if (starGroup.GetChild(i) is RectTransform rt)
                        stars.Add(rt);
                }
            }
        }

        if (bubbles.Count == 0)
        {
            RectTransform bubbleGroup = FindRectByName("BubblesGroup");
            if (bubbleGroup != null)
            {
                for (int i = 0; i < bubbleGroup.childCount; i++)
                {
                    if (bubbleGroup.GetChild(i) is RectTransform rt)
                        bubbles.Add(rt);
                }
            }
        }

        originalBubblePositions.Clear();
        for (int i = 0; i < bubbles.Count; i++)
            originalBubblePositions.Add(bubbles[i] != null ? bubbles[i].anchoredPosition : Vector2.zero);

        ApplyResponsiveLayout();
        CacheAllRects();
        initialized = true;
    }

    private void RefreshResponsiveLayoutIfNeeded()
    {
        RectTransform canvasRt = GetComponent<RectTransform>();
        if (canvasRt == null) return;

        Vector2 currentSize = canvasRt.rect.size;
        if (currentSize.x <= 0f || currentSize.y <= 0f) return;
        if (Vector2.Distance(currentSize, lastLayoutSize) < 0.5f) return;

        ApplyResponsiveLayout();
        CacheAllRects();
    }

    private void ApplyResponsiveLayout()
    {
        Canvas.ForceUpdateCanvases();
        RectTransform canvasRt = GetComponent<RectTransform>();
        float w = canvasRt != null && canvasRt.rect.width > 0f ? canvasRt.rect.width : 1080f;
        float h = canvasRt != null && canvasRt.rect.height > 0f ? canvasRt.rect.height : 1920f;
        lastLayoutSize = new Vector2(w, h);

        // 1. Background: envelope-fill without aspect distortion (1530 x 2992)
        if (bgRect != null)
        {
            const float bgAspect = 1530f / 2992f;
            float screenAspect = w / h;
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            if (screenAspect > bgAspect)
                bgRect.sizeDelta = new Vector2(w, w / bgAspect);
            else
                bgRect.sizeDelta = new Vector2(h * bgAspect, h);
        }

        // 2. Bottom wave: full width proportionally (1530 x 618) + horizontal sway margin
        float waveHeight = w * (618f / 1530f);
        if (bottomWave != null)
        {
            float extraWidth = waveBobAmplitudeX * 2.5f;
            waveHeight = (w + extraWidth) * (618f / 1530f);
            bottomWave.sizeDelta = new Vector2(extraWidth, waveHeight);
            bottomWave.anchoredPosition = new Vector2(0f, -waveBobAmplitudeY);
        }

        // 3. Scale Bottle, StarsGroup & LoadingText cleanly to fit above the bottom wave on any device
        float safeTop = h * 0.5f - 60f;
        float safeBottom = -h * 0.5f + waveHeight + 40f;
        float usableHeight = Mathf.Max(400f, safeTop - safeBottom);

        float widthScale = Mathf.Min(1f, (w - 80f) / 760f);
        float heightScale = Mathf.Min(1f, usableHeight / 1320f);
        float uiScale = Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.5f, 1.1f);

        float bottleCenterY = safeBottom + usableHeight * 0.58f;
        Vector2 bottlePos = new Vector2(18f * uiScale, bottleCenterY);

        if (bottleRect != null)
        {
            bottleRect.localScale = Vector3.one * uiScale;
            bottleRect.anchoredPosition = bottlePos;
        }

        if (starsGroupRect != null)
        {
            starsGroupRect.localScale = Vector3.one * uiScale;
            starsGroupRect.anchoredPosition = bottlePos;
        }

        if (loadingText != null)
        {
            RectTransform txtRt = loadingText.rectTransform;
            txtRt.localScale = Vector3.one * Mathf.Min(1.05f, uiScale * 1.02f);
            float textY = safeBottom + usableHeight * 0.08f;
            txtRt.anchoredPosition = new Vector2(0f, textY);
        }

        float scaleX = w / 1080f;
        float scaleY = h / 1920f;

        for (int i = 0; i < bubbles.Count; i++)
        {
            if (bubbles[i] != null && i < originalBubblePositions.Count)
            {
                Vector2 p = originalBubblePositions[i];
                bubbles[i].anchoredPosition = new Vector2(p.x * scaleX, p.y * scaleY);
            }
        }

        bubbleWrapLimitY = h * 0.58f;
    }

    private RectTransform FindRectByName(string objectName)
    {
        foreach (RectTransform rt in GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.name == objectName)
                return rt;
        }
        return null;
    }

    private void CacheAllRects()
    {
        cachedRects.Clear();
        CacheRect(bottleRect);
        CacheRect(bottomWave);
        if (loadingText != null) CacheRect(loadingText.rectTransform);
        foreach (RectTransform rt in stars) CacheRect(rt);
        foreach (RectTransform rt in bubbles) CacheRect(rt);
    }

    private void CacheRect(RectTransform rt)
    {
        if (rt == null) return;
        for (int i = 0; i < cachedRects.Count; i++)
        {
            if (cachedRects[i].rect == rt)
                return;
        }

        cachedRects.Add(new CachedRect
        {
            rect = rt,
            scale = rt.localScale,
            anchoredPos = rt.anchoredPosition,
            rotation = rt.localRotation
        });
    }

    private bool TryGetCached(RectTransform rt, out CachedRect cached)
    {
        for (int i = 0; i < cachedRects.Count; i++)
        {
            if (cachedRects[i].rect == rt)
            {
                cached = cachedRects[i];
                return true;
            }
        }
        cached = default;
        return false;
    }

    private void RestoreCachedTransforms()
    {
        for (int i = 0; i < cachedRects.Count; i++)
        {
            CachedRect c = cachedRects[i];
            if (c.rect == null) continue;
            c.rect.localScale = c.scale;
            c.rect.anchoredPosition = c.anchoredPos;
            c.rect.localRotation = c.rotation;
        }
    }

    private IEnumerator PlayLoadingSequence(float duration)
    {
        if (bottleRect != null) bottleRect.localScale = Vector3.zero;
        for (int i = 0; i < stars.Count; i++)
        {
            if (stars[i] != null) stars[i].localScale = Vector3.zero;
        }

        // Slide bottom wave up
        if (bottomWave != null && TryGetCached(bottomWave, out CachedRect waveCached))
        {
            Vector2 startPos = waveCached.anchoredPos + new Vector2(0f, -260f);
            bottomWave.anchoredPosition = startPos;
            TrackCoroutine(StartCoroutine(MoveAnchored(bottomWave, startPos, waveCached.anchoredPos, 0.45f)));
        }

        // Pop bottle illustration
        if (bottleRect != null)
            TrackCoroutine(StartCoroutine(PopScale(bottleRect, 0.45f, 1.08f)));

        yield return new WaitForSecondsRealtime(0.2f);

        // Pop stars
        for (int i = 0; i < stars.Count; i++)
        {
            if (stars[i] != null)
                TrackCoroutine(StartCoroutine(PopScale(stars[i], 0.3f, 1.18f)));
            yield return new WaitForSecondsRealtime(0.06f);
        }

        // Start idle loops
        if (bottleRect != null)
            TrackCoroutine(StartCoroutine(IdleFloatLoop(bottleRect, bottleFloatAmplitude, bottleFloatSpeed, 0f)));
        if (bottomWave != null)
            TrackCoroutine(StartCoroutine(IdleWaveLoop()));

        for (int i = 0; i < stars.Count; i++)
        {
            if (stars[i] != null)
                TrackCoroutine(StartCoroutine(IdleStarLoop(stars[i], i * 1.1f)));
        }

        for (int i = 0; i < bubbles.Count; i++)
        {
            if (bubbles[i] != null)
                TrackCoroutine(StartCoroutine(IdleBubbleLoop(bubbles[i], i)));
        }

        if (loadingText != null)
            TrackCoroutine(StartCoroutine(AnimateLoadingDots()));

        float remaining = Mathf.Max(0.4f, duration - 0.38f);
        yield return new WaitForSecondsRealtime(remaining);

        // Fade out cleanly and reveal Title Screen so its intro animation plays on cue
        isTransitioning = true;
        hasShownInitialLoading = true;

        if (showTitleScreenOnComplete && titleScreen != null)
            titleScreen.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            float ease = t * t * (3f - 2f * t);
            if (canvasGroup != null)
                canvasGroup.alpha = 1f - ease;
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        Action cb = onCompleteCallback;
        onCompleteCallback = null;
        gameObject.SetActive(false);
        cb?.Invoke();
    }

    private IEnumerator AnimateLoadingDots()
    {
        int dotCount = 1;
        while (!isTransitioning && loadingText != null)
        {
            loadingText.text = "Loading" + new string('.', dotCount);
            dotCount = (dotCount % 3) + 1;
            yield return new WaitForSecondsRealtime(dotInterval);
        }
    }

    private IEnumerator IdleWaveLoop()
    {
        if (!TryGetCached(bottomWave, out CachedRect c))
            yield break;

        float elapsed = 0f;
        while (!isTransitioning)
        {
            elapsed += Time.unscaledDeltaTime;
            float offsetY = Mathf.Sin(elapsed * waveBobSpeed) * waveBobAmplitudeY;
            float offsetX = Mathf.Cos(elapsed * waveBobSpeed * 0.7f) * waveBobAmplitudeX;
            bottomWave.anchoredPosition = c.anchoredPos + new Vector2(offsetX, offsetY);
            yield return null;
        }
    }

    private IEnumerator IdleFloatLoop(RectTransform rt, float amplitude, float speed, float phaseOffset)
    {
        if (!TryGetCached(rt, out CachedRect c))
            yield break;

        float elapsed = 0f;
        while (!isTransitioning)
        {
            elapsed += Time.unscaledDeltaTime;
            float offsetY = Mathf.Sin(elapsed * speed + phaseOffset) * amplitude;
            rt.anchoredPosition = c.anchoredPos + new Vector2(0f, offsetY);
            yield return null;
        }
    }

    private IEnumerator IdleStarLoop(RectTransform rt, float phaseOffset)
    {
        if (!TryGetCached(rt, out CachedRect c))
            yield break;

        float elapsed = 0f;
        while (!isTransitioning)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = 1f + Mathf.Sin(elapsed * starPulseSpeed + phaseOffset) * starPulseScale;
            float rotZ = Mathf.Sin(elapsed * 1.5f + phaseOffset) * 8f;
            rt.localScale = c.scale * pulse;
            rt.localRotation = c.rotation * Quaternion.Euler(0f, 0f, rotZ);
            yield return null;
        }
    }

    private IEnumerator IdleBubbleLoop(RectTransform rt, int index)
    {
        if (!TryGetCached(rt, out CachedRect c))
            yield break;

        float speedMultiplier = 0.75f + (index % 4) * 0.2f;
        float swayPhase = index * 1.1f;
        float currentY = c.anchoredPos.y;
        float elapsed = 0f;

        while (!isTransitioning)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            currentY += bubbleRiseSpeed * speedMultiplier * dt;

            if (currentY > bubbleWrapLimitY)
                currentY = -bubbleWrapLimitY;

            float offsetX = Mathf.Sin(elapsed * bubbleSwaySpeed + swayPhase) * bubbleSwayAmplitude;
            rt.anchoredPosition = new Vector2(c.anchoredPos.x + offsetX, currentY);
            yield return null;
        }
    }

    private IEnumerator PopScale(RectTransform target, float duration, float overshoot)
    {
        if (target == null || !TryGetCached(target, out CachedRect c))
            yield break;

        float elapsed = 0f;
        Vector3 endScale = c.scale;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float c1 = (overshoot - 1f) * 2.5f;
            float c3 = c1 + 1f;
            float p = t - 1f;
            float s = 1f + c3 * p * p * p + c1 * p * p;
            target.localScale = endScale * s;
            yield return null;
        }
        target.localScale = endScale;
    }

    private IEnumerator MoveAnchored(RectTransform target, Vector2 from, Vector2 to, float duration)
    {
        if (target == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            target.anchoredPosition = Vector2.LerpUnclamped(from, to, ease);
            yield return null;
        }
        target.anchoredPosition = to;
    }

    private void PauseGameplay(bool pause)
    {
        if (pause)
        {
            didPauseGameplay = true;
            if (GameManager.Instance != null)
            {
                previousGameManagerPause = GameManager.Instance.gamePause;
                GameManager.Instance.gamePause = true;
            }
            if (GameController.Instance != null)
            {
                previousGameControllerPause = GameController.Instance.shouldGamePause;
                GameController.Instance.shouldGamePause = true;
            }
        }
        else
        {
            // Keep gameplay paused if TitleScreenController is currently active underneath
            TitleScreenController titleScreen = FindFirstObjectByType<TitleScreenController>(FindObjectsInactive.Exclude);
            if (titleScreen != null && titleScreen.gameObject.activeInHierarchy)
                return;

            if (GameManager.Instance != null)
                GameManager.Instance.gamePause = previousGameManagerPause;
            if (GameController.Instance != null)
                GameController.Instance.shouldGamePause = previousGameControllerPause;
        }
    }

    private void TrackCoroutine(Coroutine routine)
    {
        if (routine != null)
            activeCoroutines.Add(routine);
    }

    private void StopAllLoadingAnimations()
    {
        for (int i = 0; i < activeCoroutines.Count; i++)
        {
            if (activeCoroutines[i] != null)
                StopCoroutine(activeCoroutines[i]);
        }
        activeCoroutines.Clear();
    }
}
