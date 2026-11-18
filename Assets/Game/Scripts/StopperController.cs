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
    private Vector3 baseScale;
    private Coroutine bottleReactionRoutine;
    private ParticleSystem runtimeSparkles;
    private ParticleSystem runtimeMagicDust;
    private ParticleSystem runtimeHaloRing;
    private static Sprite cachedSparkleSprite;
    private static Sprite cachedGlowOrbSprite;
    private static Sprite cachedRingSprite;

    private void Awake()
    {
        if (Mathf.Abs(widthScaleMultiplier - 1f) > 0.001f)
        {
            Vector3 s = transform.localScale;
            transform.localScale = new Vector3(s.x * widthScaleMultiplier, s.y, s.z);
        }

        baseScale = transform.localScale;
    }

    public void Animate()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sortingOrder < stopperSortingOrder)
        {
            sr.sortingOrder = stopperSortingOrder;
        }

        gameObject.SetActive(true);
        if (!anim)
        {
            anim = true;
            StartCoroutine(MoveAnimation());
        }
        else
        {
            PlayEffect();
        }
    }

    public void PlayCompletionEffectOnly()
    {
        PlayBottleBounce();
        PlayRuntimeSparkles();
        PlayRuntimeMagicDust();
    }

    private void PlayEffect()
    {
        // Hide the old blue sprite-sheet effect if present
        if (animator != null)
        {
            SpriteRenderer effectSR = animator.GetComponent<SpriteRenderer>();
            if (effectSR != null)
                effectSR.enabled = false;
            animator.enabled = false;
        }

        PlayBottleBounce();
        PlayRuntimeSparkles();
        PlayRuntimeMagicDust();
        PlayRuntimeHaloRing();

        if (confettiPrefab != null)
        {
            ParticleSystem ps = Instantiate(confettiPrefab, transform.position, Quaternion.identity);
            ps.Play();
            Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax + 0.5f);
        }
    }

    private void PlayBottleBounce()
    {
        Transform bottle = transform.parent;
        if (bottle == null || !bottle.gameObject.activeInHierarchy) return;

        MonoBehaviour host = bottle.GetComponent<MonoBehaviour>();
        if (host == null) return;

        if (bottleReactionRoutine != null)
            host.StopCoroutine(bottleReactionRoutine);

        bottleReactionRoutine = host.StartCoroutine(BottleCompleteReaction(bottle));
    }

    private IEnumerator BottleCompleteReaction(Transform bottle)
    {
        Vector3 origScale = bottle.localScale;
        float duration = 0.28f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float squashX;
            float squashY;
            if (t < 0.35f)
            {
                float k = t / 0.35f;
                squashX = Mathf.Lerp(1f, 1.09f, k);
                squashY = Mathf.Lerp(1f, 0.91f, k);
            }
            else
            {
                float k = (t - 0.35f) / 0.65f;
                float wave = Mathf.Sin(k * Mathf.PI) * 0.05f * (1f - k);
                squashX = Mathf.Lerp(1.09f, 1f, k) - wave;
                squashY = Mathf.Lerp(0.91f, 1f, k) + wave;
            }

            bottle.localScale = new Vector3(origScale.x * squashX, origScale.y * squashY, origScale.z);
            yield return null;
        }

        bottle.localScale = origScale;
        bottleReactionRoutine = null;
    }

    private ParticleSystem.MinMaxGradient CreatePastelMagicGradient()
    {
        Gradient colorGrad = new Gradient();
        colorGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),           // Pure White
                new GradientColorKey(new Color(1f, 0.94f, 0.58f), 0.25f), // Soft Starlight Gold
                new GradientColorKey(new Color(1f, 0.72f, 0.92f), 0.50f), // Pastel Pink
                new GradientColorKey(new Color(0.62f, 0.96f, 1f), 0.75f), // Pastel Aqua
                new GradientColorKey(new Color(0.84f, 0.72f, 1f), 1f)     // Soft Lavender
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );

        var minMax = new ParticleSystem.MinMaxGradient(colorGrad);
        minMax.mode = ParticleSystemGradientMode.RandomColor;
        return minMax;
    }

    private void PlayRuntimeSparkles()
    {
        if (runtimeSparkles == null)
        {
            GameObject go = new GameObject("BottleWinSparkles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -0.25f, 0f);

            runtimeSparkles = go.AddComponent<ParticleSystem>();
            runtimeSparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = runtimeSparkles.main;
            main.duration = 0.9f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
            main.gravityModifier = -0.18f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = CreatePastelMagicGradient();

            var emission = runtimeSparkles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 18) });

            var shape = runtimeSparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.45f, 0.8f, 0.05f);

            var sizeOverLifetime = runtimeSparkles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.22f, 1.1f),
                new Keyframe(0.55f, 0.85f),
                new Keyframe(1f, 0f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var rotOverLifetime = runtimeSparkles.rotationOverLifetime;
            rotOverLifetime.enabled = true;
            rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-1.8f, 1.8f);

            ApplyParticleMaterial(runtimeSparkles, GetSparkleTexture(), stopperSortingOrder + 6);
        }

        Vector3 bottleCenter = transform.parent != null
            ? transform.parent.position
            : transform.position + new Vector3(0f, -0.25f, 0f);
        runtimeSparkles.transform.position = bottleCenter;
        runtimeSparkles.Play();
    }

    private void PlayRuntimeMagicDust()
    {
        if (runtimeMagicDust == null)
        {
            GameObject go = new GameObject("BottleWinMagicDust");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            runtimeMagicDust = go.AddComponent<ParticleSystem>();
            runtimeMagicDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = runtimeMagicDust.main;
            main.duration = 0.85f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.gravityModifier = -0.25f; // Dreamy upward float
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = CreatePastelMagicGradient();

            var emission = runtimeMagicDust.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 22) });

            var shape = runtimeMagicDust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;

            var colorOverLifetime = runtimeMagicDust.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fadeGrad = new Gradient();
            fadeGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.2f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGrad);

            var sizeOverLifetime = runtimeMagicDust.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.3f),
                new Keyframe(0.3f, 1f),
                new Keyframe(1f, 0f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ApplyParticleMaterial(runtimeMagicDust, GetGlowOrbTexture(), stopperSortingOrder + 5);
        }

        runtimeMagicDust.transform.position = transform.position;
        runtimeMagicDust.Play();
    }

    private void PlayRuntimeHaloRing()
    {
        if (runtimeHaloRing == null)
        {
            GameObject go = new GameObject("BottleWinHaloRing");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            runtimeHaloRing = go.AddComponent<ParticleSystem>();
            runtimeHaloRing.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = runtimeHaloRing.main;
            main.duration = 0.4f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.42f;
            main.startSpeed = 0f;
            main.startSize = 0.72f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1f, 0.96f, 0.75f, 0.85f);

            var emission = runtimeHaloRing.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var shape = runtimeHaloRing.shape;
            shape.enabled = false;

            var sizeOverLifetime = runtimeHaloRing.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.25f),
                new Keyframe(1f, 1.35f)
            ));

            var colorOverLifetime = runtimeHaloRing.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fadeGrad = new Gradient();
            fadeGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGrad);

            ApplyParticleMaterial(runtimeHaloRing, GetRingTexture(), stopperSortingOrder + 4);
        }

        runtimeHaloRing.transform.position = transform.position;
        runtimeHaloRing.Play();
    }

    private static void ApplyParticleMaterial(ParticleSystem ps, Texture2D tex, int sortingOrder)
    {
        var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (psRenderer == null) return;

        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        psRenderer.sortingOrder = sortingOrder;
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            Material mat = new Material(spriteShader);
            mat.mainTexture = tex;
            psRenderer.material = mat;
        }
    }

    private static Texture2D GetSparkleTexture()
    {
        if (cachedSparkleSprite != null)
            return cachedSparkleSprite.texture;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - half) / half;
                float dy = Mathf.Abs(y - half) / half;

                float star = Mathf.Clamp01(1f - Mathf.Pow(dx * dy, 0.32f) * 2.2f);
                float radial = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 3.2f);
                float alpha = Mathf.Clamp01(star * radial * 1.45f + core * 0.5f);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        cachedSparkleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return tex;
    }

    private static Texture2D GetGlowOrbTexture()
    {
        if (cachedGlowOrbSprite != null)
            return cachedGlowOrbSprite.texture;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 1.8f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        cachedGlowOrbSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return tex;
    }

    private static Texture2D GetRingTexture()
    {
        if (cachedRingSprite != null)
            return cachedRingSprite.texture;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(dist - 0.72f) * 6.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, ring * ring);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        cachedRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return tex;
    }

    private IEnumerator MoveAnimation()
    {
        Vector3 inicialPosition = transform.localPosition;
        Vector3 finalPosition = new Vector3(inicialPosition.x, inicialPosition.y - dropDistance, inicialPosition.z);

        // Brief anticipation pause before dropping
        yield return new WaitForSeconds(0.12f);

        // Fast accelerating drop onto the bottle mouth
        float dropDuration = 0.18f;
        float t = 0f;
        while (t < dropDuration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / dropDuration);
            float eased = progress * progress;
            transform.localPosition = Vector3.Lerp(inicialPosition, finalPosition, eased);
            yield return null;
        }
        transform.localPosition = finalPosition;

        // Trigger magic sparkles, glowing dust, halo ring, and bottle bounce
        PlayEffect();

        // Cork squash-and-stretch settle
        float bounceDuration = 0.2f;
        t = 0f;
        while (t < bounceDuration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / bounceDuration);
            float squash = Mathf.Sin(progress * Mathf.PI);
            float sx = Mathf.Lerp(1.18f, 1f, progress);
            float sy = Mathf.Lerp(0.76f, 1f, progress) + squash * 0.08f;
            transform.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z);
            yield return null;
        }

        transform.localScale = baseScale;
    }
}
