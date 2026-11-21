using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Central audio controller for background music and cute puzzle/UI sound effects.
/// Auto-bootstraps at runtime and loads default clips from Resources/Audio if not assigned.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Volume Settings")]
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.32f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.85f;

    [Header("Audio Clips (Optional - auto-loaded from Resources/Audio)")]
    [SerializeField] private AudioClip bgmCozyLoop;
    [SerializeField] private AudioClip sfxUiClick;
    [SerializeField] private AudioClip sfxBottleSelect;
    [SerializeField] private AudioClip sfxBottleDeselect;
    [SerializeField] private AudioClip sfxBottlePour;
    [SerializeField] private AudioClip sfxBottleComplete;
    [SerializeField] private AudioClip sfxLevelWin;
    [SerializeField] private AudioClip sfxCoin;
    [SerializeField] private AudioClip sfxError;
    [SerializeField] private AudioClip sfxParticleSparkle;
    [SerializeField] private AudioClip sfxTitleIntro;

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private float lastUiClickTime = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null) return;

        AudioManager existing = FindFirstObjectByType<AudioManager>();
        if (existing != null) return;

        GameObject go = new GameObject("AudioManager");
        go.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = bgmVolume;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume;

        LoadDefaultClipsIfMissing();
        ApplySoundSetting();
        PlayBGM();

        SceneManager.sceneLoaded += OnSceneLoaded;
        RegisterAllInteractiveUI();
        InvokeRepeating(nameof(RegisterAllInteractiveUI), 0.5f, 0.5f);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RegisterAllInteractiveUI();
    }

    private void RegisterAllInteractiveUI()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];
            if (btn != null && btn.GetComponent<UIButtonSoundHook>() == null)
                btn.gameObject.AddComponent<UIButtonSoundHook>();
        }

        Toggle[] toggles = FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < toggles.Length; i++)
        {
            Toggle tgl = toggles[i];
            if (tgl != null && tgl.GetComponent<UIButtonSoundHook>() == null)
                tgl.gameObject.AddComponent<UIButtonSoundHook>();
        }
    }

    private void LoadDefaultClipsIfMissing()
    {
        if (bgmCozyLoop == null) bgmCozyLoop = Resources.Load<AudioClip>("Audio/bgm_cozy_loop");
        if (sfxUiClick == null) sfxUiClick = Resources.Load<AudioClip>("Audio/sfx_ui_click");
        if (sfxBottleSelect == null) sfxBottleSelect = Resources.Load<AudioClip>("Audio/sfx_bottle_select");
        if (sfxBottleDeselect == null) sfxBottleDeselect = Resources.Load<AudioClip>("Audio/sfx_bottle_deselect");
        if (sfxBottlePour == null) sfxBottlePour = Resources.Load<AudioClip>("Audio/sfx_bottle_pour");
        if (sfxBottleComplete == null) sfxBottleComplete = Resources.Load<AudioClip>("Audio/sfx_bottle_complete");
        if (sfxLevelWin == null) sfxLevelWin = Resources.Load<AudioClip>("Audio/sfx_level_win");
        if (sfxCoin == null) sfxCoin = Resources.Load<AudioClip>("Audio/sfx_coin");
        if (sfxError == null) sfxError = Resources.Load<AudioClip>("Audio/sfx_error");
        if (sfxParticleSparkle == null) sfxParticleSparkle = Resources.Load<AudioClip>("Audio/sfx_particle_sparkle");
        if (sfxTitleIntro == null) sfxTitleIntro = Resources.Load<AudioClip>("Audio/sfx_title_intro");
    }

    private void PlayBGM()
    {
        if (bgmSource == null || bgmCozyLoop == null) return;
        if (bgmSource.clip != bgmCozyLoop)
            bgmSource.clip = bgmCozyLoop;
        if (!bgmSource.isPlaying && UIController.IsSoundEnabled)
            bgmSource.Play();
    }

    private void ApplySoundSetting()
    {
        bool enabled = UIController.IsSoundEnabled;
        AudioListener.volume = enabled ? 1f : 0f;
        AudioListener.pause = !enabled;

        if (bgmSource != null)
        {
            bgmSource.volume = enabled ? bgmVolume : 0f;
            if (enabled && !bgmSource.isPlaying && bgmCozyLoop != null)
                bgmSource.Play();
        }
    }

    private void PlaySFX(AudioClip clip, float pitchMin = 1f, float pitchMax = 1f, float volumeScale = 1f)
    {
        if (!UIController.IsSoundEnabled || clip == null || sfxSource == null)
            return;

        sfxSource.pitch = (Mathf.Approximately(pitchMin, pitchMax))
            ? pitchMin
            : Random.Range(pitchMin, pitchMax);
        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    public static void RefreshSoundSetting()
    {
        if (Instance != null)
            Instance.ApplySoundSetting();
    }

    public static void PlayUIClick()
    {
        if (Instance == null) return;
        if (Time.unscaledTime - Instance.lastUiClickTime < 0.05f) return;

        Instance.lastUiClickTime = Time.unscaledTime;
        Instance.PlaySFX(Instance.sfxUiClick, 0.96f, 1.04f, 0.85f);
    }

    public static void PlayBottleSelect()
    {
        if (Instance != null)
            Instance.PlaySFX(Instance.sfxBottleSelect, 0.95f, 1.06f, 0.95f);
    }

    public static void PlayBottleDeselect()
    {
        if (Instance != null)
            Instance.PlaySFX(Instance.sfxBottleDeselect, 0.96f, 1.04f, 0.85f);
    }

    public static void PlayBottlePour()
    {
        if (Instance != null)
            Instance.PlaySFX(Instance.sfxBottlePour, 0.94f, 1.06f, 0.9f);
    }

    public static void PlayBottleComplete()
    {
        if (Instance != null)
            Instance.PlaySFX(Instance.sfxBottleComplete, 0.98f, 1.03f, 1f);
    }

    public static void PlayLevelWin()
    {
        if (Instance != null)
            Instance.PlaySFX(Instance.sfxLevelWin, 1f, 1f, 1f);
    }

    public static void PlayCoin()
    {
        if (Instance != null)
            Instance.PlaySFX(Instance.sfxCoin, 0.98f, 1.04f, 0.95f);
    }

    public static void PlayError()
    {
        if (Instance != null)
            Instance.PlaySFX(Instance.sfxError, 0.97f, 1.02f, 0.85f);
    }

    public static void PlayParticleSparkle()
    {
        if (Instance != null)
            Instance.PlaySFX(Instance.sfxParticleSparkle, 0.96f, 1.05f, 0.9f);
    }

    public static void PlayTitleIntro()
    {
        if (Instance != null)
            Instance.PlaySFX(Instance.sfxTitleIntro, 1f, 1f, 0.95f);
    }
}

/// <summary>
/// Automatically attached by AudioManager to every Button and Toggle so all UI interactions play a click sound.
/// </summary>
public class UIButtonSoundHook : MonoBehaviour
{
    private Button button;
    private Toggle toggle;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnInteractiveClick);

        toggle = GetComponent<Toggle>();
        if (toggle != null)
            toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnInteractiveClick);
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void OnInteractiveClick()
    {
        if (button != null && button.interactable)
            AudioManager.PlayUIClick();
    }

    private void OnToggleChanged(bool _)
    {
        if (toggle != null && toggle.interactable)
            AudioManager.PlayUIClick();
    }
}
