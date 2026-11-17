using System.Collections;
using UnityEngine;

public class StopperController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private ParticleSystem confettiPrefab;
    [SerializeField] private float dropDistance = 0.31f;
    [SerializeField] private float widthScaleMultiplier = 1.12f;
    [SerializeField] private int stopperSortingOrder = 2;
    private bool anim = false;
    private ParticleSystem runtimeConfetti;
    private static Sprite cachedConfettiSprite;

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
        // Hide the old blue sprite-sheet effect if present
        if (animator != null)
        {
            SpriteRenderer effectSR = animator.GetComponent<SpriteRenderer>();
            if (effectSR != null)
            {
                effectSR.enabled = false;
            }
            animator.enabled = false;
        }

        if (confettiPrefab != null)
        {
            ParticleSystem ps = Instantiate(confettiPrefab, transform.position, Quaternion.identity);
            ps.Play();
            Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax + 0.5f);
            return;
        }

        PlayRuntimeConfetti();
    }

    private void PlayRuntimeConfetti()
    {
        if (runtimeConfetti == null)
        {
            GameObject go = new GameObject("BottleWinConfetti");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            runtimeConfetti = go.AddComponent<ParticleSystem>();
            runtimeConfetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = runtimeConfetti.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.gravityModifier = 1.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            Gradient colorGrad = new Gradient();
            colorGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.84f, 0.15f), 0f),   // Gold
                    new GradientColorKey(new Color(1f, 0.30f, 0.45f), 0.2f), // Coral Pink
                    new GradientColorKey(new Color(0.20f, 0.85f, 1f), 0.45f), // Cyan
                    new GradientColorKey(new Color(0.45f, 0.95f, 0.35f), 0.7f), // Lime
                    new GradientColorKey(new Color(0.75f, 0.40f, 1f), 1f)    // Violet
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(colorGrad);

            var emission = runtimeConfetti.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 28) });

            var shape = runtimeConfetti.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // Shoot upward (+Y in 2D)

            var rotOverLifetime = runtimeConfetti.rotationOverLifetime;
            rotOverLifetime.enabled = true;
            rotOverLifetime.separateAxes = true;
            rotOverLifetime.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rotOverLifetime.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            var sizeOverLifetime = runtimeConfetti.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.75f, 1f),
                new Keyframe(1f, 0f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var psRenderer = runtimeConfetti.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                psRenderer.sortingOrder = stopperSortingOrder + 5;
                Shader spriteShader = Shader.Find("Sprites/Default");
                if (spriteShader != null)
                {
                    Material mat = new Material(spriteShader);
                    mat.mainTexture = GetConfettiTexture();
                    psRenderer.material = mat;
                }
            }
        }

        runtimeConfetti.transform.position = transform.position;
        runtimeConfetti.Play();
    }

    private static Texture2D GetConfettiTexture()
    {
        if (cachedConfettiSprite != null)
            return cachedConfettiSprite.texture;

        // 8x8 texture with an 8x4 white strip in the center so billboarded particles look like confetti rectangles
        Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[64];
        for (int y = 0; y < 8; y++)
        {
            Color c = (y >= 2 && y < 6) ? Color.white : Color.clear;
            for (int x = 0; x < 8; x++)
            {
                pixels[y * 8 + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cachedConfettiSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f));
        return tex;
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
