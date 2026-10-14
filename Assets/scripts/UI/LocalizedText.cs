using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum UiTextKey
{
    Play,
    Settings,
    Music,
    SoundEffects,
    Language,
    Quit
}

[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private UiTextKey key;

    private Text legacyText;
    private TMP_Text tmpText;

    private void Awake()
    {
        legacyText = GetComponent<Text>();
        tmpText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        GameManager.OnLanguageChanged += HandleLanguageChanged;
        Apply(GameManager.SelectedLanguage);
    }

    private void OnDisable()
    {
        GameManager.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(GameManager.Language language)
    {
        Apply(language);
    }

    private void Apply(GameManager.Language language)
    {
        string value = Resolve(key, language);

        if (tmpText != null)
        {
            tmpText.text = value;
        }
        else if (legacyText != null)
        {
            legacyText.text = value;
        }
    }

    private static string Resolve(UiTextKey key, GameManager.Language language)
    {
        bool english = language == GameManager.Language.English;

        switch (key)
        {
            case UiTextKey.Play:
                return english ? "PLAY" : "JUGAR";
            case UiTextKey.Settings:
                return english ? "SETTINGS" : "CONFIGURACIÓN";
            case UiTextKey.Music:
                return english ? "Music" : "Música";
            case UiTextKey.SoundEffects:
                return english ? "Sound Effects" : "Efectos de sonido";
            case UiTextKey.Language:
                return english ? "Language" : "Idioma";
            case UiTextKey.Quit:
                return english ? "QUIT" : "SALIR";
            default:
                return string.Empty;
        }
    }
}
