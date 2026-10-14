using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    private const string MusicVolumeKey = "audio.musicVolume";
    private const string SfxVolumeKey = "audio.sfxVolume";
    private const string MusicSliderName = "Slider_music";
    private const string FxSliderName = "Slider_fx";

    [SerializeField] private float musicMaxVolume = 1f;
    [SerializeField] private float sfxMaxVolume = 0.6f;
    [SerializeField] private float defaultMusicVolume = 1f;
    [SerializeField] private float defaultSfxVolume = 1f;
    [SerializeField] private float pauseMusicDuck = 0.5f;

    private AudioSource musicSource;
    private AudioSource sfxSource;

    private AudioClip menuMusic;
    private AudioClip gameMusic;

    private AudioClip clickClip;
    private AudioClip playerShotClip;
    private AudioClip enemyShotClip;
    private AudioClip damageClip;
    private AudioClip enemyExplosionClip;
    private AudioClip playerDeathClip;
    private AudioClip victoryClip;
    private AudioClip honeypotClip;

    private float musicVolume = 1f;
    private float sfxVolume = 1f;
    private bool musicDucked;

    private readonly List<Slider> musicSliders = new List<Slider>();
    private readonly List<Slider> fxSliders = new List<Slider>();

    public static AudioManager Instance { get; private set; }

    public event System.Action OnVolumesChanged;

    public float MusicVolume
    {
        get => musicVolume;
        set => SetMusicVolume(value);
    }

    public float SfxVolume
    {
        get => sfxVolume;
        set => SetSfxVolume(value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject host = new GameObject("AudioManager");
        host.AddComponent<AudioManager>();
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

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        LoadClips();
        LoadPrefs();
        ApplyVolumes();

        SceneManager.sceneLoaded += HandleSceneLoaded;
        musicSliders.Clear();
        fxSliders.Clear();
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }

    private void LoadClips()
    {
        menuMusic = Resources.Load<AudioClip>("Audio/menu");
        gameMusic = Resources.Load<AudioClip>("Audio/main");

        clickClip = Resources.Load<AudioClip>("Audio/fxs/click");
        playerShotClip = Resources.Load<AudioClip>("Audio/fxs/disparo_nave");
        enemyShotClip = Resources.Load<AudioClip>("Audio/fxs/disparo_enemigo");
        damageClip = Resources.Load<AudioClip>("Audio/fxs/danio");
        enemyExplosionClip = Resources.Load<AudioClip>("Audio/fxs/nave_explotando");
        playerDeathClip = Resources.Load<AudioClip>("Audio/fxs/muerte");
        victoryClip = Resources.Load<AudioClip>("Audio/fxs/final");
        honeypotClip = Resources.Load<AudioClip>("Audio/fxs/honeypot");
    }

    private void LoadPrefs()
    {
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, defaultMusicVolume));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, defaultSfxVolume));
    }

    private void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
        ApplyVolumes();
        SyncSliders();
        OnVolumesChanged?.Invoke();
    }

    private void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        ApplyVolumes();
        SyncSliders();
        OnVolumesChanged?.Invoke();
    }

    private void ApplyVolumes()
    {
        if (musicSource != null)
        {
            musicSource.volume = musicVolume * musicMaxVolume * (musicDucked ? pauseMusicDuck : 1f);
        }

        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume * sfxMaxVolume;
        }
    }

    public void SetMusicDucked(bool ducked)
    {
        if (musicDucked == ducked)
        {
            return;
        }

        musicDucked = ducked;
        ApplyVolumes();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == GameManager.MainSceneName)
        {
            PlayMusic(gameMusic);
        }
        else
        {
            PlayMusic(menuMusic);
        }

        RegisterButtonClicks();
        RegisterVolumeSliders();
    }

    private void PlayMusic(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    private void RegisterButtonClicks()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveListener(PlayClick);
            button.onClick.AddListener(PlayClick);
        }
    }

    private void RegisterVolumeSliders()
    {
        musicSliders.Clear();
        fxSliders.Clear();

        Slider[] sliders = FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Slider slider in sliders)
        {
            if (slider == null)
            {
                continue;
            }

            string lower = slider.name.ToLowerInvariant();
            if (lower == MusicSliderName.ToLowerInvariant())
            {
                slider.onValueChanged.RemoveListener(OnMusicSliderChanged);
                slider.onValueChanged.AddListener(OnMusicSliderChanged);
                musicSliders.Add(slider);
            }
            else if (lower == FxSliderName.ToLowerInvariant())
            {
                slider.onValueChanged.RemoveListener(OnFxSliderChanged);
                slider.onValueChanged.AddListener(OnFxSliderChanged);
                fxSliders.Add(slider);
            }
        }

        SyncSliders();
    }

    private void SyncSliders()
    {
        foreach (Slider slider in musicSliders)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(musicVolume);
            }
        }

        foreach (Slider slider in fxSliders)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(sfxVolume);
            }
        }
    }

    private void OnMusicSliderChanged(float value)
    {
        MusicVolume = value;
    }

    private void OnFxSliderChanged(float value)
    {
        SfxVolume = value;
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    public void PlayClick() => Play(clickClip);
    public void PlayPlayerShot() => Play(playerShotClip);
    public void PlayEnemyShot() => Play(enemyShotClip);
    public void PlayDamage() => Play(damageClip);
    public void PlayEnemyExplosion() => Play(enemyExplosionClip);
    public void PlayPlayerDeath() => Play(playerDeathClip);
    public void PlayVictory() => Play(victoryClip);
    public void PlayHoneypot() => Play(honeypotClip);
}
