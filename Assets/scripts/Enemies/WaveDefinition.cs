using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composicion de una oleada: cuantos enemigos de cada prefab salen. Es un asset
/// (CreateAssetMenu) para editarlo en el Inspector sin tocar la escena y para que
/// todas las variantes del mismo indice (1a, 1b, 1c...) referencien el MISMO asset:
/// la oleada N es identica sin importar por que rama llego el player.
///
/// No guarda donde aparecen: eso es responsabilidad de cada WaveCheckpoint, que
/// referencia sus propios SpawnPoint. Asi la misma composicion sirve para ramas
/// con geometria distinta.
/// </summary>
[CreateAssetMenu(fileName = "Wave", menuName = "Honeypot/Wave Definition")]
public class WaveDefinition : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Prefab del enemigo (nave00 = TankShip, nave01 = Shooter, nave02 = Melee).")]
        public GameObject enemyPrefab;

        [Min(0)]
        [Tooltip("Cuantos de este prefab salen en la oleada.")]
        public int count = 1;
    }

    [Tooltip("Enemigos que componen la oleada.")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    [Tooltip("Segundos entre enemigos consecutivos (stagger para que no aparezcan todos en el mismo frame).")]
    [SerializeField] private float spawnInterval = 0.2f;

    [Tooltip("Pausa tras limpiar la oleada antes de habilitar el siguiente indice.")]
    [SerializeField] private float postWavePause = 2f;

    public IReadOnlyList<Entry> Entries => entries;
    public float SpawnInterval => Mathf.Max(spawnInterval, 0f);
    public float PostWavePause => Mathf.Max(postWavePause, 0f);

    public int TotalCount
    {
        get
        {
            int total = 0;
            foreach (Entry entry in entries)
            {
                if (entry != null && entry.enemyPrefab != null)
                {
                    total += Mathf.Max(entry.count, 0);
                }
            }
            return total;
        }
    }
}
