using UnityEngine;

[DisallowMultipleComponent]
public class EndingController : MonoBehaviour
{
    [SerializeField] private Transform panelRoot;

    [SerializeField] private GameObject defeatGroup;
    [SerializeField] private GameObject victoryGroup;

    [SerializeField] private GameObject defeatEnglish;
    [SerializeField] private GameObject defeatSpanish;
    [SerializeField] private GameObject victoryEnglish;
    [SerializeField] private GameObject victorySpanish;

    private void Awake()
    {
        ResolveReferences();
        Apply();
    }

    public void QuitGame()
    {
        GameManager.QuitGame();
    }

    public void RestartGame()
    {
        GameManager.RestartGame();
    }

    public void LoadMainMenu()
    {
        GameManager.LoadMainMenu();
    }

    private void Apply()
    {
        bool victory = GameManager.LastOutcome == GameManager.Outcome.Victory;
        bool english = GameManager.SelectedLanguage == GameManager.Language.English;

        SetActive(defeatGroup, !victory);
        SetActive(victoryGroup, victory);

        SetActive(defeatEnglish, !victory && english);
        SetActive(defeatSpanish, !victory && !english);
        SetActive(victoryEnglish, victory && english);
        SetActive(victorySpanish, victory && !english);
    }

    private void ResolveReferences()
    {
        if (panelRoot == null && name == "Ending_Panel")
        {
            panelRoot = transform;
        }

        if (panelRoot == null)
        {
            GameObject found = GameObject.Find("Ending_Panel");
            if (found != null)
            {
                panelRoot = found.transform;
            }
        }

        if (panelRoot == null)
        {
            return;
        }

        ResolveGroup(panelRoot.Find("Defeat"), ref defeatGroup, ref defeatEnglish, ref defeatSpanish);
        ResolveGroup(panelRoot.Find("Victory"), ref victoryGroup, ref victoryEnglish, ref victorySpanish);
    }

    private static void ResolveGroup(Transform group, ref GameObject groupRef, ref GameObject english, ref GameObject spanish)
    {
        if (group == null)
        {
            return;
        }

        if (groupRef == null)
        {
            groupRef = group.gameObject;
        }
        if (english == null)
        {
            english = Child(group, "EN");
        }
        if (spanish == null)
        {
            spanish = Child(group, "ES");
        }
    }

    private static GameObject Child(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        return found != null ? found.gameObject : null;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
