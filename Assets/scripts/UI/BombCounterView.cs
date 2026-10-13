using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BombCounterView : MonoBehaviour
{
    [SerializeField] private Text countText;
    [SerializeField] private Image bombIcon;
    [SerializeField] private Color emptyTint = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color normalTint = Color.white;

    public void SetCount(int count)
    {
        int clamped = Mathf.Max(count, 0);

        if (countText != null)
        {
            countText.text = clamped.ToString();
        }

        if (bombIcon != null)
        {
            bombIcon.color = clamped > 0 ? normalTint : emptyTint;
        }
    }

    private void Awake()
    {
        if (countText == null)
        {
            countText = GetComponentInChildren<Text>();
        }

        if (bombIcon == null)
        {
            bombIcon = GetComponentInChildren<Image>();
        }
    }
}