using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HealthBarView : MonoBehaviour
{
    [SerializeField] private Image fill;

    [SerializeField] private Text healthText;

    [SerializeField] private float fillSeconds = 0.2f;

    private RectTransform fillRect;

    private float fullWidth;

    private float targetFill = 1f;
    private float shownFill = 1f;

    private void Awake()
    {
        if (fill == null)
        {
            fill = GetComponentInChildren<Image>();
        }

        if (fill != null)
        {
            fillRect = fill.rectTransform;
        }
    }

    private void Start()
    {
        if (fillRect != null)
        {
            fullWidth = fillRect.sizeDelta.x;
        }

        Debug.Log($"[HealthBarView] vivo. fill={(fill != null ? fill.name : "NULL")} " +
                  $"anchoFull={(fillRect != null ? fillRect.sizeDelta.x.ToString() : "-")} " +
                  $"fillSeconds={fillSeconds}", this);
        Apply();
    }

    private void OnDisable()
    {
        shownFill = targetFill;
        Apply();
    }

    public void SetHealth(float current, float max)
    {
        targetFill = max > 0.0001f ? Mathf.Clamp01(current / max) : 0f;

        if (healthText != null)
        {
            healthText.text = Mathf.CeilToInt(Mathf.Max(current, 0f)).ToString();
        }
    }

    private void Update()
    {
        if (fillSeconds <= 0.0001f)
        {
            shownFill = targetFill;
            Apply();
            return;
        }

        if (Mathf.Abs(shownFill - targetFill) < 0.0005f)
        {
            return;
        }

        shownFill = Mathf.MoveTowards(shownFill, targetFill, Time.deltaTime / fillSeconds);
        Apply();
    }

    private void Apply()
    {
        if (fillRect == null)
        {
            Debug.LogError("[HealthBarView] fill sin asignar y no se encontro ningun Image hijo.", this);
            return;
        }

        fillRect.sizeDelta = new Vector2(fullWidth * shownFill, fillRect.sizeDelta.y);
    }
}