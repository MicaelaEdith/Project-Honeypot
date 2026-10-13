using UnityEngine;

[DisallowMultipleComponent]
public class HudController : MonoBehaviour
{
    [SerializeField] private TankController tank;

    [SerializeField] private HoneyPotActivator honeypotActivator;

    [SerializeField] private HealthBarView healthBar;
    [SerializeField] private BeeStatusView bee;
    [SerializeField] private BombCounterView bombs;
    [SerializeField] private EnemyCounterView enemies;
    [SerializeField] private RespawnCounterView respawns;

    [SerializeField] private GameManager gameManager;

    private void Awake()
    {
        ResolveTank();
        ResolveHoneypotActivator();
        ResolveGameManager();
        ResolveEnemiesView();
        ResolveRespawnsView();
    }

    private void OnEnable()
    {
        ResolveTank();
        ResolveHoneypotActivator();
        ResolveGameManager();
        ResolveEnemiesView();
        ResolveRespawnsView();

        if (tank != null)
        {
            tank.OnHealthChanged -= HandleHealth;
            tank.OnHealthChanged += HandleHealth;
            tank.OnRespawnsChanged -= HandleRespawns;
            tank.OnRespawnsChanged += HandleRespawns;
        }

        if (honeypotActivator != null)
        {
            honeypotActivator.OnHoneypotCountChanged -= HandleHoneypotCount;
            honeypotActivator.OnHoneypotCountChanged += HandleHoneypotCount;
        }

        if (gameManager != null)
        {
            gameManager.OnEnemiesRemainingChanged -= HandleEnemiesRemaining;
            gameManager.OnEnemiesRemainingChanged += HandleEnemiesRemaining;
        }

        Debug.Log($"[HudController] tank={(tank != null ? tank.name : "NULL")} " +
                  $"healthBar={(healthBar != null)} bee={(bee != null)} bombs={(bombs != null)} " +
                  $"enemies={(enemies != null)} respawns={(respawns != null)} gameManager={(gameManager != null)}", this);
    }

    private void OnDisable()
    {
        if (tank != null)
        {
            tank.OnHealthChanged -= HandleHealth;
            tank.OnRespawnsChanged -= HandleRespawns;
        }

        if (honeypotActivator != null)
        {
            honeypotActivator.OnHoneypotCountChanged -= HandleHoneypotCount;
        }

        if (gameManager != null)
        {
            gameManager.OnEnemiesRemainingChanged -= HandleEnemiesRemaining;
        }
    }

    private void Start()
    {
        ResolveTank();
        ResolveHoneypotActivator();
        ResolveGameManager();
        ResolveEnemiesView();
        ResolveRespawnsView();
        PushCurrentHealth();
        PushCurrentHoneypotCount();
        PushCurrentEnemies();
        PushCurrentRespawns();
    }

    private void ResolveTank()
    {
        if (tank == null)
        {
            tank = FindFirstObjectByType<TankController>();
        }
    }

    private void ResolveHoneypotActivator()
    {
        if (honeypotActivator == null)
        {
            honeypotActivator = FindFirstObjectByType<HoneyPotActivator>();
        }
    }

    private void ResolveGameManager()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }
    }

    private void ResolveEnemiesView()
    {
        if (enemies == null)
        {
            enemies = FindFirstObjectByType<EnemyCounterView>();
        }
    }

    private void ResolveRespawnsView()
    {
        if (respawns == null)
        {
            respawns = FindFirstObjectByType<RespawnCounterView>();
        }
    }

    private void PushCurrentHealth()
    {
        if (tank != null)
        {
            HandleHealth(tank.CurrentHealth, tank.MaxHealth);
        }
    }

    private void PushCurrentHoneypotCount()
    {
        if (honeypotActivator != null)
        {
            HandleHoneypotCount(honeypotActivator.RemainingHoneypots);
        }
    }

    private void HandleHoneypotCount(int count)
    {
        if (bombs == null)
        {
            Debug.LogError("[HudController] bombs sin asignar: el contador no se va a actualizar.", this);
        }
        else
        {
            bombs.SetCount(count);
        }

        Debug.Log($"[HudController] honeypots restantes {count}", this);
    }

    private void PushCurrentEnemies()
    {
        if (gameManager != null)
        {
            HandleEnemiesRemaining(gameManager.RemainingEnemies);
        }
    }

    private void HandleEnemiesRemaining(int count)
    {
        if (enemies == null)
        {
            Debug.LogError("[HudController] enemies sin asignar: el contador de enemigos no se va a actualizar.", this);
        }
        else
        {
            enemies.SetCount(count);
        }

        Debug.Log($"[HudController] enemigos restantes {count}", this);
    }

    private void PushCurrentRespawns()
    {
        if (tank != null)
        {
            HandleRespawns(tank.RespawnsRemaining);
        }
    }

    private void HandleRespawns(int count)
    {
        if (respawns == null)
        {
            Debug.LogError("[HudController] respawns sin asignar: el contador de respawns no se va a actualizar.", this);
        }
        else
        {
            respawns.SetCount(count);
        }

        Debug.Log($"[HudController] respawns restantes {count}", this);
    }

    private void HandleHealth(float current, float max)
    {
        if (healthBar == null)
        {
            Debug.LogError("[HudController] healthBar sin asignar: la barra no se va a mover.", this);
        }
        else
        {
            healthBar.SetHealth(current, max);
        }

        if (bee != null)
        {
            bee.SetHealth(current, max);
        }

        Debug.Log($"[HudController] vida {current}/{max}", this);
    }
}