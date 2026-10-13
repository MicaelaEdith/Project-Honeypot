using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class TankController : MonoBehaviour, IDamagable, IHealth
{

    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private float gravity = 40f;
    [SerializeField] private bool invertForward = false;
    [SerializeField] private bool invertTurn = false;
    [SerializeField] private Vector3 bodyForwardLocal = Vector3.zero;

    [SerializeField] private bool autoFitController = true;
    [Range(0.5f, 1f)]
    [SerializeField] private float fitWidthFactor = 0.8f;

    [SerializeField] private float obstacleStopMargin = 0.5f;

    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private int maxRespawns = 3;
    [SerializeField] private Transform spawnPointTransform;

    private CharacterController controller;
    private CannonController cannon;
    private PlayerObstacleSensor obstacleSensor;
    private Vector3 forwardLocal;
    private float verticalSpeed;
    private float currentHealth;
    private Vector3 spawnPoint;
    private Quaternion spawnRotation = Quaternion.identity;
    private int respawnsUsed;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public int RespawnsUsed => respawnsUsed;
    public int MaxRespawns => maxRespawns;
    public int RespawnsRemaining => Mathf.Max(maxRespawns - respawnsUsed, 0);

    public event System.Action<float, float> OnHealthChanged;
    public event System.Action OnRespawnLimitReached;
    public event System.Action<int> OnRespawnsChanged;

    private void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public Vector3 MovingForward
    {
        get
        {
            Vector3 direction = transform.TransformDirection(forwardLocal);
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }
            return transform.forward;
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cannon = GetComponentInChildren<CannonController>();
        obstacleSensor = GetComponent<PlayerObstacleSensor>();
    }

    private void Start()
    {
        forwardLocal = ResolveForward();
        if (autoFitController)
        {
            FitController();
        }
        SetHealth(maxHealth);
        ResolveSpawn();
        OnRespawnsChanged?.Invoke(RespawnsRemaining);
    }

    private void ResolveSpawn()
    {
        Transform spawn = spawnPointTransform;
        if (spawn == null)
        {
            GameObject found = GameObject.Find("SpawnerPlayer");
            if (found != null)
            {
                spawn = found.transform;
            }
        }

        if (spawn == null)
        {
            Debug.LogWarning("[TankController] No se encontro SpawnerPlayer. El spawn queda en la posicion actual.", this);
            spawnPoint = transform.position;
            spawnRotation = transform.rotation;
            return;
        }

        spawnPoint = spawn.position;
        spawnRotation = spawn.rotation;
        transform.SetPositionAndRotation(spawnPoint, spawnRotation);
        verticalSpeed = 0f;
    }

    public void TakeDamage(float damage)
    {
        if (currentHealth <= 0f)
        {
            return;
        }

        SetHealth(currentHealth - Mathf.Max(0f, damage));
        Debug.Log($"[TankController] El tanque recibio {damage} de daño. Vida: {currentHealth}", this);

        if (currentHealth <= 0f)
        {
            HandleDeath();
        }
    }

    private void HandleDeath()
    {
        if (respawnsUsed >= maxRespawns)
        {
            Debug.Log("[TankController] Sin respawns restantes.", this);
            OnRespawnLimitReached?.Invoke();
            return;
        }

        respawnsUsed++;
        OnRespawnsChanged?.Invoke(RespawnsRemaining);
        Respawn();
    }

    public void Healing(int amount)
    {
        if (amount < 0)
        {
            return;
        }
        SetHealth(currentHealth + amount);
    }

    private void Respawn()
    {
        Debug.Log("[TankController] El tanque fue destruido. Respawneando...", this);
        SetHealth(maxHealth);
        transform.SetPositionAndRotation(spawnPoint, spawnRotation);
        verticalSpeed = 0f;
    }

    private void Update()
    {
        float throttle = 0f;
        float turn = 0f;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) throttle += 1f;
            if (keyboard.sKey.isPressed) throttle -= 1f;
            if (keyboard.aKey.isPressed) turn -= 1f;
            if (keyboard.dKey.isPressed) turn += 1f;
        }

        if (invertForward) throttle = -throttle;
        if (invertTurn) turn = -turn;

        transform.Rotate(Vector3.up, turn * turnSpeed * Time.deltaTime, Space.World);

        Vector3 forward = transform.TransformDirection(forwardLocal);
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
        {
            forward.Normalize();
        }

        obstacleSensor?.Refresh(forward);

        if (controller.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }
        verticalSpeed -= gravity * Time.deltaTime;

        Vector3 motion = forward * (throttle * moveSpeed) + Vector3.up * verticalSpeed;
        motion = ApplyObstacleBraking(motion);
        controller.Move(motion * Time.deltaTime);
    }

    private Vector3 ApplyObstacleBraking(Vector3 motion)
    {
        if (obstacleSensor == null || obstacleStopMargin <= 0f)
        {
            return motion;
        }

        for (int i = 0; i < obstacleSensor.ReadingCount; i++)
        {
            Vector3 direction = obstacleSensor.ReadingDirection(i);
            float clearance = obstacleSensor.ReadingDistance(i) - obstacleSensor.ExtentAlong(direction);
            if (clearance >= obstacleStopMargin)
            {
                continue;
            }

            float scale = Mathf.Clamp01(Mathf.Max(clearance, 0f) / obstacleStopMargin);
            float towards = Vector3.Dot(motion, direction);
            if (towards > 0f)
            {
                motion -= direction * (towards * (1f - scale));
            }
        }

        return motion;
    }

    private Vector3 ResolveForward()
    {
        if (bodyForwardLocal.sqrMagnitude > 0.0001f)
        {
            Vector3 manual = bodyForwardLocal;
            manual.y = 0f;
            if (manual.sqrMagnitude > 0.0001f)
            {
                return manual.normalized;
            }
        }

        if (cannon != null)
        {
            Vector3 barrel = cannon.BarrelDirection;
            barrel.y = 0f;
            if (barrel.sqrMagnitude > 0.0001f)
            {
                Vector3 local = transform.InverseTransformDirection(barrel.normalized);
                local.y = 0f;
                if (local.sqrMagnitude > 0.0001f)
                {
                    return local.normalized;
                }
            }
        }

        return Vector3.forward;
    }

    private void FitController()
    {
        if (!TryGetBodyBounds(out Bounds world))
        {
            return;
        }

        Vector3 a = transform.InverseTransformPoint(world.min);
        Vector3 b = transform.InverseTransformPoint(world.max);
        Vector3 min = Vector3.Min(a, b);
        Vector3 max = Vector3.Max(a, b);
        Vector3 size = max - min;

        float radius = Mathf.Min(size.x, size.z) * 0.5f * fitWidthFactor;
        radius = Mathf.Max(radius, 0.1f);
        float height = Mathf.Max(size.y, radius * 2.05f);

        controller.radius = radius;
        controller.height = height;
        controller.center = new Vector3(
            (min.x + max.x) * 0.5f,
            min.y + height * 0.5f,
            (min.z + max.z) * 0.5f);
        controller.stepOffset = height * 0.1f;
        controller.skinWidth = radius * 0.1f;
    }

    private bool TryGetBodyBounds(out Bounds bounds)
    {
        bounds = default;
        bool has = false;

        Transform excluded = cannon != null ? cannon.CannonRoot : null;
        if (excluded == transform)
        {
            excluded = null;
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (IsInside(renderer.transform, excluded))
            {
                continue;
            }

            if (!has)
            {
                bounds = renderer.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return has;
    }

    private static bool IsInside(Transform candidate, Transform ancestor)
    {
        if (ancestor == null)
        {
            return false;
        }

        Transform current = candidate;
        while (current != null)
        {
            if (current == ancestor)
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }
}
