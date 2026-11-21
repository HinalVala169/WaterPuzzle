using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WinScreenAnimation : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float cardPopDuration = 0.35f;
    [SerializeField] private float titlePopDuration = 0.32f;
    [SerializeField] private float cupPopDuration = 0.42f;
    [SerializeField] private float burstPopDuration = 0.32f;
    [SerializeField] private float burstStagger = 0.03f;
    [SerializeField] private float movesCountDuration = 0.4f;

    [Header("Idle Loops")]
    [SerializeField] private float cupFloatAmplitude = 10f;
    [SerializeField] private float cupFloatSpeed = 2.4f;
    [SerializeField] private float cupWiggleAngle = 3.5f;
    [SerializeField] private float buttonPulseScale = 0.045f;
    [SerializeField] private float buttonPulseSpeed = 3f;

    private struct CachedTransform
    {
        public RectTransform rect;
        public Vector3 scale;
        public Vector2 anchoredPos;
        public Quaternion rotation;
    }

    private CanvasGroup canvasGroup;
    private RectTransform rootRect;
    private RectTransform titleGroup;
    private RectTransform titleText;
    private readonly List<RectTransform> headerStars = new List<RectTransform>();
    private RectTransform cupRect;
    private readonly List<RectTransform> burstStars = new List<RectTransform>();
    private readonly List<RectTransform> burstSplashes = new List<RectTransform>();
    private RectTransform movesGroup;
    private TextMeshProUGUI movesNumberText;
    private RectTransform nextButtonRect;

    private readonly List<CachedTransform> cachedTransforms = new List<CachedTransform>();
    private readonly List<Coroutine> activeCoroutines = new List<Coroutine>();
    private readonly List<GameObject> disabledGameplayButtons = new List<GameObject>();
    private bool previousPauseState;
    private bool initialized;

    private void Awake()
    {
        InitializeReferences();
    }

    private void OnEnable()
    {
        if (!initialized)
            InitializeReferences();

        if (GameController.Instance != null)
        {
            previousPauseState = GameController.Instance.shouldGamePause;
            GameController.Instance.shouldGamePause = true;
        }

        DisableOtherGameplayButtons();
        StopRunningAnimations();
        RestoreOriginalTransforms();
        StartCoroutineAndTrack(PlayWinSequence());
    }

    private void OnDisable()
    {
        StopRunningAnimations();
        RestoreOriginalTransforms();
        RestoreOtherGameplayButtons();

        if (GameController.Instance != null)
            GameController.Instance.shouldGamePause = previousPauseState;
    }

    private void DisableOtherGameplayButtons()
    {
        disabledGameplayButtons.Clear();

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Button btn in buttons)
        {
            if (btn == null || btn.transform.IsChildOf(transform))
                continue;

            disabledGameplayButtons.Add(btn.gameObject);
            btn.gameObject.SetActive(false);
        }
    }

    private void RestoreOtherGameplayButtons()
    {
        foreach (GameObject btnObj in disabledGameplayButtons)
        {
            if (btnObj != null)
                btnObj.SetActive(true);
        }

        disabledGameplayButtons.Clear();
    }

    private void InitializeReferences()
    {
        initialized = true;
        rootRect = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        titleGroup = FindChildRect(transform, "Title");
        if (titleGroup != null)
        {
            titleText = FindChildRect(titleGroup, "TitleTxt");
            for (int i = 0; i < titleGroup.childCount; i++)
            {
                Transform child = titleGroup.GetChild(i);
                if (child.name.StartsWith("Star"))
                {
                    RectTransform rt = child.GetComponent<RectTransform>();
                    if (rt != null) headerStars.Add(rt);
                }
            }
        }

        Transform cupAnim = transform.Find("Cup Anim");
        if (cupAnim != null)
        {
            cupRect = FindChildRect(cupAnim, "CUP");
            CollectBurstChildren(cupAnim.Find("Left ") ?? cupAnim.Find("Left"));
            CollectBurstChildren(cupAnim.Find("Right"));
        }

        movesGroup = FindChildRect(transform, "Moves");
        if (movesGroup != null)
        {
            RectTransform numRect = FindChildRect(movesGroup, "text");
            if (numRect != null)
                movesNumberText = numRect.GetComponent<TextMeshProUGUI>();
        }

        nextButtonRect = FindChildRect(transform, "Next Button");

        CacheTransform(rootRect);
        CacheTransform(titleGroup);
        CacheTransform(titleText);
        foreach (RectTransform rt in headerStars) CacheTransform(rt);
        CacheTransform(cupRect);
        foreach (RectTransform rt in burstStars) CacheTransform(rt);
        foreach (RectTransform rt in burstSplashes) CacheTransform(rt);
        CacheTransform(movesGroup);
        CacheTransform(nextButtonRect);
    }

    private void CollectBurstChildren(Transform sideParent)
    {
        if (sideParent == null) return;

        for (int i = 0; i < sideParent.childCount; i++)
        {
            Transform child = sideParent.GetChild(i);
            RectTransform rt = child.GetComponent<RectTransform>();
            if (rt == null) continue;

            string lower = child.name.ToLowerInvariant();
            if (lower.Contains("star"))
                burstStars.Add(rt);
            else
                burstSplashes.Add(rt);
        }
    }

    private static RectTransform FindChildRect(Transform parent, string childName)
    {
        Transform found = parent.Find(childName);
        return found != null ? found.GetComponent<RectTransform>() : null;
    }

    private void CacheTransform(RectTransform rt)
    {
        if (rt == null) return;
        cachedTransforms.Add(new CachedTransform
        {
            rect = rt,
            scale = rt.localScale,
            anchoredPos = rt.anchoredPosition,
            rotation = rt.localRotation
        });
    }

    private void RestoreOriginalTransforms()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        foreach (CachedTransform item in cachedTransforms)
        {
            if (item.rect == null) continue;
            item.rect.localScale = item.scale;
            item.rect.anchoredPosition = item.anchoredPos;
            item.rect.localRotation = item.rotation;
        }
    }

    private void StopRunningAnimations()
    {
        foreach (Coroutine c in activeCoroutines)
        {
            if (c != null) StopCoroutine(c);
        }
        activeCoroutines.Clear();
    }

    private Coroutine StartCoroutineAndTrack(IEnumerator routine)
    {
        Coroutine c = StartCoroutine(routine);
        activeCoroutines.Add(c);
        return c;
    }

    private IEnumerator PlayWinSequence()
    {
        PrepareInitialHiddenState();

        // 1. Card pops in
        if (rootRect != null)
            yield return StartCoroutineAndTrack(PopCardIn());

        // 2. Title & header stars pop in
        if (titleText != null)
            StartCoroutineAndTrack(ScaleBounce(titleText, titlePopDuration, 1.16f));

        for (int i = 0; i < headerStars.Count; i++)
        {
            RectTransform star = headerStars[i];
            StartCoroutineAndTrack(DelayedPopWithSpin(star, 0.06f * (i + 1), 0.28f, i % 2 == 0 ? 25f : -25f));
        }

        // 3. Trophy cup bounces in
        if (cupRect != null)
        {
            yield return WaitUnscaled(0.08f);
            yield return StartCoroutineAndTrack(PopCupIn());
        }

        // 4. Confetti splashes & stars burst outward from behind the cup
        AudioManager.PlayParticleSparkle();
        List<RectTransform> allBurst = new List<RectTransform>(burstSplashes.Count + burstStars.Count);
        allBurst.AddRange(burstSplashes);
        allBurst.AddRange(burstStars);

        for (int i = 0; i < allBurst.Count; i++)
        {
            RectTransform item = allBurst[i];
            Vector2 targetPos = GetCachedAnchoredPos(item);
            Vector2 startPos = targetPos * 0.35f;
            StartCoroutineAndTrack(BurstOut(item, startPos, targetPos, i * burstStagger, burstPopDuration));
        }

        // 5. Moves box pops in + number counts up
        yield return WaitUnscaled(0.12f);
        if (movesGroup != null)
            StartCoroutineAndTrack(ScaleBounce(movesGroup, 0.28f, 1.08f));

        if (movesNumberText != null && int.TryParse(movesNumberText.text.Trim(), out int targetMoves) && targetMoves > 0)
            StartCoroutineAndTrack(CountUpMoves(movesNumberText, targetMoves, movesCountDuration));

        // 6. Next button pops in, then starts gentle loops
        yield return WaitUnscaled(0.12f);
        if (nextButtonRect != null)
            yield return StartCoroutineAndTrack(ScaleBounce(nextButtonRect, 0.3f, 1.12f));

        StartIdleCelebrationLoops();
    }

    private void PrepareInitialHiddenState()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (rootRect != null) rootRect.localScale = GetCachedScale(rootRect) * 0.65f;
        if (titleText != null) titleText.localScale = Vector3.zero;
        foreach (RectTransform star in headerStars) star.localScale = Vector3.zero;
        if (cupRect != null) cupRect.localScale = Vector3.zero;
        foreach (RectTransform rt in burstSplashes) rt.localScale = Vector3.zero;
        foreach (RectTransform rt in burstStars) rt.localScale = Vector3.zero;
        if (movesGroup != null) movesGroup.localScale = Vector3.zero;
        if (nextButtonRect != null) nextButtonRect.localScale = Vector3.zero;
    }

    private IEnumerator PopCardIn()
    {
        Vector3 targetScale = GetCachedScale(rootRect);
        Vector3 startScale = targetScale * 0.65f;
        float elapsed = 0f;

        while (elapsed < cardPopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / cardPopDuration);
            float eased = EaseOutBack(t, 1.45f);

            rootRect.localScale = Vector3.LerpUnclamped(startScale, targetScale, eased);
            canvasGroup.alpha = Mathf.Clamp01(t * 2.2f);
            yield return null;
        }

        rootRect.localScale = targetScale;
        canvasGroup.alpha = 1f;
    }

    private IEnumerator PopCupIn()
    {
        Vector3 targetScale = GetCachedScale(cupRect);
        Vector2 targetPos = GetCachedAnchoredPos(cupRect);
        Vector2 startPos = targetPos + new Vector2(0f, 45f);
        float elapsed = 0f;

        while (elapsed < cupPopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / cupPopDuration);
            float scaleEased = EaseOutBack(t, 1.85f);
            float posEased = EaseOutBounce(t);

            cupRect.localScale = targetScale * scaleEased;
            cupRect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, posEased);
            yield return null;
        }

        cupRect.localScale = targetScale;
        cupRect.anchoredPosition = targetPos;
    }

    private IEnumerator DelayedPopWithSpin(RectTransform rt, float delay, float duration, float startAngle)
    {
        if (rt == null) yield break;
        if (delay > 0f) yield return WaitUnscaled(delay);

        Vector3 targetScale = GetCachedScale(rt);
        Quaternion targetRot = GetCachedRotation(rt);
        Quaternion startRot = targetRot * Quaternion.Euler(0f, 0f, startAngle);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t, 2.0f);

            rt.localScale = targetScale * eased;
            rt.localRotation = Quaternion.SlerpUnclamped(startRot, targetRot, Mathf.Clamp01(t * 1.15f));
            yield return null;
        }

        rt.localScale = targetScale;
        rt.localRotation = targetRot;
    }

    private IEnumerator BurstOut(RectTransform rt, Vector2 startPos, Vector2 targetPos, float delay, float duration)
    {
        if (rt == null) yield break;
        if (delay > 0f) yield return WaitUnscaled(delay);

        Vector3 targetScale = GetCachedScale(rt);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t, 1.9f);

            rt.localScale = targetScale * eased;
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, eased);
            yield return null;
        }

        rt.localScale = targetScale;
        rt.anchoredPosition = targetPos;
    }

    private IEnumerator ScaleBounce(RectTransform rt, float duration, float overshootStrength)
    {
        if (rt == null) yield break;
        Vector3 targetScale = GetCachedScale(rt);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t, (overshootStrength - 1f) * 10f);
            rt.localScale = targetScale * eased;
            yield return null;
        }

        rt.localScale = targetScale;
    }

    private IEnumerator CountUpMoves(TextMeshProUGUI label, int targetValue, float duration)
    {
        float elapsed = 0f;
        int lastShown = -1;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            int current = Mathf.RoundToInt(Mathf.Lerp(0, targetValue, eased));
            if (current != lastShown)
            {
                label.text = current.ToString();
                lastShown = current;
            }
            yield return null;
        }

        label.text = targetValue.ToString();
    }

    private void StartIdleCelebrationLoops()
    {
        if (cupRect != null)
            StartCoroutineAndTrack(IdleCupFloatLoop());

        for (int i = 0; i < headerStars.Count; i++)
            StartCoroutineAndTrack(IdleTwinkleLoop(headerStars[i], i * 0.7f, 0.08f, 3.2f));

        for (int i = 0; i < burstStars.Count; i++)
            StartCoroutineAndTrack(IdleTwinkleLoop(burstStars[i], i * 0.55f, 0.1f, 2.8f));

        if (nextButtonRect != null)
            StartCoroutineAndTrack(IdleButtonPulseLoop());
    }

    private IEnumerator IdleCupFloatLoop()
    {
        Vector2 basePos = GetCachedAnchoredPos(cupRect);
        Quaternion baseRot = GetCachedRotation(cupRect);
        float time = 0f;

        while (true)
        {
            time += Time.unscaledDeltaTime;
            float yOffset = Mathf.Sin(time * cupFloatSpeed) * cupFloatAmplitude;
            float zAngle = Mathf.Sin(time * (cupFloatSpeed * 0.75f)) * cupWiggleAngle;

            cupRect.anchoredPosition = basePos + new Vector2(0f, yOffset);
            cupRect.localRotation = baseRot * Quaternion.Euler(0f, 0f, zAngle);
            yield return null;
        }
    }

    private IEnumerator IdleTwinkleLoop(RectTransform rt, float phaseOffset, float scaleAmount, float speed)
    {
        if (rt == null) yield break;
        Vector3 baseScale = GetCachedScale(rt);
        Quaternion baseRot = GetCachedRotation(rt);
        float time = phaseOffset;

        while (true)
        {
            time += Time.unscaledDeltaTime;
            float wave = Mathf.Sin(time * speed);
            rt.localScale = baseScale * (1f + wave * scaleAmount);
            rt.localRotation = baseRot * Quaternion.Euler(0f, 0f, wave * 6f);
            yield return null;
        }
    }

    private IEnumerator IdleButtonPulseLoop()
    {
        Vector3 baseScale = GetCachedScale(nextButtonRect);
        float time = 0f;

        while (true)
        {
            time += Time.unscaledDeltaTime;
            float pulse = (Mathf.Sin(time * buttonPulseSpeed) + 1f) * 0.5f;
            nextButtonRect.localScale = baseScale * (1f + pulse * buttonPulseScale);
            yield return null;
        }
    }

    private Vector3 GetCachedScale(RectTransform rt)
    {
        foreach (CachedTransform item in cachedTransforms)
            if (item.rect == rt) return item.scale;
        return Vector3.one;
    }

    private Vector2 GetCachedAnchoredPos(RectTransform rt)
    {
        foreach (CachedTransform item in cachedTransforms)
            if (item.rect == rt) return item.anchoredPos;
        return rt != null ? rt.anchoredPosition : Vector2.zero;
    }

    private Quaternion GetCachedRotation(RectTransform rt)
    {
        foreach (CachedTransform item in cachedTransforms)
            if (item.rect == rt) return item.rotation;
        return rt != null ? rt.localRotation : Quaternion.identity;
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static float EaseOutBack(float t, float overshoot)
    {
        float c3 = overshoot + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + overshoot * Mathf.Pow(t - 1f, 2f);
    }

    private static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
            return n1 * t * t;
        if (t < 2f / d1)
            return n1 * (t -= 1.5f / d1) * t + 0.75f;
        if (t < 2.5f / d1)
            return n1 * (t -= 2.25f / d1) * t + 0.9375f;

        return n1 * (t -= 2.625f / d1) * t + 0.984375f;
    }
}
