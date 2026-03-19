using System.Collections;
using UnityEngine;

public class PopupAnimation : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float duration = 0.25f;

    private void OnEnable()
    {
        StartCoroutine(ShowAnim());
    }

    private void OnDisable()
    {
        ResetState(); // ✅ RESET WHEN DISABLED
    }

    public void Hide()
    {
        StartCoroutine(HideAnim());
    }

    IEnumerator ShowAnim()
    {
        panel.localScale = Vector3.zero;
        canvasGroup.alpha = 0;

        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            panel.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            canvasGroup.alpha = t;

            yield return null;
        }

        panel.localScale = Vector3.one;
        canvasGroup.alpha = 1;
    }

    IEnumerator HideAnim()
    {
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            panel.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            canvasGroup.alpha = 1 - t;

            yield return null;
        }

        gameObject.SetActive(false);
    }

    // ✅ RESET FUNCTION
    private void ResetState()
    {
        panel.localScale = Vector3.one;
        canvasGroup.alpha = 1;
    }
}