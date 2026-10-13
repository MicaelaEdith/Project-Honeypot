using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    [SerializeField] private EnemyPool pool;
    [SerializeField] private TankController player;
    [SerializeField] private Transform labDoor;
    [SerializeField] private float winRadius = 5f;
    [SerializeField] private float victoryDelay = 2f;
    [SerializeField] private string endingScene = "Ending";
    [SerializeField] private AudioClip victoryClip;
    [SerializeField] private int startingIndex = 1;
    [SerializeField] private float startDelay = 1f;
    [SerializeField] private float defaultPostWavePause = 2f;

    private WaveCheckpoint[] checkpoints;
    private readonly HashSet<int> triggeredIndexes = new HashSet<int>();
    private readonly List<EnemyTemplate> activeEnemies = new List<EnemyTemplate>();
    private WaveDefinition currentWave;
    private Coroutine spawnRoutine;
    private int currentIndex;
    private bool waveActive;
    private bool spawnComplete;
    private bool finished;
    private bool pendingAdvance;
    private float pauseTimer;
    private int totalEnemies;
    private int remainingEnemies;

    public event System.Action<int> OnEnemiesRemainingChanged;
    public event System.Action OnVictory;

    public int CurrentIndex => currentIndex;
    public int ActiveEnemyCount => activeEnemies.Count;
    public bool IsFinished => finished;

    public int TotalEnemies => totalEnemies;

    public int RemainingEnemies => remainingEnemies;

    private void Awake()
    {
        if (pool == null)
        {
            pool = FindFirstObjectByType<EnemyPool>();
        }
        if (pool == null)
        {
            Debug.LogWarning("[GameManager] No se encontro EnemyPool en la escena: las oleadas no van a spawnear nada.", this);
        }

        if (player == null)
        {
            player = FindFirstObjectByType<TankController>();
        }
        if (player == null)
        {
            Debug.LogWarning("[GameManager] No se encontro TankController (el Player) en la escena.", this);
        }
        else
        {
            player.OnRespawnLimitReached -= HandleRespawnLimitReached;
            player.OnRespawnLimitReached += HandleRespawnLimitReached;
        }

        if (labDoor == null)
        {
            GameObject found = GameObject.Find("LabDoor");
            if (found != null)
            {
                labDoor = found.transform;
            }
        }
        if (labDoor == null)
        {
            Debug.LogWarning("[GameManager] No se encontro LabDoor: no se puede ganar.", this);
        }
    }

    private void Start()
    {
        checkpoints = FindObjectsByType<WaveCheckpoint>(FindObjectsSortMode.None);
        currentIndex = startingIndex;
        pauseTimer = startDelay;
        pendingAdvance = false;

        Debug.Log($"[GameManager] {checkpoints.Length} checkpoints detectados. Arranca en indice {currentIndex}.", this);

        foreach (WaveCheckpoint checkpoint in checkpoints)
        {
            if (checkpoint == null)
            {
                continue;
            }

            if (checkpoint.Wave == null && !checkpoint.IsVictory)
            {
                Debug.LogWarning(
                    $"[GameManager] {checkpoint.name} (indice {checkpoint.Index}{checkpoint.Label}) " +
                    $"no tiene WaveDefinition ni isVictory: no va a hacer nada.",
                    checkpoint);
            }
            else
            {
                Debug.Log(
                    $"[GameManager] {checkpoint.name} indice {checkpoint.Index}{checkpoint.Label} " +
                    $"wave={(checkpoint.Wave != null ? checkpoint.Wave.name : "VICTORIA")} " +
                    $"spawns={checkpoint.SpawnPoints.Count}.",
                    checkpoint);
            }
        }

        totalEnemies = CountTotalEnemies();
        remainingEnemies = totalEnemies;
        Debug.Log($"[GameManager] Enemigos totales de la partida: {totalEnemies}.", this);
        OnEnemiesRemainingChanged?.Invoke(remainingEnemies);
    }

    private int CountTotalEnemies()
    {
        int total = 0;
        HashSet<int> counted = new HashSet<int>();

        foreach (WaveCheckpoint checkpoint in checkpoints)
        {
            if (checkpoint == null || checkpoint.IsVictory || checkpoint.Wave == null)
            {
                continue;
            }

            if (!counted.Add(checkpoint.Index))
            {
                continue;
            }

            total += checkpoint.Wave.TotalCount;
        }

        return total;
    }

    private void OnDestroy()
    {
        foreach (EnemyTemplate enemy in activeEnemies)
        {
            if (enemy != null)
            {
                enemy.Died -= OnEnemyDied;
            }
        }
        activeEnemies.Clear();

        if (player != null)
        {
            player.OnRespawnLimitReached -= HandleRespawnLimitReached;
        }
    }

    private void Update()
    {
        if (finished)
        {
            return;
        }

        CheckVictory();

        if (finished || waveActive || player == null)
        {
            return;
        }

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            if (pauseTimer > 0f)
            {
                return;
            }

            if (pendingAdvance)
            {
                pendingAdvance = false;
                currentIndex++;
                Debug.Log($"[GameManager] Oleada limpia. Se arma el indice {currentIndex}.", this);
            }
        }

        WaveCheckpoint checkpoint = FindArmedCheckpoint();
        if (checkpoint != null)
        {
            FireWave(checkpoint);
        }
    }

    private void CheckVictory()
    {
        if (finished || totalEnemies <= 0 || remainingEnemies > 0 || labDoor == null || player == null)
        {
            return;
        }

        if ((player.transform.position - labDoor.position).sqrMagnitude <= winRadius * winRadius)
        {
            Victory();
        }
    }

    private WaveCheckpoint FindArmedCheckpoint()
    {
        if (player == null)
        {
            return null;
        }

        Vector3 position = player.transform.position;
        WaveCheckpoint best = null;
        float bestSqr = float.MaxValue;

        foreach (WaveCheckpoint checkpoint in checkpoints)
        {
            if (checkpoint == null || checkpoint.Index != currentIndex)
            {
                continue;
            }

            if (triggeredIndexes.Contains(checkpoint.Index))
            {
                continue;
            }

            if (!checkpoint.Contains(position))
            {
                continue;
            }

            float sqr = (position - checkpoint.transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = checkpoint;
            }
        }

        return best;
    }

    private void FireWave(WaveCheckpoint checkpoint)
    {
        triggeredIndexes.Add(checkpoint.Index);
        Debug.Log($"[GameManager] Oleada {checkpoint.Index}{checkpoint.Label} disparada en {checkpoint.name}.", this);

        if (checkpoint.IsVictory)
        {
            Debug.Log($"[GameManager] {checkpoint.name} es un checkpoint de victoria: la victoria la maneja LabDoor.", this);
            return;
        }

        currentWave = checkpoint.Wave;

        if (currentWave == null || currentWave.TotalCount == 0)
        {
            Debug.LogWarning($"[GameManager] El checkpoint {checkpoint.name} no tiene oleada. Se saltea.", this);
            ScheduleAdvance(0f);
            return;
        }

        waveActive = true;
        spawnComplete = false;
        spawnRoutine = StartCoroutine(SpawnWave(checkpoint));
    }

    private IEnumerator SpawnWave(WaveCheckpoint checkpoint)
    {
        WaveDefinition wave = checkpoint.Wave;
        int index = 0;

        foreach (WaveDefinition.Entry entry in wave.Entries)
        {
            if (entry == null || entry.enemyPrefab == null)
            {
                continue;
            }

            for (int i = 0; i < entry.count; i++)
            {
                checkpoint.GetSpawn(index, out Vector3 position, out Quaternion rotation);

                EnemyTemplate enemy = pool != null
                    ? pool.Spawn(entry.enemyPrefab, position, rotation)
                    : null;

                if (enemy != null)
                {
                    enemy.Died += OnEnemyDied;
                    activeEnemies.Add(enemy);
                }

                index++;

                if (wave.SpawnInterval > 0f)
                {
                    yield return new WaitForSeconds(wave.SpawnInterval);
                }
            }
        }

        spawnRoutine = null;
        spawnComplete = true;

        if (activeEnemies.Count == 0)
        {
            WaveCleared();
        }
    }

    private void OnEnemyDied(EnemyTemplate enemy)
    {
        if (enemy != null)
        {
            enemy.Died -= OnEnemyDied;
        }
        activeEnemies.Remove(enemy);

        remainingEnemies = Mathf.Max(remainingEnemies - 1, 0);
        OnEnemiesRemainingChanged?.Invoke(remainingEnemies);

        if (waveActive && spawnComplete && activeEnemies.Count == 0)
        {
            WaveCleared();
        }
    }

    private void WaveCleared()
    {
        waveActive = false;
        spawnComplete = false;

        float pause = currentWave != null ? currentWave.PostWavePause : defaultPostWavePause;
        Debug.Log($"[GameManager] Oleada {currentIndex} limpia. Pausa de {pause:F1}s.", this);
        ScheduleAdvance(pause);
    }

    private void ScheduleAdvance(float pause)
    {
        if (pause > 0f)
        {
            pauseTimer = pause;
            pendingAdvance = true;
        }
        else
        {
            pendingAdvance = false;
            currentIndex++;
        }
    }

    private void Victory()
    {
        if (finished)
        {
            return;
        }

        finished = true;
        Debug.Log("[GameManager] VICTORIA: el player llego al LabDoor sin enemigos.", this);
        OnVictory?.Invoke();
        StartCoroutine(VictoryRoutine());
    }

    private IEnumerator VictoryRoutine()
    {
        if (victoryClip != null)
        {
            AudioSource.PlayClipAtPoint(victoryClip, player != null ? player.transform.position : transform.position);
        }

        if (victoryDelay > 0f)
        {
            yield return new WaitForSeconds(victoryDelay);
        }

        SceneManager.LoadScene(endingScene);
    }

    private void HandleRespawnLimitReached()
    {
        Defeat();
    }

    public void Defeat()
    {
        if (finished)
        {
            return;
        }

        finished = true;
        Debug.Log("[GameManager] DERROTA: el player agoto los respawns.", this);
        SceneManager.LoadScene(endingScene);
    }
}
