using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BottleController : MonoBehaviour
{
    [SerializeField] private Color[] bottleColors;
    [SerializeField] private int[] colorsIndex;

    // ── Color Palettes ────────────────────────────────────────────────────────
    // Add one entry per color-palette card shown in your UI.
    // Palette 0 = first card, Palette 1 = second card, etc.
    // Each palette needs exactly as many colors as your highest colorsIndex value + 1.
    [System.Serializable]
    public class ColorPaletteEntry
    {
        [Tooltip("12 colors — slot index matches colorsIndex values in LevelBuilder")]
        public Color[] colors = new Color[12];
    }

    [Header("Color Palettes (one per card, in card order)")]
    [SerializeField] private List<ColorPaletteEntry> colorPalettes = new List<ColorPaletteEntry>();

    [SerializeField] private SpriteRenderer bottleMaskSR;

    [SerializeField] private float timeToRotate = 1f;
    [SerializeField] private float moveAnimationTime = 0.5f;

    [SerializeField] private AnimationCurve ScaleAndRotationMultiplierCurve;
    [SerializeField] private AnimationCurve FillAmountCurve;
    [SerializeField] private AnimationCurve RotationSpeedMultiplier;

    [SerializeField] private float[] fillAmounts;
    [SerializeField] private float[] rotationValues;

    private int rotationIndex = 0;

    //[Range(0, 4)]
    public int numberOfColorsInBottle;

    public Color topColor { get; private set; }
    private int numberOfTopColorLayers = 1;

    public BottleController bottleControllerRef;

    [SerializeField] private int numberOfColorsToTransfer = 0;

    [SerializeField] private Transform leftRotationPoint;
    [SerializeField] private Transform rightRotationPoint;
    private Transform chosenRotationPoint;

    private float directionMultiplier = 1f;
    private Vector3 originalPosition;
    private Vector3 startPosition;
    private Vector3 endPosition;
    [SerializeField] private LineRenderer lineRenderer;

    private bool isFilling;
    private bool isBeingFilled;

    //a quantidade de cores que vai ter no outro recipiente - para resolver o bug gráfico da quantidade de cores
    private int newNumberOfColorsInOtherBottle;

    //variaveis para condição de vitoria
    private BottlesController bc;
    public int index { get; private set; }
    private bool underAnimation;

    public StopperController stopper;
    private bool stopperCommand;//comando para assim que possível ativar a rolha
    private Collider2D col;
    [SerializeField] private float pourHeightOffset = 1.5f;

    [Header("Pour Effects")]
    [SerializeField] private Transform pourStartPoint;
    [SerializeField] private Transform pourEndPoint;
    [SerializeField] private ParticleSystem pourParticles;
    [SerializeField] private ParticleSystem splashParticles;
    private bool isPouringStreamActive = false;

    [SerializeField] private float localLipXOffset = 0.11f;
    [SerializeField] private float localLipYOffset = 0.48f;
    [SerializeField] private float pourMouthYOffset = 0.68f;
    [SerializeField] private float pourEndTargetYOffset = 0.0f;

    // Fallback used only when a bottle has no pourEndPoint child assigned/found.
    // Tune this in the Inspector to match your fill mask's actual max height
    // (this used to be a hardcoded "0.75f" buried in code, which is why the
    // stream could land outside the bottle if it didn't match your shader/mask setup).
    [SerializeField] private float fallbackMaxFillHeight = 0.75f;

    private Transform pourParticlesOriginalParent;
    private Transform splashParticlesOriginalParent;

    private void PlayPourEffects()
    {
        if (pourParticles != null)
        {
            var main = pourParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = topColor;
            if (pourParticlesOriginalParent == null)
                pourParticlesOriginalParent = pourParticles.transform.parent;
            pourParticles.transform.SetParent(null);
            pourParticles.Play();
        }
        if (splashParticles != null)
        {
            var main = splashParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = topColor;
            if (splashParticlesOriginalParent == null)
                splashParticlesOriginalParent = splashParticles.transform.parent;
            splashParticles.transform.SetParent(null);
            splashParticles.Play();
        }
    }

    private void StopPourEffects()
    {
        if (pourParticles != null)
        {
            pourParticles.Stop();
            if (pourParticlesOriginalParent != null && pourParticles.transform.parent == null)
            {
                pourParticles.transform.SetParent(pourParticlesOriginalParent);
            }
        }
        if (splashParticles != null)
        {
            splashParticles.Stop();
            if (splashParticlesOriginalParent != null && splashParticles.transform.parent == null)
            {
                splashParticles.transform.SetParent(splashParticlesOriginalParent);
            }
        }
    }

        [Header("Theme")]
    [SerializeField] private SpriteRenderer bottleUpRenderer;
    [SerializeField] private SpriteRenderer bottleFillRenderer;

    private void Awake()
    {
        bottleColors = new Color[4];
        col = GetComponent<Collider2D>();

    }

    private void Start()
    {
        //aqui envia para o material o valor contido em fillAmounts na posição numberOfColorsInBottle
        //fillAmounts guarda os valores de tamanho do material
        //numberOfColorsInBottle é a quantidade de cores dentro do recipiente
        bottleMaskSR.material.SetFloat("_FillAmount", fillAmounts[numberOfColorsInBottle]);

        //aqui captura a posição original quando inicia o jogo
        originalPosition = transform.position;

        //método que vai enviar para o material as cores dentro do recipiente
        //UpdateColorsOnShader();

        //método que vai verificar a quantidade de líquidos dentro do recipiente e alterar valores dentro das variáveis para o funcionamento do jogo
        UpdateTopColorValues();

        // Auto-find components if not assigned
        if (lineRenderer == null)
        {
            lineRenderer = GetComponentInChildren<LineRenderer>();
        }
        if (pourParticles == null)
        {
            pourParticles = GetComponentInChildren<ParticleSystem>();
        }
        if (pourStartPoint == null)
        {
            pourStartPoint = FindDeepChild(transform, "PourStartPoint");
        }
        if (pourEndPoint == null)
        {
            pourEndPoint = FindDeepChild(transform, "PourEndPoint");
        }

        if (pourStartPoint == null)
            Debug.LogWarning($"[{gameObject.name}] PourStartPoint not found (searched all children). Falling back to computed lip offset — assign it in the Inspector or add a child named exactly 'PourStartPoint' for accurate pouring.");
        if (pourEndPoint == null)
            Debug.LogWarning($"[{gameObject.name}] PourEndPoint not found (searched all children). Falling back to computed fill-surface offset — assign it in the Inspector or add a child named exactly 'PourEndPoint' for accurate pouring.");
    }

    // Recursively searches this transform's full hierarchy (not just direct children)
    // for a child with the exact given name. transform.Find() only checks direct
    // children, which silently returns null if PourStartPoint/PourEndPoint are
    // nested a level deeper (e.g. under "Up_Part") — this fixes that.
    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            Transform result = FindDeepChild(child, name);
            if (result != null)
                return result;
        }
        return null;
    }

