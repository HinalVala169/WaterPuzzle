using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AdvancedPopup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup popupGroup;
    [SerializeField] private CanvasGroup backgroundGroup; // dim bg

    [Header("Animation")]
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private AnimationCurve curve;

    private bool isAnimating;

    private void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(Show());
    }

    public void Hide()
    {
        if (!isAnimating)
            StartCoroutine(HideAnim());
    }

    IEnumerator Show()
    {
        isAnimating = true;

        if (panel != null) panel.localScale = Vector3.zero;
        if (popupGroup != null) popupGroup.alpha = 0;
        if (backgroundGroup != null) backgroundGroup.alpha = 0;

        float time = 0;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = curve != null && curve.length > 0 ? curve.Evaluate(time / duration) : Mathf.Clamp01(time / duration);

            if (panel != null) panel.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            if (popupGroup != null) popupGroup.alpha = t;
            if (backgroundGroup != null) backgroundGroup.alpha = t * 0.6f; // dim effect

            yield return null;
        }

        if (panel != null) panel.localScale = Vector3.one;
        if (popupGroup != null) popupGroup.alpha = 1;
        if (backgroundGroup != null) backgroundGroup.alpha = 0.6f;

        isAnimating = false;
    }

    IEnumerator HideAnim()
    {
        isAnimating = true;

        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = curve.Evaluate(time / duration);

            panel.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            popupGroup.alpha = 1 - t;
            backgroundGroup.alpha = (1 - t) * 0.6f;

            yield return null;
        }

        ResetState();
        gameObject.SetActive(false);

        isAnimating = false;
    }

    private void ResetState()
    {
        panel.localScale = Vector3.one;
        popupGroup.alpha = 1;
        backgroundGroup.alpha = 0;
    }
}