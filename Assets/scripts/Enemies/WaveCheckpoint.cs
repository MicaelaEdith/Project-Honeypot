using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WaveCheckpoint : MonoBehaviour
{
    [SerializeField] private int index = 1;
    [SerializeField] private string label = "a";
    [SerializeField] private float triggerRadius = 40f;
    [SerializeField] private WaveDefinition wave;
    [SerializeField] private List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

    [SerializeField] private bool isVictory = false;

    public int Index => index;
    public string Label => label;
    public WaveDefinition Wave => wave;
    public IReadOnlyList<SpawnPoint> SpawnPoints => spawnPoints;
    public bool IsVictory => isVictory;
    public float TriggerRadius => triggerRadius;

    public bool Contains(Vector3 position)
    {
        return (position - transform.position).sqrMagnitude <= triggerRadius * triggerRadius;
    }


    public void GetSpawn(int i, out Vector3 position, out Quaternion rotation)
    {
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            SpawnPoint point = spawnPoints[Mathf.Abs(i) % spawnPoints.Count];
            if (point != null)
            {
                position = point.Position;
                rotation = point.Rotation;
                return;
            }
        }

        position = transform.position;
        rotation = transform.rotation;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isVictory
            ? new Color(0.2f, 1f, 0.2f, 0.35f)
            : new Color(1f, 0.8f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}
