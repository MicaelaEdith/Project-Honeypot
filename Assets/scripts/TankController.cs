using System.Collections.Generic;
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
    [SerializeField] private float fitWidthFactor = 0.8f;

    [SerializeField] private float obstacleStopMargin = 0.5f;

    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private int maxRespawns = 3;
    [SerializeField] private Transform spawnPointTransform;

    [SerializeField] private float respawnFadeOutDuration = 0.6f;
    [SerializeField] private float respawnBlackHold = 0.35f;
    [SerializeField] private float respawnFadeInDuration = 0.6f;

    [SerializeField] private float fallResetDepth = 10f;

    [SerializeField] private float enemyPushMargin = 0.1f;
    [SerializeField] private float enemyPushRange = 25f;
    [SerializeField] private float enemyBlockSkin = 2.0f;
    [SerializeField] private bool debugEnemyBlocking = true;

    private float nextEnemyDebugTime;

    private CharacterController controller;
    private CannonController cannon;
    private PlayerObstacleSensor obstacleSensor;
    private CameraFollow cameraFollow;
    private Vector3 forwardLocal;
    private float verticalSpeed;
    private float currentHealth;
    private Vector3 spawnPoint;
    private Quaternion spawnRotation = Quaternion.identity;
    private int respawnsUsed;
    private bool isRespawning;

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
        cameraFollow = FindFirstObjectByType<CameraFollow>();
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

        if (damage > 0f)
        {
            AudioManager.Instance?.PlayDamage();
        }

        if (currentHealth <= 0f)
        {
            HandleDeath();
        }
    }

    private void HandleDeath()
    {
        AudioManager.Instance?.PlayPlayerDeath();

        if (respawnsUsed >= maxRespawns)
        {
            Debug.Log("[TankController] Sin respawns restantes.", this);
            OnRespawnLimitReached?.Invoke();
            return;
        }

        respawnsUsed++;
        OnRespawnsChanged?.Invoke(RespawnsRemaining);

        if (!isRespawning)
        {
            StartCoroutine(RespawnRoutine());
        }
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        isRespawning = true;

        ScreenFader fader = ScreenFader.Instance;
        if (fader != null)
        {
            yield return fader.FadeOut(respawnFadeOutDuration);
        }

        Respawn();

        if (respawnBlackHold > 0f)
        {
            yield return new WaitForSecondsRealtime(respawnBlackHold);
        }

        if (fader != null)
        {
            yield return fader.FadeIn(respawnFadeInDuration);
        }

        isRespawning = false;
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
        TeleportToSpawn();

        if (cameraFollow != null)
        {
            cameraFollow.SnapToTarget();
        }
    }

    private void TeleportToSpawn()
    {
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.SetPositionAndRotation(spawnPoint + Vector3.up * 0.1f, spawnRotation);
        controller.enabled = wasEnabled;
        Physics.SyncTransforms();

        verticalSpeed = 0f;
    }

    private void Update()
    {
        if (isRespawning || Time.timeScale <= 0f)
        {
            return;
        }

        LogEnemyBlockingDiagnostics();

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
        motion = ApplyEnemyBlocking(motion);
        controller.Move(motion * Time.deltaTime);

        ResolveEnemyOverlaps();

        if (fallResetDepth > 0f && transform.position.y < spawnPoint.y - fallResetDepth)
        {
            Debug.LogWarning("[TankController] El player cayo fuera del mundo; se lo devuelve al spawn.", this);
            TeleportToSpawn();
        }
    }

    private void ResolveEnemyOverlaps()
    {
        IReadOnlyList<EnemyTemplate> enemies = EnemyTemplate.ActiveEnemies;
        if (enemies == null || enemies.Count == 0)
        {
            return;
        }

        float playerRadius = PlayerHullRadius();
        float rangeSqr = Mathf.Max(enemyPushRange, 0f) * Mathf.Max(enemyPushRange, 0f);

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyTemplate enemy = enemies[i];
            if (enemy == null || !enemy.IsBlocking)
            {
                continue;
            }

            Collider other = enemy.BodyCollider;
            if (other == null || !other.enabled)
            {
                continue;
            }

            if (!TryGetEnemyProximity(other, out Vector3 outward, out float hullDistance))
            {
                continue;
            }

            if (hullDistance * hullDistance > rangeSqr)
            {
                continue;
            }

            float overlap = playerRadius + Mathf.Max(enemyPushMargin, 0f) - hullDistance;
            if (overlap <= 0f)
            {
                continue;
            }

            controller.Move(outward * overlap);
        }
    }

    private Vector3 ApplyEnemyBlocking(Vector3 motion)
    {
        Vector3 flatMotion = new Vector3(motion.x, 0f, motion.z);
        float speed = flatMotion.magnitude;
        if (speed < 0.0001f)
        {
            return motion;
        }

        IReadOnlyList<EnemyTemplate> enemies = EnemyTemplate.ActiveEnemies;
        if (enemies == null || enemies.Count == 0)
        {
            return motion;
        }

        float playerRadius = PlayerHullRadius();
        float blockDistance = playerRadius + Mathf.Max(enemyBlockSkin, 0f);
        Vector3 moveDirection = flatMotion / speed;
        Vector3 tangential = moveDirection;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyTemplate enemy = enemies[i];
            if (enemy == null || !enemy.IsBlocking)
            {
                continue;
            }

            Collider other = enemy.BodyCollider;
            if (other == null || !other.enabled)
            {
                continue;
            }

            if (!TryGetEnemyProximity(other, out Vector3 outward, out float hullDistance))
            {
                continue;
            }

            if (hullDistance > blockDistance)
            {
                continue;
            }

            Vector3 intoEnemy = -outward;
            float towards = Vector3.Dot(moveDirection, intoEnemy);
            if (towards > 0f)
            {
                tangential -= intoEnemy * towards;
            }
        }

        if (tangential.sqrMagnitude < 0.0000001f)
        {
            return new Vector3(0f, motion.y, 0f);
        }

        tangential = tangential.normalized * speed;
        return new Vector3(tangential.x, motion.y, tangential.z);
    }

    private void LogEnemyBlockingDiagnostics()
    {
        if (!debugEnemyBlocking || Time.unscaledTime < nextEnemyDebugTime)
        {
            return;
        }
        nextEnemyDebugTime = Time.unscaledTime + 0.15f;

        IReadOnlyList<EnemyTemplate> enemies = EnemyTemplate.ActiveEnemies;
        Vector3 center = PlayerHullCenter();
        float best = float.MaxValue;
        EnemyTemplate bestEnemy = null;
        Collider bestCollider = null;

        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyTemplate e = enemies[i];
                if (e == null)
                {
                    continue;
                }

                Collider c = e.BodyCollider;
                if (c == null)
                {
                    continue;
                }

                Vector3 closest = c.ClosestPoint(center);
                Vector3 d = closest - center;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < best)
                {
                    best = dist;
                    bestEnemy = e;
                    bestCollider = c;
                }
            }
        }

        if (bestEnemy == null || best > 12f)
        {
            return;
        }

        Vector3 toTransform = bestEnemy.transform.position - center;
        toTransform.y = 0f;

        string info = bestEnemy != null
            ? $"{bestEnemy.name} state={bestEnemy.CurrentState} blocking={bestEnemy.IsBlocking} colEnabled={bestCollider.enabled} colType={bestCollider.GetType().Name} colSize={bestCollider.bounds.size} colCenter={bestCollider.bounds.center} enemyPos={bestEnemy.transform.position} hullRadius={bestEnemy.BodyRadius:F2} distCollider={best:F2} distTransform={toTransform.magnitude:F2}"
            : "ninguno";

        Debug.Log($"[BlockDiag] playerPos={transform.position} playerCenter={center} hullRadius={PlayerHullRadius():F2} blockDistance={PlayerHullRadius() + Mathf.Max(enemyBlockSkin, 0f):F2} enemies={enemies?.Count ?? -1} nearest=[{info}]");
    }

    private bool TryGetEnemyProximity(Collider other, out Vector3 outward, out float hullDistance)
    {
        outward = Vector3.forward;
        hullDistance = 0f;

        Vector3 center = PlayerHullCenter();
        Bounds bounds = other.bounds;

        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float halfHeight = controller.height * 0.5f * scaleY;
        if (center.y + halfHeight < bounds.min.y || center.y - halfHeight > bounds.max.y)
        {
            return false;
        }

        Vector3 closest = other.ClosestPoint(center);
        Vector3 away = center - closest;
        away.y = 0f;
        float distance = away.magnitude;

        if (distance > 0.0001f)
        {
            outward = away / distance;
            hullDistance = distance;
            return true;
        }

        if (other is BoxCollider box)
        {
            outward = BoxNearestFaceOutward(box, center);
            return true;
        }

        Vector3 fromCenter = center - bounds.center;
        fromCenter.y = 0f;
        if (fromCenter.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        outward = fromCenter.normalized;
        return true;
    }

    private Vector3 BoxNearestFaceOutward(BoxCollider box, Vector3 worldPoint)
    {
        Transform boxTransform = box.transform;
        Vector3 local = boxTransform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 half = box.size * 0.5f;

        float penetrationX = half.x - Mathf.Abs(local.x);
        float penetrationZ = half.z - Mathf.Abs(local.z);

        Vector3 localDirection = penetrationX < penetrationZ
            ? new Vector3(Mathf.Sign(local.x), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(local.z));

        Vector3 world = boxTransform.rotation * localDirection;
        world.y = 0f;
        return world.sqrMagnitude > 0.0001f ? world.normalized : Vector3.forward;
    }

    private Vector3 PlayerHullCenter()
    {
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        return transform.position + transform.up * (controller.center.y * scaleY);
    }

    private float PlayerHullRadius()
    {
        float scaleXZ = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        return controller.radius * scaleXZ;
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