public void SetTheme(
    Sprite upSprite,
    Sprite fillSprite)
{
    if (bottleUpRenderer != null
        && upSprite != null)
    {
        bottleUpRenderer.sprite =
            upSprite;
    }

    if (bottleFillRenderer != null
        && fillSprite != null)
    {
        bottleFillRenderer.sprite =
            fillSprite;
    }
}

    private void Update()
    {
        if (GameController.Instance != null)
        {
            col.enabled = !GameController.Instance.shouldGamePause;
        }
        if (stopperCommand && isBeingFilled)
        {
            stopperCommand = false;

            if (UIController.IsBottleTopEnabled) // ✅ ADD THIS
                stopper.Animate();
        }
    }

    private void LateUpdate()
    {
        if (isPouringStreamActive)
        {
            UpdatePouringStream();
        }
    }

    private Vector3 GetLocalPourStartOffset()
    {
        if (pourStartPoint != null)
        {
            return transform.InverseTransformPoint(pourStartPoint.position);
        }

        float lipX = localLipXOffset;
        float lipY = localLipYOffset;

        SpriteRenderer refSR = (bottleFillRenderer != null && bottleFillRenderer.sprite != null)
            ? bottleFillRenderer
            : bottleUpRenderer;

        if (refSR != null && refSR.sprite != null)
        {
            Bounds b = refSR.sprite.bounds;
            Vector3 localScale = refSR.transform.localScale;
            Vector3 localPos = refSR.transform.localPosition;
            lipY = localPos.y + (b.max.y * localScale.y * 0.96f);
            lipX = localPos.x + (b.max.x * localScale.x * 0.72f);
        }

        float signedLipX = (chosenRotationPoint == leftRotationPoint) ? -lipX : lipX;
        return new Vector3(signedLipX, lipY, 0f);
    }

    private void UpdatePouringStream()
    {
        if (lineRenderer == null || bottleControllerRef == null) return;

        // Determine start point of the pour stream (aligned to the tilted mouth lip)
        Vector3 startPoint = (pourStartPoint != null)
            ? pourStartPoint.position
            : transform.TransformPoint(GetLocalPourStartOffset());

        // Determine end point of the pour stream (where it enters the destination bottle, tracking the rising liquid level)
        Vector3 endPoint;
        if (bottleControllerRef.pourEndPoint != null)
        {
            endPoint = bottleControllerRef.pourEndPoint.position;
        }
        else
        {
            float currentFill = (bottleControllerRef.bottleMaskSR != null && bottleControllerRef.bottleMaskSR.material != null && bottleControllerRef.bottleMaskSR.material.HasProperty("_FillAmount")) ? 
                bottleControllerRef.bottleMaskSR.material.GetFloat("_FillAmount") : 
                bottleControllerRef.fillAmounts[bottleControllerRef.numberOfColorsInBottle];
            
            float maxFillHeight = bottleControllerRef.fallbackMaxFillHeight;
            if (bottleControllerRef.bottleMaskSR != null && bottleControllerRef.bottleMaskSR.material != null && bottleControllerRef.bottleMaskSR.material.HasProperty("_MaxFillHeight"))
            {
                maxFillHeight = bottleControllerRef.bottleMaskSR.material.GetFloat("_MaxFillHeight");
            }
            
            float localSurfaceY = (currentFill * maxFillHeight) + pourEndTargetYOffset;
            endPoint = bottleControllerRef.transform.TransformPoint(new Vector3(0f, localSurfaceY, 0f));
        }

        // Keep the falling stream vertical from the pouring lip into the target bottle
        endPoint.x = startPoint.x;
        endPoint.z = startPoint.z;

        // Ensure LineRenderer uses World Space
        lineRenderer.useWorldSpace = true;
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        // Calculate dynamic Sorting Order so the stream renders in front of both bottles
        int sourceOrder = transform.GetComponent<SpriteRenderer>().sortingOrder;
        int destOrder = bottleControllerRef.transform.GetComponent<SpriteRenderer>().sortingOrder;
        int highestOrder = Mathf.Max(sourceOrder, destOrder);
        lineRenderer.sortingOrder = highestOrder + 1;

        // Compensate for parent scale if nested in scaled UI/Canvas
        Transform lrParent = lineRenderer.transform.parent;
        if (lrParent != null)
        {
            Vector3 parentLossyScale = lrParent.lossyScale;
            if (parentLossyScale.x != 0 && parentLossyScale.y != 0)
            {
                lineRenderer.transform.localScale = new Vector3(1f / parentLossyScale.x, 1f / parentLossyScale.y, 1f / parentLossyScale.z);
            }
        }

        // Calculate direction for particle orientation
        Vector3 direction = (endPoint - startPoint).normalized;

        // Update Particle System position, rotation, simulation space, color, scale, and sorting order
        if (pourParticles != null)
        {
            pourParticles.transform.position = startPoint;
            if (direction != Vector3.zero)
            {
                // Keep the particle system flat in the 2D XY plane with local -Y pointing along the pour stream
                pourParticles.transform.rotation = Quaternion.FromToRotation(Vector3.down, direction);
            }

            // Compensate parent scale (only if still attached to parent)
            Transform pParent = pourParticles.transform.parent;
            if (pParent != null)
            {
                Vector3 parentLossyScale = pParent.lossyScale;
                if (parentLossyScale.x != 0)
                {
                    pourParticles.transform.localScale = new Vector3(1f / parentLossyScale.x, 1f / parentLossyScale.y, 1f / parentLossyScale.z);
                }
            }
            else
            {
                pourParticles.transform.localScale = Vector3.one;
            }

            // Set Sorting Order
            var renderer = pourParticles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = highestOrder + 2;
            }
        }

        if (splashParticles != null)
        {
            splashParticles.transform.position = endPoint;
            splashParticles.transform.rotation = Quaternion.identity;

            // Compensate parent scale (only if still attached to parent)
            Transform sParent = splashParticles.transform.parent;
            if (sParent != null)
            {
                Vector3 parentLossyScale = sParent.lossyScale;
                if (parentLossyScale.x != 0)
                {
                    splashParticles.transform.localScale = new Vector3(1f / parentLossyScale.x, 1f / parentLossyScale.y, 1f / parentLossyScale.z);
                }
            }
            else
            {
                splashParticles.transform.localScale = Vector3.one;
            }

            // Set Sorting Order
            var renderer = splashParticles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = highestOrder + 2;
            }
        }
    }

    //método que vai fazer a transferência de líquidos
    public void StartColorTransfer()
    {
        if (GameController.Instance != null && GameController.Instance.shouldGamePause)
        return; 
        //pegando a quantidade de cores no outro recipiente
        newNumberOfColorsInOtherBottle = bottleControllerRef.numberOfColorsInBottle;

        //indica que o outro recipiente está enchendo
        bottleControllerRef.SetIsBeingFilled(true);
        isFilling = true;

        //método que vai verificar para qual lado o recipiente deve rotacionar 
        ChoseRotationPointAndDirection();

        //aqui vai verificar quantas cores vai passar para o outro recipiente
        //verificando qual é o menor número entre a quantidade de cores iguais no topo ou a quantidade de espaços vazios no outro recipiente
        numberOfColorsToTransfer = Mathf.Min(numberOfTopColorLayers, 4 - bottleControllerRef.numberOfColorsInBottle);

        //somando a quantidade de cores que tem que passar
        newNumberOfColorsInOtherBottle += numberOfColorsToTransfer;

        //criando histórico
        bc.CreateHistoric(index, GetColors(), numberOfColorsInBottle, bottleControllerRef.index, bottleControllerRef.GetColors(), bottleControllerRef.numberOfColorsInBottle);

        //transferência lógica
        for (int i = 0; i < numberOfColorsToTransfer; i++)
        {
            bottleControllerRef.SetColors(bottleControllerRef.numberOfColorsInBottle + i, topColor);
        }

        //gambiarra pra resolver o bug visual gerado pela falta de sincronia entre o recipiente que joga o liquido e o recipiente que recebe
        if (bottleControllerRef.numberOfColorsInBottle < 4)
        {
            for (int i = bottleControllerRef.numberOfColorsInBottle; i < 4; i++)
            {
                bottleControllerRef.SetColors(i, topColor);
            }
        }

        //passando as referencias das cores
        bottleControllerRef.SetTransferredColorsIndex(colorsIndex[numberOfColorsInBottle - 1]);

        //método que vai enviar para o material as cores dentro do recipiente
        bottleControllerRef.UpdateColorsOnShader();

        //método que vai calcular o quanto o frasco deve girar
        CalculateRotationIndex(4 - bottleControllerRef.numberOfColorsInBottle);

        //mudando as sorting layer dos 2 objetos para eles ficarem na frente dos outros
        transform.GetComponent<SpriteRenderer>().sortingOrder += 2;
        bottleMaskSR.sortingOrder += 2;

        bottleControllerRef.numberOfColorsInBottle += numberOfColorsToTransfer;

        //iniciando a animação
        StartCoroutine(MoveBottle());
    }

    public void ReloadColors(int[] colors, int value)
    {
        colorsIndex = colors;//array que vai indicar as cores que devem ser usadas

        numberOfColorsInBottle = value;//quantidade de cores na bottle

        //chamando o método que vai recarregar as corres
        LoadColorsPallet();
    }

    //método que vai enviar para o material as cores dentro do recipiente
    public void UpdateColorsOnShader()
    {
        bottleMaskSR.material.SetColor("_C1", bottleColors[0]);
        bottleMaskSR.material.SetColor("_C2", bottleColors[1]);
        bottleMaskSR.material.SetColor("_C3", bottleColors[2]);
        bottleMaskSR.material.SetColor("_C4", bottleColors[3]);
    }

    //método que vai verificar a quantidade de líquidos dentro do recipiente e alterar valores dentro das variáveis para o funcionamento do jogo
    public void UpdateTopColorValues()
    {
        //cadeia de ifs que vai verificar se os líquidos no topo são iguais
        if (numberOfColorsInBottle != 0)
        {
            numberOfTopColorLayers = 1;

            topColor = bottleColors[numberOfColorsInBottle - 1];

            if (numberOfColorsInBottle == 4)
            {
                if (bottleColors[3].Equals(bottleColors[2]))
                {
                    numberOfTopColorLayers = 2;
                    if (bottleColors[2].Equals(bottleColors[1]))
                    {
                        numberOfTopColorLayers = 3;
                        if (bottleColors[1].Equals(bottleColors[0]))
                            numberOfTopColorLayers = 4;

                    }
                }
            }
            else if (numberOfColorsInBottle == 3)
            {
                if (bottleColors[2].Equals(bottleColors[1]))
                {
                    numberOfTopColorLayers = 2;
                    if (bottleColors[1].Equals(bottleColors[0]))
                        numberOfTopColorLayers = 3;
                }
            }
            else if (numberOfColorsInBottle == 2)
            {
                if (bottleColors[1].Equals(bottleColors[0]))
                    numberOfTopColorLayers = 2;
            }

            //definindo o quanto o recipiente deve rotacionar quando fizer a animação
            rotationIndex = 3 - (numberOfColorsInBottle - numberOfTopColorLayers);
        }
    }

    //método chamado pela classe GameController para verificar se pode transferir líquido de um recipiente para o outro
    public bool FillBottleCheck(Color colorToCheck)
    {
        //verifica se está vazio
        if (numberOfColorsInBottle == 0)
            return true;
        else
        {
            //verifica se está cheio
            if (numberOfColorsInBottle == 4)
                return false;
            else
            {
                //verifica se as cores são iguais
                if (topColor.Equals(colorToCheck))
                    return true;
                else
                    return false;
            }
        }
    }

    public void NewOriginalPosition(Vector3 position)
    {
        originalPosition = position;
    }

    //método que vai calcular o quanto o frasco deve girar
    private void CalculateRotationIndex(int numberOfEmptySpacesInSecondBottle)
    {
        rotationIndex = 3 - (numberOfColorsInBottle - Mathf.Min(numberOfEmptySpacesInSecondBottle, numberOfTopColorLayers));
    }

    //método que vai "encher" o outro recipiente, passando para o _FillAmount do material o valor que deve ter
    private void FillUp(float fillAmountToAdd)
    {
        bottleMaskSR.material.SetFloat("_FillAmount", bottleMaskSR.material.GetFloat("_FillAmount") + fillAmountToAdd);
    }

    //método para ajustar o fill amount e evitar o bug dos recipientes com quantidades diferentes de líquidos
    public void AdjustFillAmount(int value)
    {

        bottleMaskSR.material.SetFloat("_FillAmount", fillAmounts[value]);
    }

    //método que vai verificar para qual lado o recipiente deve rotacionar 
    private void ChoseRotationPointAndDirection()
    {
       bool isRightBottle = transform.position.x > bottleControllerRef.transform.position.x;

        if (isRightBottle)
        {
            chosenRotationPoint = leftRotationPoint;
            directionMultiplier = 1f; // tilt left
        }
        else
        {
            chosenRotationPoint = rightRotationPoint;
            directionMultiplier = -1f; // tilt right
        }
    }

    public bool CheckIfItsDone()
    {
        if (numberOfColorsInBottle == 4)
        {
            if (bottleColors[1].Equals(bottleColors[0]) &&
                bottleColors[2].Equals(bottleColors[1]) &&
                bottleColors[3].Equals(bottleColors[2]))
            {
                return true;
            }
        }
        return false;
    }

    public void SetTransferredColorsIndex(int value)
    {
        for (int x = numberOfColorsInBottle; x < 4; x++)
        {
            colorsIndex[x] = value;
        }
    }

    //método que vai carregar as corres de acordo com a paleta selecionada
    public void LoadColorsPallet()
    {
        // paletteIndex = the card the player selected (0 = first card, 1 = second card …)
        int paletteIndex = (GameManager.Instance != null)
            ? GameManager.Instance.selectedColorPalette
            : PlayerPrefs.GetInt("SelectedColor", 0);

        Color[] pallet = null;

        // ── Source 1: Get colors directly from the selected card in ThemeSelectionManager
        ThemeSelectionManager themeMgr = ThemeSelectionManager.Instance != null
            ? ThemeSelectionManager.Instance
            : FindObjectOfType<ThemeSelectionManager>();

        if (themeMgr != null)
        {
            pallet = themeMgr.GetCurrentPaletteColors();
        }

        // ── Source 2: Fall back to this prefab's own colorPalettes list
        if ((pallet == null || pallet.Length == 0) && colorPalettes != null && colorPalettes.Count > 0)
        {
            paletteIndex = Mathf.Clamp(paletteIndex, 0, colorPalettes.Count - 1);
            pallet = colorPalettes[paletteIndex].colors;
        }

        if (pallet == null || pallet.Length == 0)
        {
            return;
        }

        for (int x = 0; x < 4; x++)
        {
            int slot = Mathf.Clamp(colorsIndex[x], 0, pallet.Length - 1);
            bottleColors[x] = pallet[slot];
        }

        UpdateColorsOnShader();
        bottleMaskSR.material.SetFloat("_FillAmount", fillAmounts[numberOfColorsInBottle]);
        UpdateTopColorValues();
    }

    //método que vai chamar a animação de seleção
    public void Selected()
    {
        AnimateBottle(new Vector3(transform.position.x, transform.position.y + 0.15f, transform.position.z), 0.25f);
    }

    //método que vai chamar a animação de des seleção
    public void Unselected()
    {
        AnimateBottle(new Vector3(transform.position.x, transform.position.y - 0.15f, transform.position.z), 0.25f);
    }

    //========================================= Sets & Gets =========================================

    public void setIndex(int value, BottlesController bottlesController/*, GameController gameController*/)
    {
        index = value;
        bc = bottlesController;
    }

    public void SetColors(int pos, Color color)
    {
        bottleColors[pos] = color;
    }

    public void SetIsBeingFilled(bool value)
    {
        isBeingFilled = value;
    }

    //método que vai receber as cores desse recipiente no momento em que ele estiver sendo criado
    public void SetColorsIndex(int[] colors, int numberOfColors)
    {
        numberOfColorsInBottle = numberOfColors;//numero de cores nesse recipiente

        colorsIndex = colors;//array com as corres

        LoadColorsPallet();//carregando as cores de acordo com a paleta selecionada
    }

    public void SetStopperCommand()
    {
        stopperCommand = true;
    }

    public bool GetUnderAnimation()
    {
        return underAnimation;
    }

    public int[] GetColors()
    {
        int[] colors = new int[4];

        for (int x = 0; x < 4; x++)
        {
            colors[x] = colorsIndex[x];
        }

        return colors;
    }

    public bool GetIsFilling()
    {
        return isFilling;
    }

    ///<summary>
    ///Método usado para verificar se o recipiente está em processo de receber liquido de outro recipiente
    ///-Esse método retorna um bool
    ///</summary>
    public bool GetIsBeingFilled()
    {
        return isBeingFilled;
    }

    //========================================== ANIMAÇÕES ========================================== 

    //animação que vai rotacionar o recipiente
    private IEnumerator RotateBottle()
    {
        if (GameController.Instance != null && GameController.Instance.shouldGamePause)
                yield break;
        float t = 0;
        float lerpValue;
        float angleValue;

        float lastAngleValue = 0;

        //para a animação de rotacionar ser proporcional a quantidade de líquidos que serão transferidos
        float rotationSpeed = timeToRotate + ((4 - numberOfColorsInBottle) * 0.1f) + ((numberOfColorsToTransfer - 1) * 0.1f);

        while (t < rotationSpeed)
        {
            lerpValue = t / rotationSpeed;
            angleValue = Mathf.Lerp(0f, directionMultiplier * rotationValues[rotationIndex], lerpValue);

            transform.rotation = Quaternion.Euler(0, 0, angleValue);

            bottleMaskSR.material.SetFloat("_SARM", ScaleAndRotationMultiplierCurve.Evaluate(angleValue));

            if (fillAmounts[numberOfColorsInBottle] > FillAmountCurve.Evaluate(angleValue) + 0.005f)
            {
                if (!lineRenderer.enabled)
                {
                    if (lineRenderer != null)
                    {
                        lineRenderer.startColor = topColor;
                        lineRenderer.endColor = topColor;
                        lineRenderer.enabled = true;
                    }
                    isPouringStreamActive = true;

                    PlayPourEffects();

                    UpdatePouringStream(); // Force immediate position update on first frame
                }

                bottleMaskSR.material.SetFloat("_FillAmount", FillAmountCurve.Evaluate(angleValue));

                bottleControllerRef.FillUp(FillAmountCurve.Evaluate(lastAngleValue) - FillAmountCurve.Evaluate(angleValue));
            }

            t += Time.deltaTime * RotationSpeedMultiplier.Evaluate(angleValue);

            lastAngleValue = angleValue;

            yield return new WaitForEndOfFrame();
        }

        Debug.Log("Tempo: " + t);

        angleValue = directionMultiplier * rotationValues[rotationIndex];

        bottleMaskSR.material.SetFloat("_SARM", ScaleAndRotationMultiplierCurve.Evaluate(angleValue));
        bottleMaskSR.material.SetFloat("_FillAmount", FillAmountCurve.Evaluate(angleValue));

        //corrigindo bug de quantidade de liquido no outro recipiente
        bottleControllerRef.AdjustFillAmount(newNumberOfColorsInOtherBottle);

        //numberOfColorsInBottle -= numberOfTopColorLayers;
        numberOfColorsInBottle -= numberOfColorsToTransfer;

        //bottleControllerRef.numberOfColorsInBottle += numberOfColorsToTransfer;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
        isPouringStreamActive = false;

        StopPourEffects();

        bottleControllerRef.UpdateTopColorValues();

        StartCoroutine(RotateBottleBack());
    }

    //animação que vai rotacionar de volta
    private IEnumerator RotateBottleBack()
    {
        float t = 0;
        float lerpValue;
        float angleValue;

        float lastAngleValue = directionMultiplier * rotationValues[rotationIndex];

        float startFill = bottleMaskSR.material.GetFloat("_FillAmount");
        float endFill = fillAmounts[numberOfColorsInBottle];

        while (t < timeToRotate)
        {
            lerpValue = t / timeToRotate;
            angleValue = Mathf.Lerp(directionMultiplier * rotationValues[rotationIndex], 0f, lerpValue);

            transform.rotation = Quaternion.Euler(0, 0, angleValue);

            bottleMaskSR.material.SetFloat("_SARM", ScaleAndRotationMultiplierCurve.Evaluate(angleValue));
            bottleMaskSR.material.SetFloat("_FillAmount", Mathf.Lerp(startFill, endFill, lerpValue));

            lastAngleValue = angleValue;

            t += Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        //atualizando o estado
        UpdateTopColorValues();

        angleValue = 0f;
        transform.eulerAngles = new Vector3(0, 0, angleValue);
        bottleMaskSR.material.SetFloat("_SARM", ScaleAndRotationMultiplierCurve.Evaluate(angleValue));
        bottleMaskSR.material.SetFloat("_FillAmount", endFill);

        //correção para que a animação corra por completo
        underAnimation = false;

        //verificando se o outro recipiente está cheio
        if (bottleControllerRef.CheckIfItsDone())
        {
            UIController.Vibrate();
            bc.VerifyVictory();

            // ✅ CHECK HERE
            if (UIController.IsBottleTopEnabled && bottleControllerRef.stopper != null)
            {
                bottleControllerRef.stopper.Animate();
            }
        }

        StartCoroutine(MoveBottleBack());
    }

    private void SetupLineRenderer()
    {
        if (lineRenderer != null)
        {
            lineRenderer.startColor = topColor;
            lineRenderer.endColor = topColor;
            lineRenderer.enabled = true;
        }
        isPouringStreamActive = true;

        PlayPourEffects();

        UpdatePouringStream(); // Force immediate update
    }

    private IEnumerator RotateBackOnly()
    {
        float t = 0;
        float startAngle = directionMultiplier * rotationValues[rotationIndex];

        while (t < moveAnimationTime)
        {
            float angle = Mathf.Lerp(startAngle, 0f, t / moveAnimationTime);
            t += Time.deltaTime;
            yield return null;
        }

        transform.eulerAngles = Vector3.zero;
    }

    //animação que vai mover o recipiente para proximo do outro recipiente
    private IEnumerator MoveBottle()
    {
        if (GameController.Instance != null && GameController.Instance.shouldGamePause)
            yield break;

        underAnimation = true;

        startPosition = transform.position;

        Vector3 targetPos = bottleControllerRef.transform.position;

        float startAngle = 0f;
        float endAngle = directionMultiplier * rotationValues[rotationIndex];

        // Position the bottle so its tilted pouring lip lands directly above the target bottle's center
        Vector3 localLip = GetLocalPourStartOffset();
        Vector3 rotatedLipWorld = Vector3.Scale(Quaternion.Euler(0, 0, endAngle) * localLip, transform.lossyScale);
        Vector3 desiredLipWorldPos = targetPos + new Vector3(0f, pourMouthYOffset, 0f);
        endPosition = desiredLipWorldPos - rotatedLipWorld;

        float t = 0;

        while (t <= moveAnimationTime)
        {
            float progress = t / moveAnimationTime;

            // MOVE
            transform.position = Vector3.Lerp(startPosition, endPosition, progress);

            // ROTATE
            float angle = Mathf.Lerp(startAngle, endAngle, progress);
            transform.rotation = Quaternion.Euler(0, 0, angle);

            // Keep liquid horizontal as it tilts
            bottleMaskSR.material.SetFloat("_SARM", ScaleAndRotationMultiplierCurve.Evaluate(angle));
            bottleMaskSR.material.SetFloat("_FillAmount", FillAmountCurve.Evaluate(angle));

            t += Time.deltaTime;
            yield return null;
        }

        transform.position = endPosition;
        transform.rotation = Quaternion.Euler(0, 0, endAngle);
        bottleMaskSR.material.SetFloat("_SARM", ScaleAndRotationMultiplierCurve.Evaluate(endAngle));
        bottleMaskSR.material.SetFloat("_FillAmount", FillAmountCurve.Evaluate(endAngle));

        GameController.Instance.AddMove();

        StartCoroutine(ContinuePour());
    }

    private IEnumerator ContinuePour()
    {
        float t = 0;
        float duration = timeToRotate;

        float sourceStartFill = fillAmounts[numberOfColorsInBottle];
        float sourceEndFill = fillAmounts[numberOfColorsInBottle - numberOfColorsToTransfer];

        float destStartFill = fillAmounts[newNumberOfColorsInOtherBottle - numberOfColorsToTransfer];
        float destEndFill = fillAmounts[newNumberOfColorsInOtherBottle];

        // Enable the pouring stream and particles
        if (lineRenderer != null)
        {
            lineRenderer.startColor = topColor;
            lineRenderer.endColor = topColor;
            lineRenderer.enabled = true;
        }
        isPouringStreamActive = true;

        PlayPourEffects();

        UpdatePouringStream(); // Force immediate position update on first frame

        while (t < duration)
        {
            float progress = t / duration;

            // Smoothly interpolate fill amounts
            float currentSourceFill = Mathf.Lerp(sourceStartFill, sourceEndFill, progress);
            float currentDestFill = Mathf.Lerp(destStartFill, destEndFill, progress);

            bottleMaskSR.material.SetFloat("_FillAmount", currentSourceFill);
            if (bottleControllerRef != null && bottleControllerRef.bottleMaskSR != null)
            {
                bottleControllerRef.bottleMaskSR.material.SetFloat("_FillAmount", currentDestFill);
            }

            t += Time.deltaTime;
            yield return null;
        }

        // Set final exact values
        bottleMaskSR.material.SetFloat("_FillAmount", sourceEndFill);
        if (bottleControllerRef != null && bottleControllerRef.bottleMaskSR != null)
        {
            bottleControllerRef.bottleMaskSR.material.SetFloat("_FillAmount", destEndFill);
        }

        // Disable stream and particles
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
        isPouringStreamActive = false;

        StopPourEffects();

        numberOfColorsInBottle -= numberOfColorsToTransfer;
        bottleControllerRef.AdjustFillAmount(newNumberOfColorsInOtherBottle);

        bottleControllerRef.UpdateTopColorValues();

        StartCoroutine(MoveBackWithRotate());
    }

    private IEnumerator MoveBackWithRotate()
    {
        startPosition = transform.position;
        endPosition = originalPosition;

        float t = 0;

        float startAngle = directionMultiplier * rotationValues[rotationIndex];
        float endAngle = 0f;

        float startFill = bottleMaskSR.material.GetFloat("_FillAmount");
        float endFill = fillAmounts[numberOfColorsInBottle];

        while (t <= moveAnimationTime)
        {
            float progress = t / moveAnimationTime;

            // MOVE BACK
            transform.position = Vector3.Lerp(startPosition, endPosition, progress);

            // ROTATE BACK
            float angle = Mathf.Lerp(startAngle, endAngle, progress);
            transform.rotation = Quaternion.Euler(0, 0, angle);

            bottleMaskSR.material.SetFloat("_SARM", ScaleAndRotationMultiplierCurve.Evaluate(angle));
            bottleMaskSR.material.SetFloat("_FillAmount", Mathf.Lerp(startFill, endFill, progress));

            t += Time.deltaTime;
            yield return null;
        }

        transform.position = endPosition;

        // Reset clean
        transform.eulerAngles = Vector3.zero;
        bottleMaskSR.material.SetFloat("_SARM", ScaleAndRotationMultiplierCurve.Evaluate(0f));
        bottleMaskSR.material.SetFloat("_FillAmount", endFill);

        transform.GetComponent<SpriteRenderer>().sortingOrder -= 2;
        bottleMaskSR.sortingOrder -= 2;

        UpdateTopColorValues();

        isFilling = false;
        bottleControllerRef.SetIsBeingFilled(false);

        underAnimation = false;

        if (bottleControllerRef.CheckIfItsDone())
        {
            UIController.Vibrate();
            bc.VerifyVictory();

            if (UIController.IsBottleTopEnabled && bottleControllerRef.stopper != null)
            {
                bottleControllerRef.stopper.Animate();
            }
        }
    }

    //animação que vai mover o recipiente de volta para o lugar
    private IEnumerator MoveBottleBack()
    {
        startPosition = transform.position;
        endPosition = originalPosition;

        float t = 0;

        while (t <= moveAnimationTime)
        {
            transform.position = Vector3.Lerp(startPosition, endPosition, (t / moveAnimationTime));

            t += Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        transform.position = endPosition;

        transform.GetComponent<SpriteRenderer>().sortingOrder -= 2;
        bottleMaskSR.sortingOrder -= 2;

        //indica que esse recipiente não está mais enchendo outro recipiente
        isFilling = false;

        //indica que o outro recipiente não está mais recebendo liquido
        bottleControllerRef.SetIsBeingFilled(false);
    }

    public void AnimateBottle(Vector3 finalPosition, float duration)
    {
        StartCoroutine(MoveAnimation(finalPosition, duration));
    }

    //animação de movimentação
    private IEnumerator MoveAnimation(Vector3 finalPosition, float duration)
    {
        Vector3 inicialPosition = transform.position;

        float t = 0;

        while (t <= duration)
        {
            transform.position = Vector3.Lerp(inicialPosition, finalPosition, (t / duration));

            t += Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        transform.position = finalPosition;
    }

    private void OnDisable()
    {
        // Re-parent particles if they were left detached
        if (pourParticles != null && pourParticlesOriginalParent != null && pourParticles.transform.parent == null)
        {
            pourParticles.transform.SetParent(pourParticlesOriginalParent);
        }
        if (splashParticles != null && splashParticlesOriginalParent != null && splashParticles.transform.parent == null)
        {
            splashParticles.transform.SetParent(splashParticlesOriginalParent);
        }
    }

    private void OnDestroy()
    {
        // Destroy detached particles to avoid leaks if bottle is destroyed
        if (pourParticles != null && pourParticlesOriginalParent != null && pourParticles.transform.parent == null)
        {
            Destroy(pourParticles.gameObject);
        }
        if (splashParticles != null && splashParticlesOriginalParent != null && splashParticles.transform.parent == null)
        {
            Destroy(splashParticles.gameObject);
        }
    }
}
 
/*
*   -Alterar as variáveis que podem ser privadas para privado
*   -criar métodos set e get necessários
*   -usar o método MoveAnimation ao invés dos métodos atuais para mover o recipiente
*   -Otimizações gerais
*/

/*
*   -Alterar as variáveis que podem ser privadas para privado
*   -criar métodos set e get necessários
*   -usar o método MoveAnimation ao invés dos métodos atuais para mover o recipiente
*   -Otimizações gerais
*/