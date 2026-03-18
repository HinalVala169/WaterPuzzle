using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIIntroAnimation : MonoBehaviour
{
    [Header("Buttons List (Set Order Manually)")]
    [SerializeField] private List<Transform> buttons = new List<Transform>();

    [Header("Animation Settings")]
    [SerializeField] private float delayBetweenButtons = 0.08f;
    [SerializeField] private float scaleTime = 0.2f;

    private Coroutine animationRoutine;

    private void OnEnable()
    {
        // Stop any previous animation (important fix)
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine = StartCoroutine(AnimateButtons());
    }

   IEnumerator AnimateButtons()
{
    // Reset all buttons
    foreach (Transform btn in buttons)
    {
        btn.localScale = Vector3.zero;
    }

    yield return null;

    // Very fast stagger (key part)
    for (int i = 0; i < buttons.Count; i++)
    {
        StartCoroutine(ScaleUp(buttons[i]));
        yield return new WaitForSeconds(0.015f); // 🔥 super fast delay
    }
}
    IEnumerator ScaleUp(Transform target)
    {
        float time = 0;
        Vector3 start = Vector3.zero;
        Vector3 end = Vector3.one;

        while (time < scaleTime)
        {
            time += Time.deltaTime;

            float t = time / scaleTime;
            t = t * t * (3f - 2f * t); // SmoothStep (better feel)

            target.localScale = Vector3.Lerp(start, end, t);
            yield return null;
        }

        target.localScale = end;
    }

    public void OpenPanel()
{
    GameController.Instance.shouldGamePause = true;
    Canvas canvas = GetComponent<Canvas>();
    if (canvas != null)
        canvas.enabled = true;

    if (animationRoutine != null)
        StopCoroutine(animationRoutine);

    animationRoutine = StartCoroutine(AnimateButtons());
}

    // ✅ CLOSE: ONLY CLOSE PANEL (NO BUTTON ANIMATION)
    public void ClosePanel()
{
    GameController.Instance.shouldGamePause = false;
    if (animationRoutine != null)
        StopCoroutine(animationRoutine);

    // Disable Canvas instead of GameObject
    Canvas canvas = GetComponent<Canvas>();
    if (canvas != null)
        canvas.enabled = false;
}
}