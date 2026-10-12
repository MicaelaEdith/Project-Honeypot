using UnityEngine;

/// <summary>
/// Camara de persecucion "detras del morro": se ubica siempre por detras
/// del rumbo del tanque (MovingForward) y lo mira, manteniendo el horizonte
/// nivelado. Es orgánica al girar y desliza suavemente en teleports
/// (respawn), sin heredar la rotacion del player. Si un obstaculo de
/// Decor tapa la linea player-camara, se levanta (y se inclina en X)
/// proporcionalmente, y como ultimo recurso se acerca al obstaculo.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Referencia")]
    [Tooltip("Objetivo a seguir. Si queda vacio se busca el TankController.")]
    [SerializeField] private Transform target;

    [Header("Camara")]
    [Tooltip("Distancia horizontal detras del morro del tanque.")]
    [SerializeField] private float distance = 30f;
    [Tooltip("Altura de la camara sobre el tanque.")]
    [SerializeField] private float height = 17.5f;
    [Tooltip("Altura del punto del tanque que la camara enfoca.")]
    [SerializeField] private float lookHeight = 8f;
    [Tooltip("Suavizado del movimiento y la rotacion. 0 = pegada instantanea.")]
    [SerializeField] private float smoothing = 12f;
    [Tooltip("Velocidad maxima del movimiento de la camara (u/s). Limita el desliz en teleports/respawn.")]
    [SerializeField] private float maxSpeed = 60f;

    [Header("Obstaculos")]
    [Tooltip("Sensor de obstaculos del player. Vacio = se busca en el target.")]
    [SerializeField] private PlayerObstacleSensor obstacleSensor;
    [Tooltip("Levantamiento de la camara proporcional a la penetracion del rayo player-camara (u por u).")]
    [SerializeField] private float liftFactor = 1f;
    [Tooltip("Levantamiento maximo de la camara (u).")]
    [SerializeField] private float maxLift = 20f;
    [Tooltip("Si con el levantamiento maximo la vista sigue tapada, acercar la camara al obstaculo.")]
    [SerializeField] private bool pullInWhenBlocked = true;
    [Tooltip("Distancia minima entre la camara y el obstaculo al acercarla.")]
    [SerializeField] private float collisionPadding = 1f;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null)
        {
            TankController controller = FindFirstObjectByType<TankController>();
            if (controller != null)
            {
                target = controller.transform;
            }
            else
            {
                return;
            }
        }

        if (obstacleSensor == null)
        {
            obstacleSensor = target.GetComponent<PlayerObstacleSensor>();
        }

        Vector3 forward = GetTankForward();
        Vector3 desired = target.position - forward * distance + Vector3.up * height;
        Vector3 lookPoint = target.position + Vector3.up * lookHeight;

        desired = AdjustForObstacles(lookPoint, desired);

        if (smoothing <= 0f)
        {
            transform.SetPositionAndRotation(desired, Quaternion.LookRotation(lookPoint - desired, Vector3.up));
            return;
        }

        Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref velocity, 1f / smoothing, maxSpeed);
        transform.position = pos;
        Vector3 direction = lookPoint - pos;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction.normalized, Vector3.up),
            smoothing * Time.deltaTime);
    }

    /// <summary>
    /// Reacomodo de la camara ante obstaculos: si el rayo
    /// player-camara choca, levanta la camara proporcionalmente
    /// (al seguir mirando al mismo punto, la rotacion en X se
    /// inclina sola) y, si con el levantamiento maximo la vista
    /// sigue tapada, la acerca al obstaculo.
    /// </summary>
    private Vector3 AdjustForObstacles(Vector3 origin, Vector3 desired)
    {
        if (obstacleSensor == null)
        {
            return desired;
        }

        float lift = 0f;
        if (obstacleSensor.RaycastTowards(origin, desired, out float hitDistance))
        {
            float penetration = Vector3.Distance(origin, desired) - hitDistance;
            if (penetration > 0f)
            {
                lift = Mathf.Clamp(penetration * Mathf.Max(liftFactor, 0f), 0f, Mathf.Max(maxLift, 0f));
            }
        }

        desired.y += lift;

        if (pullInWhenBlocked && maxLift > 0f && lift >= maxLift
            && obstacleSensor.RaycastTowards(origin, desired, out float stillBlocked))
        {
            Vector3 direction = (desired - origin).normalized;
            float stop = Mathf.Max(stillBlocked - Mathf.Max(collisionPadding, 0f), 0.1f);
            desired = origin + direction * stop;
        }

        return desired;
    }

    private Vector3 GetTankForward()
    {
        TankController controller = target.GetComponent<TankController>();
        if (controller != null && controller.MovingForward.sqrMagnitude > 0.0001f)
        {
            return controller.MovingForward;
        }

        Vector3 fallback = target.forward;
        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.0001f)
        {
            return fallback.normalized;
        }
        return Vector3.forward;
    }
}