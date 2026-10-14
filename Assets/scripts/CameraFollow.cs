using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 30f;
    [SerializeField] private float height = 17.5f;
    [SerializeField] private float lookHeight = 8f;
    [SerializeField] private float smoothing = 12f;
    [SerializeField] private float maxSpeed = 60f;

    [SerializeField] private PlayerObstacleSensor obstacleSensor;
    [SerializeField] private float liftFactor = 1f;
    [SerializeField] private float maxLift = 20f;
    [SerializeField] private bool pullInWhenBlocked = true;
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


    public void SnapToTarget()
    {
        velocity = Vector3.zero;

        if (target == null)
        {
            TankController controller = FindFirstObjectByType<TankController>();
            if (controller == null)
            {
                return;
            }
            target = controller.transform;
        }

        Vector3 forward = GetTankForward();
        Vector3 desired = target.position - forward * distance + Vector3.up * height;
        Vector3 lookPoint = target.position + Vector3.up * lookHeight;

        transform.SetPositionAndRotation(
            desired,
            Quaternion.LookRotation(lookPoint - desired, Vector3.up));
    }

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