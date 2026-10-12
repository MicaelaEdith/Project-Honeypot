using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool de enemigos por prefab. Pre-calienta una cantidad de instancias por prefab
/// al arrancar y las reutiliza: cuando un enemigo termina su secuencia de muerte,
/// EnemyTemplate avisa aca y la instancia vuelve a la cola en vez de destruirse.
///
/// El maximo a pre-calentar por prefab es el maximo concurrente de la oleada mas
/// poblada (TankShip 4 / Shooter 5 / Melee 5 en la tabla de oleadas). Todo editable
/// por Inspector: si una oleada pide mas de lo pre-calentado, Spawn instancia una
/// extra y la agrega al pool.
///
/// No se suscribe al evento Died: EnemyTemplate llama Reclaim() directo al cerrar
/// la muerte. Died queda libre para que el GameManager cuente bajas sin acoplarse.
/// </summary>
[DisallowMultipleComponent]
public class EnemyPool : MonoBehaviour
{
    [Serializable]
    public class PoolConfig
    {
        [Tooltip("Prefab del enemigo (nave00, nave01, nave02).")]
        public GameObject prefab;

        [Min(0)]
        [Tooltip("Cuantas instancias dejar listas al arrancar.")]
        public int prewarm = 4;
    }

    [Header("Pools")]
    [Tooltip("Un elemento por prefab de enemigo. Prewarm = maximo concurrente esperado.")]
    [SerializeField] private List<PoolConfig> pools = new List<PoolConfig>();

    [Header("Jerarquia")]
    [Tooltip("Padre de las instancias inactivas. Vacio = este mismo GameObject.")]
    [SerializeField] private Transform poolRoot;

    private readonly Dictionary<GameObject, Queue<EnemyTemplate>> available = new Dictionary<GameObject, Queue<EnemyTemplate>>();
    private readonly Dictionary<EnemyTemplate, GameObject> sourceOf = new Dictionary<EnemyTemplate, GameObject>();

    private void Awake()
    {
        if (poolRoot == null)
        {
            poolRoot = transform;
        }
        Prewarm();
    }

    private void Prewarm()
    {
        foreach (PoolConfig config in pools)
        {
            if (config == null || config.prefab == null)
            {
                continue;
            }

            if (!available.TryGetValue(config.prefab, out Queue<EnemyTemplate> queue))
            {
                queue = new Queue<EnemyTemplate>();
                available[config.prefab] = queue;
            }

            for (int i = 0; i < config.prewarm; i++)
            {
                EnemyTemplate instance = CreateInstance(config.prefab);
                if (instance != null)
                {
                    queue.Enqueue(instance);
                }
            }
        }
    }

    private EnemyTemplate CreateInstance(GameObject prefab)
    {
        GameObject go = Instantiate(prefab, poolRoot);
        go.name = prefab.name + " (pooled)";

        EnemyTemplate template = go.GetComponent<EnemyTemplate>();
        if (template == null)
        {
            Debug.LogError($"[EnemyPool] El prefab {prefab.name} no tiene un EnemyTemplate. Se ignora.", this);
            Destroy(go);
            return null;
        }

        template.AttachPool(this);
        sourceOf[template] = prefab;
        go.SetActive(false);
        return template;
    }

    /// <summary>
    /// Saca una instancia (de la cola o nueva si la cola esta vacia) y la deja lista
    /// en la posicion pedida. Devuelve null si el prefab es invalido.
    /// </summary>
    public EnemyTemplate Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("[EnemyPool] Spawn con prefab nulo.", this);
            return null;
        }

        if (!available.TryGetValue(prefab, out Queue<EnemyTemplate> queue))
        {
            queue = new Queue<EnemyTemplate>();
            available[prefab] = queue;
        }

        EnemyTemplate template = null;
        while (queue.Count > 0 && template == null)
        {
            template = queue.Dequeue();
        }

        if (template == null)
        {
            template = CreateInstance(prefab);
        }

        if (template == null)
        {
            return null;
        }

        template.ResetForSpawn(position, rotation);
        return template;
    }

    /// <summary>
    /// Devuelve una instancia al pool. La llama EnemyTemplate al terminar la muerte.
    /// </summary>
    public void Reclaim(EnemyTemplate template)
    {
        if (template == null)
        {
            return;
        }

        template.gameObject.SetActive(false);

        if (sourceOf.TryGetValue(template, out GameObject prefab) && prefab != null)
        {
            if (!available.TryGetValue(prefab, out Queue<EnemyTemplate> queue))
            {
                queue = new Queue<EnemyTemplate>();
                available[prefab] = queue;
            }
            queue.Enqueue(template);
        }
        else
        {
            Destroy(template.gameObject);
        }
    }
}
