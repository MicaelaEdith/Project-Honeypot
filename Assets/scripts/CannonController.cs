using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CannonController : MonoBehaviour
{
    [SerializeField] private Transform cannonRoot;
    [SerializeField] private Transform muzzle;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GameObject impactVfxPrefab;

    [SerializeField] private Camera aimCamera;
    [SerializeField] private float aimHeight = 0f;
    [SerializeField] private Vector3 barrelAxisLocal = Vector3.zero;
    [SerializeField] private float rotationSpeed = 200f;

    [SerializeField] private float projectileSpeed = 0f;
    [SerializeField] private float fireCooldown = 0.3f;
    [SerializeField] private float projectileScale = 0f;
    [SerializeField] private float projectileScaleMultiplier = 1f;

    [SerializeField] private float projectileSizeFactor = 0.05f;
    [SerializeField] private float flightTime = 0.6f;
    [SerializeField] private float impactVfxDiameterFactor = 2f;
    [SerializeField] private float impactVfxReferenceDiameter = 5.5f;
    [SerializeField] private float impactVfxScale = 1f;

    private static readonly string[] CannonNames =
    {
        "canion", "cañon", "cañón", "canon", "cannon", "turret", "torreta", "barrel", "gun"
    };

    private Vector3 barrelAxis;
    private Vector3 muzzleLocal;
    private float cooldownTimer;

    public Transform CannonRoot => cannonRoot;

    public GameObject ProjectilePrefab => projectilePrefab;

    public GameObject ImpactVfxPrefab => impactVfxPrefab;

    public Vector3 BarrelDirection
    {
        get
        {
            if (cannonRoot == null)
            {
                return transform.forward;
            }
            Vector3 direction = cannonRoot.TransformDirection(barrelAxis);
            if (direction.sqrMagnitude < 0.0001f)
            {
                return cannonRoot.forward;
            }
            return direction.normalized;
        }
    }

    private void Awake()
    {
        if (cannonRoot == null)
        {
            cannonRoot = IsCannonName(transform.name) ? transform : FindCannon(transform);
        }
        if (cannonRoot == null)
        {
            cannonRoot = transform;
        }

        Renderer barrelRenderer = FindLongestRenderer(cannonRoot);

        barrelAxis = barrelAxisLocal.sqrMagnitude > 0.0001f
            ? barrelAxisLocal.normalized
            : DetectBarrelAxis(barrelRenderer);

        Vector3 initialDirection = cannonRoot.TransformDirection(barrelAxis);
        if (initialDirection.sqrMagnitude < 0.0001f)
        {
            initialDirection = cannonRoot.forward;
        }
        initialDirection.Normalize();

        muzzleLocal = DetectMuzzle(cannonRoot, barrelRenderer, initialDirection);

        Debug.Log($"[CannonController] root={cannonRoot.name} axis={barrelAxis} dir={BarrelDirection} muzzleLocal={muzzleLocal}", this);
    }

    public Vector3 MuzzleWorldPosition()
    {
        return muzzle != null ? muzzle.position : cannonRoot.TransformPoint(muzzleLocal);
    }

    private float AimPlaneHeight()
    {
        if (aimHeight > 0f)
        {
            return aimHeight;
        }
        return MuzzleWorldPosition().y;
    }

    private void LateUpdate()
    {
        cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);

        AimAtMouse();

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && cooldownTimer <= 0f)
        {
            cooldownTimer = fireCooldown;
            Fire();
        }
    }

    private void AimAtMouse()
    {
        Vector3 origin = cannonRoot.position;
        Vector3 current = BarrelDirection;
        current.y = 0f;

        Vector3 toTarget = GetMouseGroundPoint(AimPlaneHeight()) - origin;
        toTarget.y = 0f;

        if (current.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        current.Normalize();
        toTarget.Normalize();

        float yaw = Vector3.SignedAngle(current, toTarget, Vector3.up);
        float step = Mathf.Clamp(yaw, -rotationSpeed * Time.deltaTime, rotationSpeed * Time.deltaTime);
        if (Mathf.Abs(step) < 0.0001f)
        {
            return;
        }

        cannonRoot.Rotate(Vector3.up, step, Space.World);
    }

    private void Fire()
    {
        Vector3 direction = BarrelDirection;
        Vector3 spawn = MuzzleWorldPosition();
        Vector3 aimPoint = GetMouseGroundPoint(AimPlaneHeight());

        float diameter = ProjectileDiameter();

        bool fromPrefab = projectilePrefab != null;
        GameObject projectile = fromPrefab
            ? Instantiate(projectilePrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);

        if (fromPrefab)
        {
            float current = WorldDiameter(projectile);
            float factor = current > 0.0001f ? diameter / current : 1f;
            projectile.transform.localScale *= factor * projectileScaleMultiplier;
        }
        else
        {
            projectile.transform.localScale = Vector3.one * (diameter * projectileScaleMultiplier);
        }

        projectile.transform.SetPositionAndRotation(
            spawn,
            Quaternion.LookRotation(direction, Vector3.up));

        Collider collider = projectile.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false;
        }

        Rigidbody body = projectile.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = projectile.AddComponent<Rigidbody>();
        }
        body.useGravity = false;
        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = direction * ProjectileSpeedTo(spawn, aimPoint);

        if (collider != null)
        {
            IgnorePlayerCollisions(collider);
        }

        Projectile projectileScript = projectile.GetComponent<Projectile>();
        if (projectileScript == null)
        {
            projectileScript = projectile.AddComponent<Projectile>();
        }
        projectileScript.impactVfx = impactVfxPrefab;
        projectileScript.impactVfxScale = ImpactVfxScale(diameter);
    }

    private float HullRadius()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return 1f;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return Mathf.Max(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.0001f);
    }

    private float ProjectileDiameter()
    {
        if (projectileScale > 0f)
        {
            return projectileScale;
        }
        return HullRadius() * Mathf.Max(projectileSizeFactor, 0f);
    }

    private float ProjectileSpeedTo(Vector3 from, Vector3 to)
    {
        if (projectileSpeed > 0f)
        {
            return projectileSpeed;
        }
        float seconds = Mathf.Max(flightTime, 0.05f);
        return Mathf.Max(Vector3.Distance(from, to) / seconds, 0.0001f);
    }


    private float ImpactVfxScale(float shellDiameter)
    {
        float reference = Mathf.Max(impactVfxReferenceDiameter, 0.0001f);
        float ratio = shellDiameter * Mathf.Max(impactVfxDiameterFactor, 0f) / reference;
        return ratio * Mathf.Max(impactVfxScale, 0f);
    }

    private static float WorldDiameter(GameObject target)
    {
        float max = 0f;
        foreach (Collider other in target.GetComponentsInChildren<Collider>())
        {
            Vector3 size = other.bounds.size;
            max = Mathf.Max(max, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        }
        return max;
    }

    private void IgnorePlayerCollisions(Collider projectileCollider)
    {
        Collider[] playerColliders = transform.root.GetComponentsInChildren<Collider>();
        foreach (Collider other in playerColliders)
        {
            if (other != projectileCollider)
            {
                Physics.IgnoreCollision(projectileCollider, other, true);
            }
        }
    }

    private Vector3 GetMouseGroundPoint(float height)
    {
        Camera camera = aimCamera != null ? aimCamera : Camera.main;
        if (camera == null)
        {
            return cannonRoot.position + BarrelDirection * 20f;
        }

        Vector3 screen = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector3.zero;
        Ray ray = camera.ScreenPointToRay(screen);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return ray.origin + ray.direction.normalized * 20f;
    }

    private static Vector3 DetectBarrelAxis(Renderer best)
    {
        if (best == null)
        {
            return Vector3.forward;
        }

        Bounds bounds = best.localBounds;
        Vector3 extents = bounds.extents;

        int axis;
        if (extents.x >= extents.y && extents.x >= extents.z)
        {
            axis = 0;
        }
        else if (extents.y >= extents.z)
        {
            axis = 1;
        }
        else
        {
            axis = 2;
        }

        float max = bounds.max[axis];
        float min = bounds.min[axis];
        int sign = Mathf.Abs(max) >= Mathf.Abs(min) ? 1 : -1;

        Vector3 result = Vector3.zero;
        result[axis] = sign;
        return result;
    }

    private static Vector3 DetectMuzzle(Transform root, Renderer best, Vector3 worldDirection)
    {
        if (best == null)
        {
            return Vector3.zero;
        }

        Bounds bounds = best.localBounds;
        Vector3 axisLocal = best.transform.InverseTransformDirection(worldDirection).normalized;

        int axis = 0;
        if (Mathf.Abs(axisLocal.y) > Mathf.Abs(axisLocal[axis]))
        {
            axis = 1;
        }
        if (Mathf.Abs(axisLocal.z) > Mathf.Abs(axisLocal[axis]))
        {
            axis = 2;
        }

        Vector3 tip = bounds.center;
        tip[axis] = axisLocal[axis] >= 0f ? bounds.max[axis] : bounds.min[axis];

        Vector3 worldTip = best.transform.TransformPoint(tip);
        return root.InverseTransformPoint(worldTip);
    }

    private static Renderer FindLongestRenderer(Transform root)
    {
        Renderer best = null;
        float bestLength = 0f;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            Vector3 extents = renderer.localBounds.extents;
            float length = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            if (length > bestLength)
            {
                bestLength = length;
                best = renderer;
            }
        }

        return best;
    }

    private static Transform FindCannon(Transform root)
    {
        if (IsCannonName(root.name))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindCannon(root.GetChild(i));
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static bool IsCannonName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        string lower = name.ToLowerInvariant();
        foreach (string candidate in CannonNames)
        {
            if (lower.Contains(candidate))
            {
                return true;
            }
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Transform root = cannonRoot != null ? cannonRoot : transform;
        Vector3 origin = root.position;

        Vector3 axis = barrelAxis;
        if (axis.sqrMagnitude < 0.0001f)
        {
            axis = DetectBarrelAxis(FindLongestRenderer(root));
        }
        Vector3 direction = Application.isPlaying ? BarrelDirection : root.TransformDirection(axis);
        direction.Normalize();

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + direction * 100f);

        Vector3 muzzlePoint = Application.isPlaying
            ? MuzzleWorldPosition()
            : root.TransformPoint(muzzleLocal);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(muzzlePoint, 4f);
        Gizmos.DrawLine(origin, muzzlePoint);
    }
}
