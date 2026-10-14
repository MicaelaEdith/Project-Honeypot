using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PauseMenu : MonoBehaviour
{
    private const string PausePanelName = "pause_panel";
    private const string PauseButtonName = "btn_pause";

    private static readonly string[] ResumeButtonNames =
    {
        "btn_resume", "btn_continuar", "btn_reanudar", "btn_continue", "btn_volver", "btn_cerrar", "btn_quit"
    };

    private static readonly string[] MenuButtonNames =
    {
        "btn_menu", "btn_mainmenu", "btn_main_menu", "btn_salir"
    };

    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button menuButton;

    private bool isPaused;

    private void Start()
    {
        ResolveReferences();

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveListener(Open);
            pauseButton.onClick.AddListener(Open);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(Close);
            resumeButton.onClick.AddListener(Close);
        }

        if (menuButton != null)
        {
            menuButton.onClick.RemoveListener(GoToMenu);
            menuButton.onClick.AddListener(GoToMenu);
        }

        isPaused = false;
    }

    private void OnDisable()
    {
        if (isPaused)
        {
            Time.timeScale = 1f;
            isPaused = false;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicDucked(false);
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        if (isPaused)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (isPaused)
        {
            return;
        }

        isPaused = true;

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }

        Time.timeScale = 0f;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicDucked(true);
        }
    }

    public void Close()
    {
        if (!isPaused)
        {
            return;
        }

        isPaused = false;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        Time.timeScale = 1f;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicDucked(false);
        }
    }

    public void GoToMenu()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicDucked(false);
        }

        GameManager.LoadMainMenu();
    }

    private void ResolveReferences()
    {
        if (pauseButton == null)
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Button button in buttons)
            {
                if (button != null && button.name == PauseButtonName)
                {
                    pauseButton = button;
                    break;
                }
            }
        }

        if (pausePanel == null)
        {
            GameObject found = GameObject.Find(PausePanelName);
            if (found != null)
            {
                pausePanel = found;
            }
        }

        if (pausePanel == null)
        {
            return;
        }

        if (resumeButton == null)
        {
            resumeButton = FindButton(pausePanel.transform, ResumeButtonNames);
        }

        if (menuButton == null)
        {
            menuButton = FindButton(pausePanel.transform, MenuButtonNames);
        }
    }

    private static Button FindButton(Transform root, string[] names)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            string lower = button.name.ToLowerInvariant();
            foreach (string candidate in names)
            {
                if (lower == candidate)
                {
                    return button;
                }
            }
        }

        return null;
    }
}
