using UnityEngine;
using UnityEngine.UI;


[DisallowMultipleComponent]
public class BeeStatusView : MonoBehaviour
{
    [SerializeField] private Image beeIcon;
    [SerializeField] private Sprite[] beeSprites = new Sprite[0];

    [Range(1f, 2f)]
    [SerializeField] private float punchScale = 1.05f;

    [SerializeField] private float punchSeconds = 0.25f;

    private int currentIndex = -1;
    private float lastHealth;
    private float maxHealth = 1f;
    private float punchTimer;
    private RectTransform iconRect;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        if (beeIcon == null)
        {
            beeIcon = GetComponentInChildren<Image>();
        }

        if (beeIcon != null)
        {
            iconRect = beeIcon.rectTransform;
            baseScale = iconRect.localScale;
        }
    }

    private void OnDisable()
    {
        punchTimer = 0f;
        if (iconRect != null)
        {
            iconRect.localScale = baseScale;
        }
    }

    public void SetSprites(Sprite[] sprites)
    {
        beeSprites = sprites ?? new Sprite[0];
        currentIndex = -1;
    }

    public void ResetTo(float current, float max)
    {
        maxHealth = Mathf.Max(max, 0.0001f);
        lastHealth = Mathf.Clamp(current, 0f, maxHealth);
        ApplyState(lastHealth);
    }

    public void SetHealth(float current, float max)
    {
        maxHealth = Mathf.Max(max, 0.0001f);
        float clamped = Mathf.Clamp(current, 0f, maxHealth);

        if (clamped < lastHealth)
        {
            punchTimer = Mathf.Max(punchSeconds, 0.0001f);
        }

        lastHealth = clamped;
        ApplyState(clamped);
    }

    private void ApplyState(float current)
    {
        if (beeIcon == null || beeSprites == null || beeSprites.Length == 0)
        {
            return;
        }

        int index = StateIndex(current);
        if (index == currentIndex)
        {
            return;
        }

        currentIndex = index;
        Sprite sprite = beeSprites[index];
        if (sprite != null)
        {
            beeIcon.sprite = sprite;
        }
    }

    private int StateIndex(float current)
    {
        int count = beeSprites.Length;
        if (count <= 1)
        {
            return 0;
        }

        float step = maxHealth / count;
        if (step <= 0.0001f)
        {
            return 0;
        }

        return Mathf.Clamp(Mathf.FloorToInt((maxHealth - current) / step), 0, count - 1);
    }

    private void Update()
    {
        if (punchTimer <= 0f || iconRect == null)
        {
            return;
        }

        punchTimer -= Time.deltaTime;
        if (punchTimer <= 0f)
        {
            iconRect.localScale = baseScale;
            return;
        }

        float t = Mathf.Clamp01(punchTimer / Mathf.Max(punchSeconds, 0.0001f));
        float bump = Mathf.Sin(t * Mathf.PI);
        iconRect.localScale = baseScale * Mathf.Lerp(1f, punchScale, bump);
    }
}