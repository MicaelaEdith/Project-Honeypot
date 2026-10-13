using UnityEngine;

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
