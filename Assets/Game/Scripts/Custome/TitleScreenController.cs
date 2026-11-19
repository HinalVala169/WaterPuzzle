using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Main/Title Screen UI, intro & idle animations, and transition into gameplay.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TitleScreenController : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private RectTransform titleGroup;
    [SerializeField] private RectTransform titleCloud;
    [SerializeField] private RectTransform waterLeft;
    [SerializeField] private RectTransform waterRight;
    [SerializeField] private RectTransform bottomWave;
    [SerializeField] private Button startButton;

    [Header("Title Letter Groups")]
    [SerializeField] private List<RectTransform> happyLetters = new List<RectTransform>();
    [SerializeField] private List<RectTransform> waterLetters = new List<RectTransform>();
    [SerializeField] private List<RectTransform> sortLetters = new List<RectTransform>();

    [Header("Bubbles")]
    [SerializeField] private List<RectTransform> bubbles = new List<RectTransform>();

    [Header("Intro Timing")]
    [SerializeField] private float waveRiseDuration = 0.5f;
    [SerializeField] private float cloudPopDuration = 0.4f;
    [SerializeField] private float splashPopDuration = 0.35f;
    [SerializeField] private float letterPopDuration = 0.28f;
    [SerializeField] private float letterStagger = 0.035f;
    [SerializeField] private float buttonPopDuration = 0.35f;

    [Header("Idle Loops")]
    [SerializeField] private float waveBobAmplitudeY = 10f;
    [SerializeField] private float waveBobAmplitudeX = 14f;
    [SerializeField] private float waveBobSpeed = 1.6f;
    [SerializeField] private float cloudPulseScale = 0.025f;
    [SerializeField] private float cloudPulseSpeed = 1.8f;
    [SerializeField] private float letterFloatAmplitude = 6f;
    [SerializeField] private float letterFloatSpeed = 2.4f;
    [SerializeField] private float buttonPulseScale = 0.045f;
    [SerializeField] private float buttonPulseSpeed = 2.8f;

    [Header("Bubble Drift")]
    [SerializeField] private float bubbleRiseSpeed = 65f;
    [SerializeField] private float bubbleSwayAmplitude = 18f;
    [SerializeField] private float bubbleSwaySpeed = 1.4f;
    [SerializeField] private float bubbleWrapLimitY = 1100f;

    [Header("Transition")]
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private bool showOnlyOncePerSession = true;

    private static bool hasStartedSession;

    private struct CachedRect
    {
        public RectTransform rect;
        public Vector3 scale;
        public Vector2 anchoredPos;
        public Quaternion rotation;
    }

    private CanvasGroup canvasGroup;
    private RectTransform bgRect;
    private RectTransform startButtonRect;
    private readonly List<CachedRect> cachedRects = new List<CachedRect>();
    private readonly List<Coroutine> activeCoroutines = new List<Coroutine>();
    private readonly List<GameObject> hiddenGameplayButtons = new List<GameObject>();

    private bool initialized;
    private bool isTransitioning;
    private bool didPauseGameplay;
    private bool previousGameManagerPause;
    private bool previousGameControllerPause;

    private void Awake()
    {
        InitializeReferences();
    }

    private void OnEnable()
    {
        if (showOnlyOncePerSession && hasStartedSession)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!initialized)
            InitializeReferences();

        isTransitioning = false;
        PauseGameplay(true);
        HideOtherGameplayButtons();
        StopAllTitleAnimations();
        RestoreCachedTransforms();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        TrackCoroutine(StartCoroutine(PlayIntroSequence()));
    }

    private void OnDisable()
    {
        StopAllTitleAnimations();
        RestoreCachedTransforms();
        RestoreOtherGameplayButtons();
        if (didPauseGameplay)
        {
            PauseGameplay(false);
            didPauseGameplay = false;
        }
    }

    /// <summary>
    /// Re-opens the Title Screen and replays its intro animation.
    /// </summary>
    public void ShowTitleScreen()
    {
        hasStartedSession = false;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Triggered by the Start button to smoothly transition into gameplay.
    /// </summary>
    public void OnStartClicked()
    {
        if (isTransitioning)
            return;

        hasStartedSession = true;
        isTransitioning = true;
        if (canvasGroup != null)
            canvasGroup.interactable = false;

        TrackCoroutine(StartCoroutine(PlayStartTransition()));
    }

    private void InitializeReferences()
    {
        if (initialized)
            return;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (titleGroup == null)
            titleGroup = FindRectByName("TitleGroup");
        if (titleCloud == null)
            titleCloud = FindRectByName("Title_Bg");
        if (waterLeft == null)
            waterLeft = FindRectByName("Water_L");
        if (waterRight == null)
            waterRight = FindRectByName("Water_R");
        if (bottomWave == null)
            bottomWave = FindRectByName("Up_Water_Bg");
        bgRect = FindRectByName("Bg");

        if (startButton == null)
        {
            RectTransform btnRt = FindRectByName("StartButton");
            if (btnRt != null)
                startButton = btnRt.GetComponent<Button>();
        }

        if (startButton != null)
        {
            startButtonRect = startButton.GetComponent<RectTransform>();
            startButton.onClick.RemoveListener(OnStartClicked);
            startButton.onClick.AddListener(OnStartClicked);
        }

        PopulateLetterList(happyLetters, "HappyGroup", new[] { "H", "A", "P1", "P2", "Y" });
        PopulateLetterList(waterLetters, "WaterGroup", new[] { "W", "A", "T", "E", "R" });
        PopulateLetterList(sortLetters, "SortGroup", new[] { "S", "O", "R", "T" });

        if (bubbles.Count == 0)
        {
            RectTransform bubbleContainer = FindRectByName("BubblesGroup");
            if (bubbleContainer != null)
            {
                for (int i = 0; i < bubbleContainer.childCount; i++)
                {
                    if (bubbleContainer.GetChild(i) is RectTransform childRt)
                        bubbles.Add(childRt);
                }
            }
        }

        ApplyResponsiveLayout();
        CacheAllRects();
        initialized = true;
    }

    private void ApplyResponsiveLayout()
    {
        RectTransform canvasRt = GetComponent<RectTransform>();
        float w = canvasRt != null && canvasRt.rect.width > 0f ? canvasRt.rect.width : 1080f;
        float h = canvasRt != null && canvasRt.rect.height > 0f ? canvasRt.rect.height : 1920f;

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

        // 2. Bottom wave: always span full width proportionally (1530 x 628) + extra margin for horizontal sway
        if (bottomWave != null)
        {
            float extraWidth = waveBobAmplitudeX * 2.5f;
            float waveHeight = (w + extraWidth) * (628f / 1530f);
            bottomWave.sizeDelta = new Vector2(extraWidth, waveHeight);
            bottomWave.anchoredPosition = new Vector2(0f, -waveBobAmplitudeY);
        }

        // 3. Scale TitleGroup & StartButton to fit any phone/tablet aspect ratio
        float widthScale = Mathf.Min(1f, (w - 60f) / 940f);
        float heightScale = Mathf.Min(1f, h / 1750f);
        float uiScale = Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.55f, 1.15f);

        if (titleGroup != null)
        {
            titleGroup.localScale = Vector3.one * uiScale;
            titleGroup.anchoredPosition = new Vector2(0f, h * 0.12f);
        }

        if (startButtonRect != null)
        {
            startButtonRect.localScale = Vector3.one * Mathf.Min(1.1f, uiScale * 1.02f);
            startButtonRect.anchoredPosition = new Vector2(0f, -h * 0.215f);
        }

        // 4. Scale bubble positions proportionally to screen size
        float scaleX = w / 1080f;
        float scaleY = h / 1920f;
        for (int i = 0; i < bubbles.Count; i++)
        {
            if (bubbles[i] != null)
            {
                Vector2 p = bubbles[i].anchoredPosition;
                bubbles[i].anchoredPosition = new Vector2(p.x * scaleX, p.y * scaleY);
            }
        }
        bubbleWrapLimitY = h * 0.58f;
    }

    private void PopulateLetterList(List<RectTransform> targetList, string groupName, string[] letterNames)
    {
        if (targetList.Count > 0)
            return;

        RectTransform group = FindRectByName(groupName);
        if (group == null)
            return;

        foreach (string letterName in letterNames)
        {
            Transform child = group.Find(letterName);
            if (child is RectTransform rt)
                targetList.Add(rt);
        }
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
        CacheRect(titleGroup);
        CacheRect(titleCloud);
        CacheRect(waterLeft);
        CacheRect(waterRight);
        CacheRect(bottomWave);
        CacheRect(startButtonRect);

        foreach (RectTransform rt in happyLetters) CacheRect(rt);
        foreach (RectTransform rt in waterLetters) CacheRect(rt);
        foreach (RectTransform rt in sortLetters) CacheRect(rt);
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

    private IEnumerator PlayIntroSequence()
    {
        // Hide animated elements before intro begins
        SetScaleZero(titleCloud);
        SetScaleZero(waterLeft);
        SetScaleZero(waterRight);
        SetScaleZero(startButtonRect);
        SetListScaleZero(happyLetters);
        SetListScaleZero(waterLetters);
        SetListScaleZero(sortLetters);
        SetListScaleZero(bubbles);

        // 1. Slide bottom water wave up
        if (bottomWave != null && TryGetCached(bottomWave, out CachedRect waveCached))
        {
            Vector2 startPos = waveCached.anchoredPos + new Vector2(0f, -260f);
            bottomWave.anchoredPosition = startPos;
            TrackCoroutine(StartCoroutine(MoveAnchored(bottomWave, startPos, waveCached.anchoredPos, waveRiseDuration)));
        }

        yield return new WaitForSecondsRealtime(0.08f);

        // 2. Pop title cloud
        if (titleCloud != null)
            TrackCoroutine(StartCoroutine(PopScale(titleCloud, cloudPopDuration, 1.08f)));

        yield return new WaitForSecondsRealtime(0.14f);

        // 3. Pop water droplets
        if (waterLeft != null)
            TrackCoroutine(StartCoroutine(PopScale(waterLeft, splashPopDuration, 1.15f)));
        if (waterRight != null)
            TrackCoroutine(StartCoroutine(PopScale(waterRight, splashPopDuration, 1.15f)));

        // 4. Staggered letter pop-ins: Happy -> WATER -> Sort
        yield return StartCoroutine(PopLetterGroup(happyLetters));
        yield return StartCoroutine(PopLetterGroup(waterLetters));
        yield return StartCoroutine(PopLetterGroup(sortLetters));

        // 5. Pop bubbles
        for (int i = 0; i < bubbles.Count; i++)
        {
            if (bubbles[i] != null)
                TrackCoroutine(StartCoroutine(PopScale(bubbles[i], 0.25f, 1.15f)));
            yield return new WaitForSecondsRealtime(0.025f);
        }

        // 6. Pop Start button
        if (startButtonRect != null)
        {
            yield return StartCoroutine(PopScale(startButtonRect, buttonPopDuration, 1.12f));
        }

        // 7. Start continuous idle animations
        StartIdleLoops();
    }

    private IEnumerator PopLetterGroup(List<RectTransform> letters)
    {
        for (int i = 0; i < letters.Count; i++)
        {
            if (letters[i] != null)
                TrackCoroutine(StartCoroutine(PopScale(letters[i], letterPopDuration, 1.18f)));
            yield return new WaitForSecondsRealtime(letterStagger);
        }
    }

    private void StartIdleLoops()
    {
        if (bottomWave != null)
            TrackCoroutine(StartCoroutine(IdleWaveLoop()));
        if (titleCloud != null)
            TrackCoroutine(StartCoroutine(IdlePulseLoop(titleCloud, cloudPulseScale, cloudPulseSpeed, 0f)));
        if (waterLeft != null)
            TrackCoroutine(StartCoroutine(IdleFloatLoop(waterLeft, 7f, 2.0f, 0f)));
        if (waterRight != null)
            TrackCoroutine(StartCoroutine(IdleFloatLoop(waterRight, 7f, 2.0f, Mathf.PI)));

        float phase = 0f;
        phase = StartLetterFloatList(happyLetters, phase);
        phase = StartLetterFloatList(waterLetters, phase);
        StartLetterFloatList(sortLetters, phase);

        for (int i = 0; i < bubbles.Count; i++)
        {
            if (bubbles[i] != null)
                TrackCoroutine(StartCoroutine(IdleBubbleLoop(bubbles[i], i)));
        }

        if (startButtonRect != null)
            TrackCoroutine(StartCoroutine(IdlePulseLoop(startButtonRect, buttonPulseScale, buttonPulseSpeed, 0.5f)));
    }

    private float StartLetterFloatList(List<RectTransform> letters, float startPhase)
    {
        float phase = startPhase;
        for (int i = 0; i < letters.Count; i++)
        {
            if (letters[i] != null)
                TrackCoroutine(StartCoroutine(IdleFloatLoop(letters[i], letterFloatAmplitude, letterFloatSpeed, phase)));
            phase += 0.45f;
        }
        return phase;
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

    private IEnumerator IdlePulseLoop(RectTransform rt, float scaleAmount, float speed, float phaseOffset)
    {
        if (!TryGetCached(rt, out CachedRect c))
            yield break;

        float elapsed = 0f;
        while (!isTransitioning)
        {
            elapsed += Time.unscaledDeltaTime;
            float factor = 1f + Mathf.Sin(elapsed * speed + phaseOffset) * scaleAmount;
            rt.localScale = c.scale * factor;
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

    private IEnumerator PlayStartTransition()
    {
        // Quick button press feedback
        if (startButtonRect != null && TryGetCached(startButtonRect, out CachedRect btnCached))
        {
            yield return StartCoroutine(ScaleTo(startButtonRect, btnCached.scale * 0.9f, 0.07f));
            yield return StartCoroutine(ScaleTo(startButtonRect, btnCached.scale * 1.08f, 0.08f));
        }

        // Fade out the entire Title Screen
        float elapsed = 0f;
        Vector3 startTitleScale = titleGroup != null ? titleGroup.localScale : Vector3.one;
        Vector3 endTitleScale = startTitleScale * 1.06f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            float ease = t * t * (3f - 2f * t);

            if (canvasGroup != null)
                canvasGroup.alpha = 1f - ease;
            if (titleGroup != null)
                titleGroup.localScale = Vector3.Lerp(startTitleScale, endTitleScale, ease);

            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
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
            float s = EaseOutBack(t, overshoot);
            target.localScale = endScale * s;
            yield return null;
        }

        target.localScale = endScale;
    }

    private IEnumerator ScaleTo(RectTransform target, Vector3 endScale, float duration)
    {
        if (target == null)
            yield break;

        Vector3 startScale = target.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            target.localScale = Vector3.Lerp(startScale, endScale, t);
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

    private static float EaseOutBack(float t, float overshoot)
    {
        float c1 = (overshoot - 1f) * 2.5f;
        float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    private static void SetScaleZero(RectTransform rt)
    {
        if (rt != null)
            rt.localScale = Vector3.zero;
    }

    private static void SetListScaleZero(List<RectTransform> list)
    {
        for (int i = 0; i < list.Count; i++)
            SetScaleZero(list[i]);
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
            if (GameManager.Instance != null)
                GameManager.Instance.gamePause = previousGameManagerPause;
            if (GameController.Instance != null)
                GameController.Instance.shouldGamePause = previousGameControllerPause;
        }
    }

    private void HideOtherGameplayButtons()
    {
        hiddenGameplayButtons.Clear();
        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Button btn in allButtons)
        {
            if (btn == null || btn.transform.IsChildOf(transform))
                continue;

            hiddenGameplayButtons.Add(btn.gameObject);
            btn.gameObject.SetActive(false);
        }
    }

    private void RestoreOtherGameplayButtons()
    {
        foreach (GameObject btnObj in hiddenGameplayButtons)
        {
            if (btnObj != null)
                btnObj.SetActive(true);
        }
        hiddenGameplayButtons.Clear();
    }

    private void TrackCoroutine(Coroutine routine)
    {
        if (routine != null)
            activeCoroutines.Add(routine);
    }

    private void StopAllTitleAnimations()
    {
        for (int i = 0; i < activeCoroutines.Count; i++)
        {
            if (activeCoroutines[i] != null)
                StopCoroutine(activeCoroutines[i]);
        }
        activeCoroutines.Clear();
    }
}
