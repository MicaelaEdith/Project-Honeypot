using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class EnemyCounterView : MonoBehaviour
{
    [SerializeField] private Text countText;
    [SerializeField] private Image enemyIcon;
    [SerializeField] private Color emptyTint = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color normalTint = Color.white;

    public void SetCount(int count)
    {
        int clamped = Mathf.Max(count, 0);

        if (countText != null)
        {
            countText.text = clamped.ToString();
        }

        if (enemyIcon != null)
        {
            enemyIcon.color = clamped > 0 ? normalTint : emptyTint;
        }
    }

    private void Awake()
    {
        if (countText == null)
        {
            countText = GetComponentInChildren<Text>();
        }

        if (countText == null && transform.parent != null)
        {
            countText = transform.parent.GetComponentInChildren<Text>();
        }

        if (enemyIcon == null)
        {
            enemyIcon = GetComponentInChildren<Image>();
        }
    }
}
