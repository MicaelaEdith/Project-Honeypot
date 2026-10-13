using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Wave", menuName = "Honeypot/Wave Definition")]
public class WaveDefinition : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public GameObject enemyPrefab;

        public int count = 1;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();
    [SerializeField] private float spawnInterval = 0.2f;
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
