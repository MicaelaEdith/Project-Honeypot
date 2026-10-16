using UnityEngine;

[DisallowMultipleComponent]
public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private Color gizmoColor = new Color(1f, 0.4f, 0f, 1f);

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
