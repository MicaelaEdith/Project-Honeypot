using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AudioSettingsUI : MonoBehaviour
{
    private const string MusicSliderName = "Slider_music";
    private const string FxSliderName = "Slider_fx";

    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider fxSlider;

    private bool hooked;

    private void Awake()
    {
        ResolveSliders();
        Hook();
        SyncFromManager();
    }

    private void OnEnable()
    {
        ResolveSliders();
        Hook();
        SyncFromManager();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnVolumesChanged -= SyncFromManager;
            AudioManager.Instance.OnVolumesChanged += SyncFromManager;
        }
    }

    private void OnDisable()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnVolumesChanged -= SyncFromManager;
        }
    }

    private void ResolveSliders()
    {
        Slider[] sliders = GetComponentsInChildren<Slider>(true);

        if (musicSlider == null)
        {
            musicSlider = FindSlider(sliders, MusicSliderName);
        }

        if (fxSlider == null)
        {
            fxSlider = FindSlider(sliders, FxSliderName);
        }
    }

    private static Slider FindSlider(Slider[] sliders, string name)
    {
        foreach (Slider slider in sliders)
        {
            if (slider != null && slider.name == name)
            {
                return slider;
            }
        }

        return null;
    }

    private void Hook()
    {
        if (hooked)
        {
            return;
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.AddListener(HandleMusicChanged);
        }

        if (fxSlider != null)
        {
            fxSlider.onValueChanged.AddListener(HandleFxChanged);
        }

        hooked = musicSlider != null || fxSlider != null;
    }

    private void HandleMusicChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.MusicVolume = value;
        }
    }

    private void HandleFxChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SfxVolume = value;
        }
    }

    private void SyncFromManager()
    {
        AudioManager manager = AudioManager.Instance;
        if (manager == null)
        {
            return;
        }

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(manager.MusicVolume);
        }

        if (fxSlider != null)
        {
            fxSlider.SetValueWithoutNotify(manager.SfxVolume);
        }
    }
}
