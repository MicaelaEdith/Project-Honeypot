using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto del recorrido que dispara la oleada de su indice. El GameManager tiene
/// armado un solo indice a la vez: cuando el player entra en triggerRadius de
/// CUALQUIER checkpoint con ese indice, la oleada sale y ningun otro checkpoint
/// del mismo indice puede volver a dispararse (asi una rama no repetida no dispara
/// dos veces la misma oleada).
///
/// Convencion de nombres: index = numero de oleada (1, 2, 3...) y label = letra de
/// la variante/bifurcacion (1a, 1b, 1c). El label es solo para leer el Inspector;
/// el GameManager agrupa por index.
///
/// No tiene Update: el GameManager polea Contains() para que haya un unico dueno
/// del estado de la partida.
/// </summary>
[DisallowMultipleComponent]
public class WaveCheckpoint : MonoBehaviour
{
    [Tooltip("Numero de oleada que dispara. Todas las variantes con el mismo index comparten la misma oleada.")]
    [Min(0)]
    [SerializeField] private int index = 1;

    [Tooltip("Etiqueta de la variante para el Inspector (por ejemplo a, b, c). No afecta la logica.")]
    [SerializeField] private string label = "a";

    [Tooltip("Radio en unidades de mundo dentro del cual el player dispara la oleada.")]
    [SerializeField] private float triggerRadius = 40f;

    [Tooltip("Composicion de la oleada. Vacio = este checkpoint no spawnea enemigos (por ejemplo el de victoria).")]
    [SerializeField] private WaveDefinition wave;

    [Tooltip("Puntos donde nacen los enemigos de esta rama. Vacio = usa la posicion de este checkpoint.")]
    [SerializeField] private List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

    [Tooltip("Si esta activo, llegar a este checkpoint gana la partida (el Laboratorio).")]
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

    /// <summary>
    /// Punto de spawn de la posicion i (round-robin). Si no hay SpawnPoint asignado
    /// cae al propio checkpoint para que una oleada mal cableada igual salga.
    /// </summary>
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
