using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Dropdown))]
public class LanguageSelector : MonoBehaviour
{
    private TMP_Dropdown dropdown;

    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        dropdown.onValueChanged.AddListener(HandleValueChanged);
        dropdown.SetValueWithoutNotify(ToIndex(GameManager.SelectedLanguage));
        dropdown.RefreshShownValue();
    }

    private void OnDestroy()
    {
        if (dropdown != null)
        {
            dropdown.onValueChanged.RemoveListener(HandleValueChanged);
        }
    }

    private void HandleValueChanged(int index)
    {
        GameManager.SelectedLanguage = index == 1 ? GameManager.Language.Spanish : GameManager.Language.English;
    }

    private static int ToIndex(GameManager.Language language)
    {
        return language == GameManager.Language.Spanish ? 1 : 0;
    }
}
