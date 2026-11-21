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
    private bool hasCustomScale = false;
    private Vector3 baseScale;
    private Coroutine bottleReactionRoutine;
    private ParticleSystem runtimeSparkles;
    private ParticleSystem runtimeConfettiRibbons;
    private static Sprite cachedSparkleSprite;
    private static Sprite cachedRaySprite;
    private static Sprite cachedFanGlowSprite;
    private static Sprite cachedRibbonSprite;

    private static readonly Color[] SplashPalette = new Color[]
    {
        new Color(1f, 0.86f, 0.12f, 1f), // Golden Yellow
        new Color(1f, 0.20f, 0.56f, 1f), // Hot Pink
        new Color(0.12f, 0.90f, 1f, 1f), // Bright Cyan
        new Color(0.36f, 0.94f, 0.26f, 1f), // Lime Green
        new Color(0.20f, 0.50f, 1f, 1f), // Electric Blue
        new Color(1f, 0.50f, 0.12f, 1f), // Vibrant Orange
        new Color(0.74f, 0.32f, 1f, 1f)  // Vivid Purple
    };

    private void Awake()
    {
        if (!hasCustomScale && Mathf.Abs(widthScaleMultiplier - 1f) > 0.001f)
        {
            Vector3 s = transform.localScale;
            transform.localScale = new Vector3(s.x * widthScaleMultiplier, s.y, s.z);
        }

        baseScale = transform.localScale;
    }

    /// <summary>
    /// Sets the uniform (x, y, z) local scale for the bottle cap and keeps baseScale in sync.
    /// </summary>
    public void SetStopperScale(float scale)
    {
        Vector3 targetScale = new Vector3(scale, scale, scale);
        hasCustomScale = true;
        transform.localScale = targetScale;
        baseScale = targetScale;
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
        AudioManager.PlayParticleSparkle();
        PlayBottleBounce();
        PlaySplashFanBurst();
        PlayRuntimeSparkles();
        PlayRuntimeConfettiRibbons();
    }

    private void PlayEffect()
    {
        AudioManager.PlayParticleSparkle();
        if (animator != null)
        {
            SpriteRenderer effectSR = animator.GetComponent<SpriteRenderer>();
            if (effectSR != null)
                effectSR.enabled = false;
            animator.enabled = false;
        }

        PlayBottleBounce();
        PlaySplashFanBurst();
        PlayRuntimeSparkles();
        PlayRuntimeConfettiRibbons();

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

    private Color GetBottleLiquidTint()
    {
        if (transform.parent != null)
        {
            BottleController bc = transform.parent.GetComponent<BottleController>();
            if (bc != null)
            {
                Color c = bc.topColor;
                if (c.a > 0.05f)
                    return new Color(c.r, c.g, c.b, 1f);
            }
        }
        return new Color(0.75f, 0.9f, 1f, 1f);
    }

    private void PlaySplashFanBurst()
    {
        Transform hostTransform = transform.parent != null ? transform.parent : transform;
        MonoBehaviour host = hostTransform.GetComponent<MonoBehaviour>();
        if (host == null || !host.gameObject.activeInHierarchy) return;

        host.StartCoroutine(SplashFanBurstRoutine(transform.position, GetBottleLiquidTint()));
    }

    private IEnumerator SplashFanBurstRoutine(Vector3 origin, Color liquidTint)
    {
        GameObject container = new GameObject("BottleSplashFan");
        container.transform.position = origin + new Vector3(0f, -0.02f, 0f);

        // V-shaped spotlight cone tinted to the bottle's liquid color
        GameObject glowObj = new GameObject("FanGlow");
        glowObj.transform.SetParent(container.transform, false);
        glowObj.transform.localPosition = Vector3.zero;
        SpriteRenderer glowSR = glowObj.AddComponent<SpriteRenderer>();
        glowSR.sprite = GetFanGlowSprite();
        glowSR.sortingOrder = stopperSortingOrder + 3;
        Color coneColor = Color.Lerp(liquidTint, Color.white, 0.25f);
        glowSR.color = new Color(coneColor.r, coneColor.g, coneColor.b, 0f);

        // 10 slender, needle-tapered rays fanning out from the bottle mouth
        const int rayCount = 10;
        SpriteRenderer[] rays = new SpriteRenderer[rayCount];
        Vector3[] dirs = new Vector3[rayCount];
        float[] maxLengths = new float[rayCount];
        float[] widths = new float[rayCount];
        float[] startOffsets = new float[rayCount];

        int colorOffset = Random.Range(0, SplashPalette.Length);

        for (int i = 0; i < rayCount; i++)
        {
            float normalized = (float)i / (rayCount - 1); // 0..1
            float angleDeg = Mathf.Lerp(-32f, 32f, normalized) + Random.Range(-2.5f, 2.5f);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f);
            dirs[i] = dir;

            // Center rays shoot taller, outer rays are slightly shorter (fountain silhouette)
            float centerBias = 1f - Mathf.Abs(normalized - 0.5f) * 1.1f;
            maxLengths[i] = Mathf.Lerp(0.55f, 1.15f, Mathf.Clamp01(centerBias)) * Random.Range(0.85f, 1.15f);
            widths[i] = Random.Range(0.055f, 0.095f);
            startOffsets[i] = Random.Range(0.02f, 0.10f);

            GameObject rayObj = new GameObject("SplashRay_" + i);
            rayObj.transform.SetParent(container.transform, false);
            rayObj.transform.localPosition = Vector3.zero;
            rayObj.transform.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);
            rayObj.transform.localScale = new Vector3(widths[i], 0.01f, 1f);

            SpriteRenderer sr = rayObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetRaySprite();
            sr.sortingOrder = stopperSortingOrder + 4;
            sr.color = SplashPalette[(colorOffset + i) % SplashPalette.Length];
            rays[i] = sr;
        }

        float duration = 0.55f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);

            // Cone beam: quick fade in, gentle fade out
            float glowAlpha = t < 0.18f
                ? Mathf.Lerp(0f, 0.36f, t / 0.18f)
                : Mathf.Lerp(0.36f, 0f, (t - 0.18f) / 0.82f);
            glowSR.color = new Color(coneColor.r, coneColor.g, coneColor.b, glowAlpha);
            glowObj.transform.localScale = new Vector3(
                Mathf.Lerp(0.7f, 1.15f, easeOut),
                Mathf.Lerp(0.5f, 1.15f, easeOut),
                1f
            );

            for (int i = 0; i < rayCount; i++)
            {
                if (rays[i] == null) continue;

                // Rays shoot out from the mouth, then travel slightly outward while shrinking & fading
                float lengthScale = t < 0.45f
                    ? Mathf.Lerp(0.15f, maxLengths[i], easeOut / 0.83f)
                    : Mathf.Lerp(maxLengths[i], maxLengths[i] * 0.35f, (t - 0.45f) / 0.55f);

                float travel = startOffsets[i] * easeOut + (t > 0.35f ? (t - 0.35f) * 0.32f : 0f);
                rays[i].transform.localPosition = dirs[i] * travel;
                rays[i].transform.localScale = new Vector3(
                    widths[i] * (1f - t * 0.3f),
                    lengthScale,
                    1f
                );

                Color c = rays[i].color;
                c.a = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
                rays[i].color = c;
            }

            yield return null;
        }

        Destroy(container);
    }

    private ParticleSystem.MinMaxGradient CreateVibrantGradient()
    {
        Gradient colorGrad = new Gradient();
        colorGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.22f, 0.58f), 0f),   // Hot Pink
                new GradientColorKey(new Color(1f, 0.88f, 0.16f), 0.25f), // Gold
                new GradientColorKey(new Color(0.15f, 0.90f, 1f), 0.5f),  // Cyan
                new GradientColorKey(new Color(0.42f, 0.95f, 0.30f), 0.75f), // Lime
                new GradientColorKey(new Color(0.78f, 0.38f, 1f), 1f)     // Purple
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.85f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.96f, 0.65f, 1f),
                new Color(1f, 1f, 1f, 1f)
            );

            var emission = runtimeSparkles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12) });

            var shape = runtimeSparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.42f, 0.75f, 0.05f);

            var sizeOverLifetime = runtimeSparkles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.25f, 1f),
                new Keyframe(0.65f, 0.85f),
                new Keyframe(1f, 0f)
            ));

            var rotOverLifetime = runtimeSparkles.rotationOverLifetime;
            rotOverLifetime.enabled = true;
            rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

            ApplyParticleMaterial(runtimeSparkles, GetSparkleTexture(), stopperSortingOrder + 6);
        }

        Vector3 bottleCenter = transform.parent != null
            ? transform.parent.position
            : transform.position + new Vector3(0f, -0.25f, 0f);
        runtimeSparkles.transform.position = bottleCenter;
        runtimeSparkles.Play();
    }

    private void PlayRuntimeConfettiRibbons()
    {
        if (runtimeConfettiRibbons == null)
        {
            GameObject go = new GameObject("BottleWinConfettiRibbons");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            runtimeConfettiRibbons = go.AddComponent<ParticleSystem>();
            runtimeConfettiRibbons.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = runtimeConfettiRibbons.main;
            main.duration = 0.8f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.095f);
            main.gravityModifier = 0.65f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = CreateVibrantGradient();

            var emission = runtimeConfettiRibbons.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12) });

            var shape = runtimeConfettiRibbons.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 44f;
            shape.radius = 0.14f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var rotOverLifetime = runtimeConfettiRibbons.rotationOverLifetime;
            rotOverLifetime.enabled = true;
            rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var sizeOverLifetime = runtimeConfettiRibbons.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.7f),
                new Keyframe(0.2f, 1f),
                new Keyframe(1f, 0f)
            ));

            ApplyParticleMaterial(runtimeConfettiRibbons, GetRibbonTexture(), stopperSortingOrder + 5);
        }

        runtimeConfettiRibbons.transform.position = transform.position + new Vector3(0f, 0.08f, 0f);
        runtimeConfettiRibbons.Play();
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

    private static Sprite GetRaySprite()
    {
        if (cachedRaySprite != null)
            return cachedRaySprite;

        const int w = 32;
        const int h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1); // 0 = sharp bottom point, 1 = rounded outer tip
            // Sleek elongated teardrop/ray: sharp point at bottom, gently widens toward v=0.82, rounded cap at top
            float widthFactor = v < 0.82f
                ? Mathf.Pow(v / 0.82f, 0.85f)
                : Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((v - 0.82f) / 0.18f, 2f)));

            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs(((float)x / (w - 1) - 0.5f) * 2f);
                float alpha = widthFactor > 0.001f
                    ? Mathf.Clamp01((widthFactor - u) * 6f)
                    : 0f;
                pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        cachedRaySprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
        return cachedRaySprite;
    }

    private static Sprite GetFanGlowSprite()
    {
        if (cachedFanGlowSprite != null)
            return cachedFanGlowSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / (size - 1);
            float coneWidth = Mathf.Lerp(0.22f, 0.98f, v);
            float verticalFade = Mathf.Clamp01(v * 3.5f) * (1f - v);

            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Abs(((float)x / (size - 1) - 0.5f) * 2f);
                float edge = Mathf.Clamp01(1f - (u / coneWidth));
                float alpha = Mathf.Clamp01(edge * edge * verticalFade * 1.4f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        cachedFanGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f), 100f);
        return cachedFanGlowSprite;
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

    private static Texture2D GetRibbonTexture()
    {
        if (cachedRibbonSprite != null)
            return cachedRibbonSprite.texture;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - half) / half;
                float ny = (y - half) / half;
                // Small curved crescent/ribbon confetti
                float curveY = nx * nx * 0.65f - 0.15f;
                float distToArc = Mathf.Abs(ny - curveY);
                float insideX = Mathf.Clamp01((0.8f - Mathf.Abs(nx)) * 6f);
                float alpha = Mathf.Clamp01((0.22f - distToArc) * 8f) * insideX;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        cachedRibbonSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
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

        // Trigger fan splash rays, light cone, confetti ribbons, magic sparkles, and bottle bounce
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
