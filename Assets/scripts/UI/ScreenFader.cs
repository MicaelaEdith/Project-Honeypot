using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Tooltip("Imagen negra a fundir. Si se deja vacío se usa el Graphic de este mismo GameObject.")]
    [SerializeField] private Graphic target;
    [SerializeField] private float defaultDuration = 0.6f;

    public bool IsFading { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[ScreenFader] Ya existe una instancia ({Instance.name}); se desactiva {name}.", this);
            enabled = false;
            return;
        }
        Instance = this;

        if (target == null)
        {
            target = GetComponent<Graphic>();
        }
        if (target != null)
        {
            target.raycastTarget = false;
        }
        SetAlpha(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public Coroutine FadeOut()
    {
        return FadeOut(defaultDuration);
    }

    public Coroutine FadeIn()
    {
        return FadeIn(defaultDuration);
    }

    public Coroutine FadeOut(float duration)
    {
        return FadeTo(1f, duration);
    }

    public Coroutine FadeIn(float duration)
    {
        return FadeTo(0f, duration);
    }

    private Coroutine FadeTo(float alpha, float duration)
    {
        return StartCoroutine(FadeRoutine(alpha, duration));
    }

    private IEnumerator FadeRoutine(float alpha, float duration)
    {
        if (target == null)
        {
            yield break;
        }

        IsFading = true;
        duration = Mathf.Max(duration, 0.0001f);
        float start = target.color.a;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(start, alpha, Mathf.Clamp01(t / duration)));
            yield return null;
        }

        SetAlpha(alpha);
        IsFading = false;
    }

    private void SetAlpha(float alpha)
    {
        if (target == null)
        {
            return;
        }

        Color color = target.color;
        color.a = alpha;
        target.color = color;
    }
}
