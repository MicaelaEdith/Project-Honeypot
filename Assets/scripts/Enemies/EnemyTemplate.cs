using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyTemplate : MonoBehaviour, IDamagable
{

    [SerializeField] private float maxHealth = 60f;
    [SerializeField] private float detectionRange = 0f;
    [SerializeField] protected float attackRange = 0f;
    [SerializeField] protected float damageAmount = 12f;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float hoverHeight = 15f;

    [SerializeField] private float detectionRangeFactor = 2.5f;
    [SerializeField] private float attackRangeFactor = 1.5f;
    [SerializeField] private float projectileSizeFactor = 0.05f;
    [SerializeField] private float muzzleMarginFactor = 0.1f;
    [SerializeField] private float flightTime = 1.2f;

    protected float FlightTime => flightTime;
    [SerializeField] private float projectileSpeed = 0f;
    [SerializeField] private float projectileScale = 0f;

    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private float impactVfxDiameterFactor = 2f;
    [SerializeField] private float impactVfxReferenceDiameter = 5.5f;
    [SerializeField] private float impactVfxScale = 1f;

    [SerializeField] private bool rotateTowardPlayer = true;
    [SerializeField] private float rotateSpeed = 120f;
    [SerializeField] private Vector3 modelForward = Vector3.forward;

    [SerializeField] private bool contactDamage = false;
    [SerializeField] private float contactRange = 0f;
    [SerializeField] private float contactCooldown = 1f;

    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private float deathVfxScaleMultiplier = 5.7f;
    [SerializeField] private int deathVfxExplosionCount = 3;
    [SerializeField] private float deathVfxExplosionDelay = 0.12f;
    [SerializeField] private float deathVfxExplosionSpread = 0.4f;
    [SerializeField] private float deathVfxParticleMultiplier = 4f;
    [SerializeField] private float deathSwellScale = 1.15f;
    [SerializeField] private int deathSwellPulses = 3;
    [SerializeField] private float deathSwellDuration = 0.5f;

    [SerializeField] private float avoidanceRadiusFactor = 1f;

    [SerializeField] private float weaveAmplitudeFactor = 0f;
    [SerializeField] private float weaveFrequency = 2f;
    [SerializeField] private float weaveFadeDistanceFactor = 0.5f;

    protected NavMeshAgent agent;
    protected EnemyState currentState { get; private set; } = EnemyState.Idle;
    protected Transform player { get; private set; }
    protected bool IsLured => lurePosition.HasValue;
    public EnemyState CurrentState => currentState;

    public event System.Action<EnemyTemplate> Died;

    public EnemyPool Pool { get; private set; }

    public void AttachPool(EnemyPool owner)
    {
        Pool = owner;
    }

    protected Vector3 ForwardFlat
    {
        get
        {
            Vector3 flat = ProjectFlat(modelForward);
            if (flat.sqrMagnitude >= 0.0001f)
            {
                return flat;
            }

            Vector3[] fallbackAxes = { Vector3.forward, Vector3.up, Vector3.right };
            foreach (Vector3 axis in fallbackAxes)
            {
                flat = ProjectFlat(axis);
                if (flat.sqrMagnitude >= 0.0001f)
                {
                    return flat;
                }
            }

            if (agent != null)
            {
                flat = agent.velocity;
                flat.y = 0f;
                if (flat.sqrMagnitude >= 0.0001f)
                {
                    return flat.normalized;
                }
            }
            return Vector3.zero;
        }
    }

    private Vector3 ProjectFlat(Vector3 localAxis)
    {
        Vector3 world = transform.TransformDirection(localAxis);
        world.y = 0f;
        return world.sqrMagnitude >= 0.0001f ? world.normalized : Vector3.zero;
    }

    private static TankController cachedPlayer;

    private GameObject playerStyleProjectile;
    private GameObject playerStyleImpactVfx;
    private Vector3 aimSample;
    private float aimSampleTime;
    private bool hasAimSample;

    private float currentHealth;
    private float nextAttackTime;
    private float nextContactTime;
    private Vector3? lurePosition;
    private float sinkEndTime;
    private bool sinkStarted;
    private Vector3 deathStartScale;
    private bool hoverChecked;
    private float hullRadius;
    private Bounds hullBounds;
    private Vector3 baseLocalScale = Vector3.one;
    private Quaternion baseLocalRotation = Quaternion.identity;
    private float weavePhase;
    private bool diedNotified;

    protected float HullRadius()
    {
        return Mathf.Max(hullRadius, 0.0001f);
    }

    protected Bounds HullBounds()
    {
        return hullBounds;
    }

    protected float DetectionRange()
    {
        return detectionRange > 0f ? detectionRange : HullRadius() * Mathf.Max(detectionRangeFactor, 0f);
    }

    protected float AttackRange()
    {
        return attackRange > 0f ? attackRange : HullRadius() * Mathf.Max(attackRangeFactor, 0f);
    }

    protected float ProjectileDiameter()
    {
        if (projectileScale > 0f)
        {
            return projectileScale;
        }
        return HullRadius() * Mathf.Max(projectileSizeFactor, 0f);
    }

    protected float ProjectileSpeedTo(Vector3 from, Vector3 to)
    {
        if (projectileSpeed > 0f)
        {
            return projectileSpeed;
        }
        float seconds = Mathf.Max(flightTime, 0.05f);
        return Mathf.Max(Vector3.Distance(from, to) / seconds, 0.0001f);
    }

    protected float MuzzleDistanceAlong(Vector3 direction, float manualDistance)
    {
        if (manualDistance > 0f)
        {
            return manualDistance;
        }

        Vector3 extents = HullBounds().extents;
        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        float support = Mathf.Abs(dir.x) * extents.x
                      + Mathf.Abs(dir.y) * extents.y
                      + Mathf.Abs(dir.z) * extents.z;

        return support * (1f + Mathf.Max(muzzleMarginFactor, 0f));
    }

    protected float ImpactVfxScale(float shellDiameter)
    {
        float reference = Mathf.Max(impactVfxReferenceDiameter, 0.0001f);
        float ratio = shellDiameter * Mathf.Max(impactVfxDiameterFactor, 0f) / reference;
        return ratio * Mathf.Max(impactVfxScale, 0f);
    }


    protected static float WorldDiameter(GameObject target)
    {
        float max = 0f;
        foreach (Collider other in target.GetComponentsInChildren<Collider>())
        {
            Vector3 size = other.bounds.size;
            max = Mathf.Max(max, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        }
        return max;
    }

    protected Vector3 PlayerAimPoint()
    {
        if (player == null)
        {
            return transform.position;
        }

        Bounds bounds = PlayerBounds();
        if (bounds.size.sqrMagnitude > 0.0001f)
        {
            return bounds.center;
        }

        return PlayerControllerCenter();
    }

    protected Bounds PlayerBounds()
    {
        Bounds bounds = default;
        bool found = false;

        if (player == null)
        {
            return bounds;
        }

        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
        {
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private Vector3 PlayerControllerCenter()
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null)
        {
            return player.position;
        }

        float scale = Mathf.Max(Mathf.Abs(player.lossyScale.y), 0.0001f);
        return player.position + player.up * (controller.center.y + controller.height * 0.5f) * scale;
    }

    protected Vector3 HullCenter()
    {
        Bounds bounds = HullBounds();
        return bounds.size.sqrMagnitude > 0.0001f ? bounds.center : transform.position;
    }

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        baseLocalScale = transform.localScale;
        baseLocalRotation = transform.localRotation;
        weavePhase = Random.value * Mathf.PI * 2f;
    }

    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = !rotateTowardPlayer;
        agent.updateUpAxis = false;
        agent.baseOffset = HoverToBaseOffset(hoverHeight);
        player = FindPlayer();
        currentHealth = maxHealth;
        FitColliderToMesh();
        float rootScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z), 0.0001f);
        agent.radius = HullRadius() * Mathf.Max(avoidanceRadiusFactor, 0f) / rootScale;
        PlaceOnNavMesh();
        ResolvePlayerStyleAssets();
    }

    private void ResolvePlayerStyleAssets()
    {
        playerStyleProjectile = projectilePrefab;
        playerStyleImpactVfx = impactVfxPrefab;

        if (playerStyleProjectile != null && playerStyleImpactVfx != null)
        {
            return;
        }

        if (cachedPlayer == null)
        {
            return;
        }

        CannonController cannon = cachedPlayer.GetComponentInChildren<CannonController>();
        if (cannon == null)
        {
            return;
        }

        if (playerStyleProjectile == null)
        {
            playerStyleProjectile = cannon.ProjectilePrefab;
        }
        if (playerStyleImpactVfx == null)
        {
            playerStyleImpactVfx = cannon.ImpactVfxPrefab;
        }
    }

    private float HoverToBaseOffset(float worldHover)
    {
        if (worldHover <= 0f)
        {
            return 0f;
        }

        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.y), 0.0001f);
        return worldHover / scale;
    }

    private void VerifyHoverOffset()
    {
        hoverChecked = true;

        if (hoverHeight <= 0f || agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, agent.areaMask))
        {
            return;
        }

        float real = transform.position.y - hit.position.y;
        if (Mathf.Abs(real - hoverHeight) > 1f)
        {
            Debug.LogWarning(
                $"[EnemyTemplate] {name}: hoverHeight {hoverHeight:F1} quedo en " +
                $"{real:F1}u sobre el navmesh (baseOffset {agent.baseOffset} x escala " +
                $"{transform.lossyScale.y:F1}).", this);
        }
    }

    protected virtual void Update()
    {
        if (!hoverChecked)
        {
            VerifyHoverOffset();
        }

        if (player == null)
        {
            return;
        }

        switch (currentState)
        {
            case EnemyState.Idle: UpdateIdleState(); break;
            case EnemyState.Chase: UpdateChaseState(); break;
            case EnemyState.Attack: UpdateAttackState(); break;
            case EnemyState.Death: UpdateDeathState(); break;
        }
    }

    public void TakeDamage(float damage)
    {
        if (currentState == EnemyState.Death)
        {
            return;
        }

        currentHealth -= Mathf.Max(0f, damage);
        Debug.Log($"[{name}] recibio {damage} de daño. Vida: {currentHealth}", this);

        if (currentHealth <= 0f)
        {
            currentState = EnemyState.Death;
        }
    }

    public void SetLurePosition(Vector3 position)
    {
        lurePosition = position;
    }

    public void ClearLure()
    {
        lurePosition = null;
    }

    protected bool CanAttack()
    {
        if (Time.time < nextAttackTime)
        {
            return false;
        }
        nextAttackTime = Time.time + attackCooldown;
        return true;
    }

    protected void ApplyDamageToPlayer(float amount)
    {
        if (player == null)
        {
            return;
        }

        Collider collider = player.GetComponentInChildren<Collider>();
        IDamagable damagable = player.GetComponent<IDamagable>();
        if (damagable == null && collider != null)
        {
            damagable = collider.GetComponent<IDamagable>();
        }
        damagable?.TakeDamage(amount);
    }

    protected GameObject SpawnEnemyProjectile(Vector3 position, Vector3 direction, Vector3 target, float damage)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "EnemyProjectile";
        projectile.transform.localScale = Vector3.one * ProjectileDiameter();
        projectile.transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(direction.normalized, Vector3.up));

        Collider collider = projectile.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false;

            foreach (Collider own in GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(collider, own, true);
            }
        }

        Rigidbody body = projectile.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = projectile.AddComponent<Rigidbody>();
        }
        body.useGravity = false;
        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        EnemyProjectile script = projectile.AddComponent<EnemyProjectile>();
        script.damage = damage;
        script.Launch(direction, ProjectileSpeedTo(position, target));

        return projectile;
    }

    protected GameObject SpawnPlayerStyleProjectile(Vector3 position, Vector3 direction, Vector3 target, float damage)
    {
        if (playerStyleProjectile == null)
        {
            return SpawnEnemyProjectile(position, direction, target, damage);
        }

        Vector3 shot = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        GameObject projectile = Instantiate(playerStyleProjectile);
        projectile.name = "EnemyShell";

        float diameter = ProjectileDiameter();
        float current = WorldDiameter(projectile);
        projectile.transform.localScale *= current > 0.0001f ? diameter / current : 1f;

        projectile.transform.SetPositionAndRotation(position, LookRotationSafe(shot));

        Collider collider = projectile.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false;

            foreach (Collider own in GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(collider, own, true);
            }
        }

        Rigidbody body = projectile.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = projectile.AddComponent<Rigidbody>();
        }

        body.useGravity = false;
        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = shot * ProjectileSpeedTo(position, target);

        Projectile script = projectile.GetComponent<Projectile>();
        if (script == null)
        {
            script = projectile.AddComponent<Projectile>();
        }
        script.damage = damage;
        script.playerOnly = true;
        script.impactVfx = playerStyleImpactVfx;
        script.impactVfxScale = ImpactVfxScale(diameter);

        return projectile;
    }

    protected static Quaternion LookRotationSafe(Vector3 direction)
    {
        Vector3 normalized = direction.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(normalized, up);
    }

    private void UpdateIdleState()
    {
        if (IsLured)
        {
            currentState = EnemyState.Chase;
            SetStopped(false);
            return;
        }

        if (Vector3.Distance(transform.position, TargetPosition()) <= DetectionRange())
        {
            currentState = EnemyState.Chase;
            SetStopped(false);
        }
    }

    private void UpdateChaseState()
    {
        if (IsLured)
        {
            TrySetDestination(TargetPosition());
            SetStopped(false);
            return;
        }

        float distance = Vector3.Distance(transform.position, TargetPosition());

        if (distance > DetectionRange())
        {
            currentState = EnemyState.Idle;
            SetStopped(true);
        }
        else if (distance <= AttackRange())
        {
            currentState = EnemyState.Attack;
            SetStopped(true);
        }
        else
        {
            TrySetDestination(WeavePoint(TargetPosition()));
            SetStopped(false);
        }

        RotateIfNeeded(TargetPosition());
    }

    private Vector3 WeavePoint(Vector3 target)
    {
        float amplitude = HullRadius() * Mathf.Max(weaveAmplitudeFactor, 0f);
        if (amplitude <= 0.0001f)
        {
            return target;
        }

        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return target;
        }

        Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized);
        if (side.sqrMagnitude < 0.0001f)
        {
            return target;
        }

        float fade = 1f;
        if (weaveFadeDistanceFactor > 0f)
        {
            float fadeDistance = HullRadius() * weaveFadeDistanceFactor;
            fade = Mathf.Clamp01(toTarget.magnitude / fadeDistance);
        }

        float offset = Mathf.Sin(Time.time * weaveFrequency + weavePhase) * amplitude * fade;
        return target + side * offset;
    }

    private void UpdateAttackState()
    {
        if (IsLured)
        {
            currentState = EnemyState.Chase;
            SetStopped(false);
            return;
        }

        float distance = Vector3.Distance(transform.position, TargetPosition());

        if (distance > DetectionRange())
        {
            currentState = EnemyState.Idle;
            SetStopped(true);
            return;
        }

        if (distance > AttackRange())
        {
            currentState = EnemyState.Chase;
            return;
        }

        RotateIfNeeded(TargetPosition());
        TryContactDamage(distance);
        AttackTick();
    }

    protected virtual void AttackTick()
    {
    }

    private void UpdateDeathState()
    {
        if (!sinkStarted)
        {
            sinkStarted = true;
            agent.enabled = false;
            sinkEndTime = Time.time + deathSwellDuration;
            deathStartScale = transform.localScale;
            SetCollidersEnabled(false);
            OnDeathStarted();
        }

        float duration = Mathf.Max(deathSwellDuration, 0.0001f);
        float t = Mathf.Clamp01(1f - (sinkEndTime - Time.time) / duration);

        if (Time.time < sinkEndTime)
        {
            float pulses = Mathf.Max(deathSwellPulses, 1);
            float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * (pulses - 0.5f)));
            float amplitude = Mathf.Lerp(0.6f, 1f, t);
            transform.localScale = deathStartScale * Mathf.Lerp(1f, deathSwellScale, pulse * amplitude);
        }
        else
        {
            SpawnDeathExplosion();
            FinishDeath();
        }
    }


    private void FinishDeath()
    {
        if (diedNotified)
        {
            return;
        }
        diedNotified = true;

        Died?.Invoke(this);

        if (Pool != null)
        {
            Pool.Reclaim(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ResetForSpawn(Vector3 position, Quaternion rotation)
    {
        gameObject.SetActive(true);


        if (agent != null)
        {
            agent.enabled = false;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 50f, agent.areaMask))
            {
                position = hit.position;
            }
        }

        transform.SetPositionAndRotation(position, SpawnRotation(rotation));
        transform.localScale = baseLocalScale;

        currentHealth = maxHealth;
        currentState = EnemyState.Idle;
        lurePosition = null;

        sinkStarted = false;
        sinkEndTime = 0f;
        diedNotified = false;
        hoverChecked = false;
        hasAimSample = false;
        nextAttackTime = 0f;
        nextContactTime = 0f;
        weavePhase = Random.value * Mathf.PI * 2f;

        SetCollidersEnabled(true);
        FitColliderToMesh();

        if (agent != null)
        {
            agent.enabled = true;
            agent.baseOffset = HoverToBaseOffset(hoverHeight);
        }
        PlaceOnNavMesh();

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }

        Debug.Log($"[{name}] ResetForSpawn en {position}.", this);
    }


    private Quaternion SpawnRotation(Quaternion spawnRotation)
    {
        Vector3 forward = spawnRotation * Vector3.forward;
        forward.y = 0f;
        Quaternion heading = forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward, Vector3.up)
            : Quaternion.identity;

        Quaternion parentWorld = transform.parent != null
            ? transform.parent.rotation
            : Quaternion.identity;

        return heading * parentWorld * baseLocalRotation;
    }

    private void SpawnDeathExplosion()
    {
        GameObject deathVfx = deathVfxPrefab != null
            ? deathVfxPrefab
            : Resources.Load<GameObject>("DeathVFX");
        if (deathVfx == null)
        {
            deathVfx = playerStyleImpactVfx;
        }
        if (deathVfx == null)
        {
            return;
        }

        Bounds live = LiveHullBounds();
        Vector3 deathPos = live.size.sqrMagnitude > 0.0001f ? live.center : transform.position;
        float hullR = HullRadius();
        float deathScale = ImpactVfxScale(hullR * 2f) * deathVfxScaleMultiplier;
        int count = Mathf.Max(deathVfxExplosionCount, 1);

        for (int i = 0; i < count; i++)
        {
            Vector3 offset = Vector3.zero;
            if (i > 0)
            {
                Vector2 circle = Random.insideUnitCircle * hullR * deathVfxExplosionSpread;
                offset = new Vector3(circle.x, Random.Range(-0.5f, 0.5f) * hullR * deathVfxExplosionSpread, circle.y);
            }

            float delay = i * deathVfxExplosionDelay;
            float scale = deathScale * (i == 0 ? 1f : 0.8f);
            GameObject vfx = Instantiate(deathVfx, deathPos + offset, Quaternion.identity);

            foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.startDelay = delay;
                main.startSizeMultiplier *= scale;

                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTimeMultiplier *= deathVfxParticleMultiplier;
                emission.rateOverDistanceMultiplier *= deathVfxParticleMultiplier;
                for (int b = 0; b < emission.burstCount; b++)
                {
                    ParticleSystem.Burst burst = emission.GetBurst(b);
                    float baseCount = Mathf.Max(burst.count.constant, burst.count.constantMin, 1f);
                    float boosted = baseCount * deathVfxParticleMultiplier;
                    burst.count = new ParticleSystem.MinMaxCurve(boosted, boosted);
                    emission.SetBurst(b, burst);
                }
            }

            Debug.Log($"[{name}] DeathVFX {i + 1}/{count} spawn={vfx.name} pos={vfx.transform.position} hullR={hullR:F2} scale={scale:F2} delay={delay:F2}", this);
            Destroy(vfx, 4f + delay);
        }
    }

    protected virtual void OnDeathStarted()
    {
    }


    protected void OnDrawGizmosSelected()
    {
        Vector3 nose = ForwardFlat;
        if (nose.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Bounds bounds = GizmoHullBounds();
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        if (radius < 0.0001f)
        {
            return;
        }

        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(bounds.center, bounds.center + nose * radius * 1.4f);
        Gizmos.DrawWireSphere(bounds.center + nose * radius * 1.4f, radius * 0.08f);

        Gizmos.color = Color.cyan;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            Gizmos.DrawWireSphere(renderer.bounds.center, radius * 0.05f);
        }
    }

    private Bounds LiveHullBounds()
    {
        return ComputeHullBounds();
    }

    private Bounds GizmoHullBounds()
    {
        if (Application.isPlaying && hullBounds.size.sqrMagnitude > 0.0001f)
        {
            return hullBounds;
        }

        return ComputeHullBounds();
    }

    private Bounds ComputeHullBounds(Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.one);
        }

        Bounds bounds = default;
        bool found = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Bounds local = renderer.localBounds;
            Matrix4x4 matrix = renderer.transform.localToWorldMatrix;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(x, y, z));
                        Vector3 world = matrix.MultiplyPoint3x4(corner);

                        if (!found)
                        {
                            bounds = new Bounds(world, Vector3.zero);
                            found = true;
                        }
                        else
                        {
                            bounds.Encapsulate(world);
                        }
                    }
                }
            }
        }

        if (!found)
        {
            return new Bounds(transform.position, Vector3.one);
        }

        return bounds;
    }

    private Bounds ComputeHullBounds()
    {
        return ComputeHullBounds(GetComponentsInChildren<Renderer>());
    }

    private void TryContactDamage(float distance)
    {

        float reach = contactRange > 0f ? contactRange : AttackRange();

        if (!contactDamage || distance > reach || Time.time < nextContactTime)
        {
            return;
        }

        nextContactTime = Time.time + contactCooldown;
        ApplyDamageToPlayer(damageAmount);
    }

    private void RotateIfNeeded(Vector3 target)
    {
        if (!rotateTowardPlayer)
        {
            return;
        }

        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }
        direction.Normalize();

        Vector3 current = ForwardFlat;
        if (current.sqrMagnitude < 0.0001f)
        {
            return;
        }
        current.Normalize();

        float angle = Vector3.SignedAngle(current, direction, Vector3.up);
        float step = Mathf.Clamp(angle, -rotateSpeed * Time.deltaTime, rotateSpeed * Time.deltaTime);
        if (Mathf.Abs(step) < 0.0001f)
        {
            return;
        }
        transform.Rotate(Vector3.up, step, Space.World);
    }

    protected Vector3 LeadAimPoint(float seconds)
    {
        Vector3 aim = PlayerAimPoint();

        if (seconds > 0.05f && hasAimSample)
        {
            float dt = Time.time - aimSampleTime;
            if (dt >= 0.05f && dt <= 3f)
            {
                Vector3 velocity = (aim - aimSample) / dt;
                return aim + velocity * seconds;
            }
        }

        aimSample = aim;
        aimSampleTime = Time.time;
        hasAimSample = true;
        return aim;
    }

    private Vector3 TargetPosition()
    {
        if (lurePosition.HasValue)
        {
            return lurePosition.Value;
        }
        return player != null ? PlayerAimPoint() : transform.position;
    }

    private void PlaceOnNavMesh()
    {
        if (agent == null || agent.isOnNavMesh)
        {
            return;
        }

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 50f, agent.areaMask))
        {
            if (Mathf.Abs(hit.position.y - transform.position.y) <= 20f)
            {
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning($"[EnemyTemplate] {name} descarto punto del navmesh demasiado alto (Y={hit.position.y:F1}).", this);
            }
        }
        else
        {
            Debug.LogWarning($"[EnemyTemplate] {name} no encontro navmesh cerca del spawn.", this);
        }
    }

    private bool TrySetDestination(Vector3 position)
    {
        return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.SetDestination(position);
    }

    private void SetStopped(bool stopped)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            return;
        }
        agent.isStopped = stopped;
    }

    private void SetCollidersEnabled(bool enabled)
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = enabled;
        }
    }

    private void FitColliderToMesh()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds worldBounds = ComputeHullBounds(renderers);

        hullBounds = worldBounds;
        hullRadius = Mathf.Max(worldBounds.extents.x, worldBounds.extents.z);

        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            return;
        }

        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = new Vector3(
            worldBounds.size.x / Mathf.Max(transform.lossyScale.x, 0.0001f),
            worldBounds.size.y / Mathf.Max(transform.lossyScale.y, 0.0001f),
            worldBounds.size.z / Mathf.Max(transform.lossyScale.z, 0.0001f));

        if (collider is BoxCollider box)
        {
            box.center = localCenter;
            box.size = localSize;
        }
        else if (collider is SphereCollider sphere)
        {
            sphere.center = localCenter;
            sphere.radius = Mathf.Max(localSize.x, Mathf.Max(localSize.y, localSize.z)) * 0.5f;
        }
    }

    private static Transform FindPlayer()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = FindFirstObjectByType<TankController>();
        }
        if (cachedPlayer != null)
        {
            return cachedPlayer.transform;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
    }
}