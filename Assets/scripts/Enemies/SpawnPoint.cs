using UnityEngine;

/// <summary>
/// Marcador de donde nace un enemigo. Es un empty en la escena, sin logica: solo
/// expone la posicion y la rotacion de salida, y dibuja un gizmo para ubicarlo de
/// un vistazo. Los WaveCheckpoint referencian el grupo de SpawnPoint que usan, y
/// el GameManager reparte los enemigos de la oleada en round-robin sobre ese grupo.
///
/// Va separado de WaveCheckpoint a proposito: el punto de gatillo (donde entra el
/// player) y el punto de spawn (donde aparece la nave) casi nunca son el mismo
/// lugar. Con checkpoints por rama, cada rama referencia sus propios SpawnPoint.
/// </summary>
[DisallowMultipleComponent]
public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Color del gizmo en el editor.")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.4f, 0f, 1f);

    [Tooltip("Radio del gizmo, en unidades de mundo (solo visual).")]
    [SerializeField] private float gizmoRadius = 2f;

    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * gizmoRadius * 2f);
    }
}
