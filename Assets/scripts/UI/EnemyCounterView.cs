using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enemigos que faltan matar en la partida: una miniatura (el icono) al lado de un
/// numero, igual que el contador de honeypots (BombCounterView).
///
/// Muestra el TOTAL de enemigos que spawnean todas las oleadas y baja de a uno por
/// cada baja, no la cantidad viva en el momento. El numero lo calcula el GameManager
/// (suma las oleadas de cada indice de checkpoint) y lo empuja por evento; esta vista
/// no consulta nada, solo pinta lo que recibe, por eso es un SetCount(int).
/// </summary>
[DisallowMultipleComponent]
public class EnemyCounterView : MonoBehaviour
{
    [Tooltip("Numero de enemigos restantes. Si se deja vacio se busca el primer Text hijo.")]
    [SerializeField] private Text countText;

    [Tooltip("Icono del enemigo. Se atenua cuando no queda ninguno.")]
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

        if (enemyIcon == null)
        {
            enemyIcon = GetComponentInChildren<Image>();
        }
    }
}
