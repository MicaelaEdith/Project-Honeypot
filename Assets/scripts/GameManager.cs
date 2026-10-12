using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquesta las oleadas. Mantiene un solo indice armado a la vez: cuando el player
/// entra en el radio de CUALQUIER WaveCheckpoint de ese indice, la oleada sale y
/// ningun otro checkpoint del mismo indice vuelve a dispararse (por eso sirve para
/// un nivel con bifurcaciones: 1a, 1b o 1c disparan la misma oleada 1, el primero
/// que toca el player). Al limpiar la oleada, tras una pausa, se habilita el indice
/// siguiente. El checkpoint marcado isVictory gana la partida.
///
/// Descubre los checkpoints solo (FindObjectsByType) y los agrupa por index; no hay
/// que arrastrar nada a mano. Se suscribe al evento Died de cada enemigo, que sale
/// al terminar la secuencia de muerte (campo limpio antes de avanzar).
/// </summary>
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Vacio = se busca el primero de la escena.")]
    [SerializeField] private EnemyPool pool;
    [Tooltip("Vacio = se busca el TankController de la escena.")]
    [SerializeField] private TankController player;

    [Header("Progresion")]
    [Tooltip("Indice de la primera oleada.")]
    [Min(0)]
    [SerializeField] private int startingIndex = 1;
    [Tooltip("Demora inicial antes de armar la primera oleada (para que el player se ubique).")]
    [Min(0)]
    [SerializeField] private float startDelay = 1f;
    [Tooltip("Pausa por defecto tras limpiar una oleada si su WaveDefinition no define una.")]
    [Min(0)]
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

    public int CurrentIndex => currentIndex;
    public int ActiveEnemyCount => activeEnemies.Count;
    public bool IsFinished => finished;

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
    }

    private void Update()
    {
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
            Victory();
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

        // Si no se pudo spawnear ninguno no hay evento Died que espere: se cierra
        // la oleada igual para no dejar la progresion trabada.
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

        // spawnComplete evita cerrar la oleada a mitad del stagger: si un enemigo
        // ya spawneado muere rapido, no cuenta como "campo limpio" hasta que el
        // ultimo de la oleada haya nacido.
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
        finished = true;
        Debug.Log("[GameManager] VICTORIA: el player llego al Laboratorio.", this);
    }

    /// <summary>
    /// Derrota por ahora solo marca la partida como terminada. La pantalla final y
    /// el respawn real de la Etapa D se cuelgan de aca.
    /// </summary>
    public void Defeat()
    {
        finished = true;
        Debug.Log("[GameManager] DERROTA.", this);
    }
}
