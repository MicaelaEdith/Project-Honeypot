using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class TransitionsController : MonoBehaviour
{
    [SerializeField] private Transform imagesRoot;
    [SerializeField] private Transform englishRoot;
    [SerializeField] private Transform spanishRoot;

    [SerializeField] private float autoAdvanceDelay = 1f;
    [SerializeField] private float slideHoldDuration = 2.5f;
    [SerializeField] private bool autoAdvance = true;
    [SerializeField] private bool advanceOnClick = true;
    [SerializeField] private bool advanceOnAnyKey = true;
    [SerializeField] private bool revealFirstOnStart = true;

    [SerializeField] private SlideHoldOverride[] slideHoldOverrides = new SlideHoldOverride[]
    {
        new SlideHoldOverride { slidePrefix = "04", holdDuration = 5f },
        new SlideHoldOverride { slidePrefix = "05", holdDuration = 5f },
        new SlideHoldOverride { slidePrefix = "06", holdDuration = 1.5f },
    };

    [SerializeField] private float fadeDuration = 0.35f;

    [SerializeField] private bool loadNextSceneOnEnd = true;
    [SerializeField] private string nextSceneName = "Main";
    [SerializeField] private float endDelay = 1.5f;

    [System.Serializable]
    private class SlideHoldOverride
    {
        public string slidePrefix;
        public float holdDuration = 2.5f;
    }

    private class Element
    {
        public string Name;
        public CanvasGroup Group;
        public bool IsImage;
    }

    private class Slide
    {
        public string Prefix;
        public readonly List<CanvasGroup> Elements = new List<CanvasGroup>();
    }

    private class Step
    {
        public CanvasGroup Group;
        public int Slide;
    }

    private readonly List<Slide> _slides = new List<Slide>();
    private readonly List<Step> _steps = new List<Step>();
    private int _currentIndex = -1;
    private int _currentSlide = -1;
    private float _nextDelay;
    private float _lastRevealTime;
    private bool _finished;

    private void Awake()
    {
        ResolveReferences();
        ApplyLanguage();
        BuildSteps();
        HideAll();
    }

    private void Start()
    {
        if (_steps.Count == 0)
        {
            Debug.LogError("[Transitions] No hay pasos para mostrar. " +
                           "Revisá imagesRoot / englishRoot / spanishRoot.");
            return;
        }

        if (revealFirstOnStart)
        {
            RevealNext();
        }
        else
        {
            _nextDelay = autoAdvanceDelay;
            _lastRevealTime = Time.unscaledTime;
        }
    }

    private void Update()
    {
        if (_finished || _steps.Count == 0)
        {
            return;
        }

        bool input = (advanceOnClick && IsClickPressed()) ||
                     (advanceOnAnyKey && IsAnyKeyDown());

        if (input)
        {
            RevealNext();
            return;
        }

        if (autoAdvance && Time.unscaledTime - _lastRevealTime >= _nextDelay)
        {
            RevealNext();
        }
    }

    private static bool IsClickPressed()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    private static bool IsAnyKeyDown()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        return mouse != null &&
               (mouse.leftButton.wasPressedThisFrame ||
                mouse.rightButton.wasPressedThisFrame ||
                mouse.middleButton.wasPressedThisFrame);
    }

    public void RevealNext()
    {
        _currentIndex++;
        _lastRevealTime = Time.unscaledTime;

        if (_currentIndex >= _steps.Count)
        {
            StartCoroutine(FinishRoutine());
            return;
        }

        Step step = _steps[_currentIndex];

        if (step.Slide != _currentSlide)
        {
            HideSlide(_currentSlide);
            _currentSlide = step.Slide;
        }

        bool lastOfSlide = _currentIndex + 1 >= _steps.Count ||
                           _steps[_currentIndex + 1].Slide != step.Slide;
        _nextDelay = lastOfSlide ? HoldDurationForSlide(step.Slide) : autoAdvanceDelay;

        StartCoroutine(FadeIn(step.Group));
    }

    private float HoldDurationForSlide(int slideIndex)
    {
        if (slideIndex < 0 || slideIndex >= _slides.Count)
        {
            return slideHoldDuration;
        }

        string prefix = _slides[slideIndex].Prefix;

        if (slideHoldOverrides != null)
        {
            foreach (SlideHoldOverride over in slideHoldOverrides)
            {
                if (over != null &&
                    string.Equals(over.slidePrefix, prefix,
                                  System.StringComparison.OrdinalIgnoreCase))
                {
                    return over.holdDuration;
                }
            }
        }

        return slideHoldDuration;
    }

    private void HideSlide(int slide)
    {
        if (slide < 0 || slide >= _slides.Count)
        {
            return;
        }

        foreach (CanvasGroup group in _slides[slide].Elements)
        {
            StartCoroutine(FadeOut(group));
        }
    }

    private IEnumerator FadeIn(CanvasGroup group)
    {
        group.alpha = 0f;

        if (fadeDuration <= 0f)
        {
            group.alpha = 1f;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        group.alpha = 1f;
    }

    private IEnumerator FadeOut(CanvasGroup group)
    {
        if (fadeDuration <= 0f)
        {
            group.alpha = 0f;
            yield break;
        }

        float start = group.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, 0f, elapsed / fadeDuration);
            yield return null;
        }

        group.alpha = 0f;
    }

    private IEnumerator FinishRoutine()
    {
        _finished = true;

        if (endDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(endDelay);
        }

        if (loadNextSceneOnEnd && !string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private void BuildSteps()
    {
        _slides.Clear();
        _steps.Clear();

        if (imagesRoot == null)
        {
            return;
        }

        Transform textRoot = GameManager.SelectedLanguage == GameManager.Language.Spanish
            ? spanishRoot
            : englishRoot;

        List<Element> elements = new List<Element>();

        foreach (Transform image in OrderedChildren(imagesRoot, textRoot))
        {
            elements.Add(new Element { Name = image.name, Group = GetGroup(image), IsImage = true });
        }

        if (textRoot != null)
        {
            foreach (Transform text in OrderedChildren(textRoot, null))
            {
                elements.Add(new Element { Name = text.name, Group = GetGroup(text), IsImage = false });
            }
        }

        elements.Sort((a, b) =>
        {
            int byName = string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
            if (byName != 0)
            {
                return byName;
            }

            return a.IsImage ? -1 : 1;
        });

        Slide slide = null;
        string prefix = null;

        foreach (Element element in elements)
        {
            string elementPrefix = Prefix(element.Name);
            if (slide == null || elementPrefix != prefix)
            {
                slide = new Slide { Prefix = elementPrefix };
                _slides.Add(slide);
                prefix = elementPrefix;
            }

            slide.Elements.Add(element.Group);
            _steps.Add(new Step { Group = element.Group, Slide = _slides.Count - 1 });
        }
    }

    private static string Prefix(string name)
    {
        int i = 0;
        while (i < name.Length && char.IsDigit(name[i]))
        {
            i++;
        }

        return name.Substring(0, i);
    }

    private static List<Transform> OrderedChildren(Transform root, Transform exclude)
    {
        List<Transform> result = new List<Transform>();

        foreach (Transform child in root)
        {
            if (exclude != null && child == exclude)
            {
                continue;
            }

            result.Add(child);
        }

        return result;
    }

    private CanvasGroup GetGroup(Transform target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.gameObject.AddComponent<CanvasGroup>();
        }

        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    private void HideAll()
    {
        foreach (Step step in _steps)
        {
            step.Group.alpha = 0f;
        }
    }

    private void ApplyLanguage()
    {
        bool spanish = GameManager.SelectedLanguage == GameManager.Language.Spanish;

        if (englishRoot != null)
        {
            englishRoot.gameObject.SetActive(!spanish);
        }
        if (spanishRoot != null)
        {
            spanishRoot.gameObject.SetActive(spanish);
        }
    }

    private void ResolveReferences()
    {
        if (englishRoot == null)
        {
            englishRoot = Child(transform, "EN");
        }
        if (spanishRoot == null)
        {
            spanishRoot = Child(transform, "ES");
        }
        if (imagesRoot == null)
        {
            imagesRoot = Child(transform, "Images") ?? Child(transform, "Imgs") ?? Child(transform, "imgs");
        }
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        return found;
    }
}
