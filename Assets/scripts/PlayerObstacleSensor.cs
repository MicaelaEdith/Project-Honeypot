using UnityEngine;

/// <summary>
/// Sensor de obstaculos del jugador: raycast horizontales en los ejes
/// del tanque (y sus diagonales) contra la capa Decor, mas un rayo
/// dedicado a la linea player-camara. El casco se mide una vez sobre
/// los renderers reales (incluido el cañon) para saber donde termina
/// el casco visual, no solo el collider (que es mas chico).
/// </summary>
[DisallowMultipleComponent]
public class PlayerObstacleSensor : MonoBehaviour
{
    private const int MaxReadings = 8;
    private const float FallbackExtent = 1f;

    [Header("Raycast")]
    [Tooltip("Capa de los obstaculos (Decor = 3).")]
    [SerializeField] private LayerMask obstacleMask = 1 << 3;
    [Tooltip("Largo de los rayos. Mas alla de esto se considera libre.")]
    [SerializeField] private float rayLength = 40f;
    [Tooltip("Agregar las 4 diagonales: detecta esquinas al girar cerca de un muro.")]
    [SerializeField] private bool includeDiagonals = true;

    private struct Reading
    {
        public Vector3 direction;
        public float distance;
        public bool hit;
    }

    private readonly Reading[] readings = new Reading[MaxReadings];
    private int readingCount;
    private Bounds hullLocal;
    private bool hasHull;

    public int ReadingCount => readingCount;

    public bool HasHull => hasHull;

    private void Start()
    {
        ComputeHull();
    }

    /// <summary>
    /// Tira los rayos desde el centro del casco. Llamar cada frame
    /// despues del giro del tanque y antes de mover (TankController).
    /// </summary>
    public void Refresh(Vector3 forward)
    {
        Vector3 f = forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f)
        {
            f = transform.forward;
            f.y = 0f;
        }
        f.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, f);

        readingCount = 0;
        readings[readingCount++] = new Reading { direction = f };
        readings[readingCount++] = new Reading { direction = -f };
        readings[readingCount++] = new Reading { direction = right };
        readings[readingCount++] = new Reading { direction = -right };
        if (includeDiagonals)
        {
            readings[readingCount++] = new Reading { direction = (f + right).normalized };
            readings[readingCount++] = new Reading { direction = (f - right).normalized };
            readings[readingCount++] = new Reading { direction = (-f + right).normalized };
            readings[readingCount++] = new Reading { direction = (-f - right).normalized };
        }

        Vector3 origin = HullCenterWorld();
        for (int i = 0; i < readingCount; i++)
        {
            Vector3 direction = readings[i].direction;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, rayLength, obstacleMask))
            {
                readings[i] = new Reading { direction = direction, distance = hit.distance, hit = true };
            }
            else
            {
                readings[i] = new Reading { direction = direction, distance = rayLength, hit = false };
            }
        }
    }

    public Vector3 ReadingDirection(int index) => readings[index].direction;

    public float ReadingDistance(int index) => readings[index].distance;

    /// <summary>
    /// Centro del casco en el mundo (altura media del tanque).
    /// </summary>
    public Vector3 HullCenterWorld()
    {
        return hasHull ? transform.TransformPoint(hullLocal.center) : transform.position;
    }

    /// <summary>
    /// Cuanto mide el casco desde su centro hasta el borde en la
    /// direccion indicada (proyeccion del casco sobre esa direccion).
    /// </summary>
    public float ExtentAlong(Vector3 direction)
    {
        Vector3 d = direction;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f || !hasHull)
        {
            return FallbackExtent;
        }
        d.Normalize();

        return Mathf.Abs(Vector3.Dot(d, transform.right)) * hullLocal.extents.x
             + Mathf.Abs(Vector3.Dot(d, transform.up)) * hullLocal.extents.y
             + Mathf.Abs(Vector3.Dot(d, transform.forward)) * hullLocal.extents.z;
    }

    /// <summary>
    /// Rayo desde un origen hasta un destino. Devuelve true y la
    /// distancia del impacto si hay un obstaculo en el camino.
    /// </summary>
    public bool RaycastTowards(Vector3 origin, Vector3 destination, out float hitDistance)
    {
        Vector3 direction = destination - origin;
        float fullDistance = direction.magnitude;
        hitDistance = fullDistance;
        if (fullDistance < 0.0001f)
        {
            return false;
        }

        direction /= fullDistance;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, fullDistance, obstacleMask))
        {
            hitDistance = hit.distance;
            return true;
        }

        return false;
    }

    private void ComputeHull()
    {
        Bounds world = default;
        bool has = false;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (!has)
            {
                world = renderer.bounds;
                has = true;
            }
            else
            {
                world.Encapsulate(renderer.bounds);
            }
        }

        if (!has)
        {
            return;
        }

        // El AABB mundial medido en Start (el casco no cambia de
        // forma en runtime) se lleva al espacio local del tanque
        // transformando sus 8 esquinas y encapsulandolas.
        Vector3 center = world.center;
        Vector3 extents = world.extents;
        Bounds local = new Bounds(transform.InverseTransformPoint(center), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = new Vector3(
                        center.x + x * extents.x,
                        center.y + y * extents.y,
                        center.z + z * extents.z);
                    local.Encapsulate(transform.InverseTransformPoint(corner));
                }
            }
        }

        hullLocal = local;
        hasHull = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!hasHull || readingCount == 0)
        {
            return;
        }

        Vector3 origin = HullCenterWorld();
        for (int i = 0; i < readingCount; i++)
        {
            Vector3 end = origin + readings[i].direction * readings[i].distance;
            Gizmos.color = readings[i].hit ? Color.red : Color.green;
            Gizmos.DrawLine(origin, end);
            if (readings[i].hit)
            {
                Gizmos.DrawWireSphere(end, 0.5f);
            }
        }
    }
}
