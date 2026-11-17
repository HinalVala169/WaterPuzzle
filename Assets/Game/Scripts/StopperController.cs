using System.Collections;
using UnityEngine;

public class StopperController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float dropDistance = 0.31f;
    [SerializeField] private float widthScaleMultiplier = 1.12f;
    [SerializeField] private int stopperSortingOrder = 2;
    private bool anim = false;

    private void Awake()
    {
        if (Mathf.Abs(widthScaleMultiplier - 1f) > 0.001f)
        {
            Vector3 s = transform.localScale;
            transform.localScale = new Vector3(s.x * widthScaleMultiplier, s.y, s.z);
        }
    }
    public void Animate()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sortingOrder < stopperSortingOrder)
        {
            sr.sortingOrder = stopperSortingOrder;
        }

        this.gameObject.SetActive(true);
        if (!anim)
        {
            anim = true;
            StartCoroutine(MoveAnimation());
        }
        else if (animator != null)
        {
            PlayEffect();
        }
    }

    private void PlayEffect()
    {
        // PlayEffect();
        animator.Play("DoneEffect");
    }

    //animação de movimentação
    private IEnumerator MoveAnimation()
    {
        float duration = 0.5f;

        Vector3 inicialPosition = transform.localPosition;

        Vector3 finalPosition = new Vector3(inicialPosition.x, inicialPosition.y - dropDistance, inicialPosition.z);

        float t = 0.25f;

        while (t <= duration)
        {
            yield return new WaitForEndOfFrame();

            t += Time.deltaTime;
        }

        t = 0;

        while (t <= duration)
        {
            transform.localPosition = Vector3.Lerp(inicialPosition, finalPosition, (t / duration));

            t += Time.deltaTime * 2;

            yield return new WaitForEndOfFrame();
        }

        transform.localPosition = finalPosition;

        PlayEffect();
    }
}
