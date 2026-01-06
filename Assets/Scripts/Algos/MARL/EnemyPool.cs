using System.Collections.Generic;
using UnityEngine;

public class EnemyPool : MonoBehaviour
{
    public static EnemyPool Instance;

    [System.Serializable]
    public class PoolInfo
    {
        public GameObject prefab;
        public int preloadCount = 20;
    }

    [Header("Known Enemy Types")]
    public List<PoolInfo> knownEnemies = new List<PoolInfo>();

    private readonly Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
    private readonly Dictionary<string, GameObject> prefabLookup = new Dictionary<string, GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);
        InitializePools();
    }

    void InitializePools()
    {
        foreach (var info in knownEnemies)
        {
            if (info.prefab == null) continue;

            string key = info.prefab.name;
            prefabLookup[key] = info.prefab;
            pools[key] = new Queue<GameObject>();

            for (int i = 0; i < info.preloadCount; i++)
            {
                GameObject enemy = Instantiate(info.prefab);
                enemy.name = key;
                enemy.SetActive(false);
                pools[key].Enqueue(enemy);
            }
        }
    }

    // This is only used if SpawnManager instantiates directly.
    // We "intercept" by checking if we can serve from pool.
    public GameObject TryGetFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        string key = prefab.name;
        if (pools.TryGetValue(key, out var queue) && queue.Count > 0)
        {
            GameObject pooled = queue.Dequeue();
            pooled.transform.SetPositionAndRotation(position, rotation);
            pooled.SetActive(true);
            pooled.GetComponent<EnemyStats>()?.ResetStats();
            pooled.GetComponent<MARLAgent>()?.ResetAgent();
            return pooled;
        }

        // if no pooled object exists, spawn a new one and track it
        GameObject newEnemy = Instantiate(prefab, position, rotation);
        newEnemy.name = key;
        prefabLookup[key] = prefab;
        return newEnemy;
    }

    public void DespawnEnemy(GameObject enemy)
    {
        string key = enemy.name;
        if (!pools.ContainsKey(key))
        {
            pools[key] = new Queue<GameObject>();
        }
        Debug.Log("Despawning enemy: " + key);
        enemy.SetActive(false);
        pools[key].Enqueue(enemy);
    }
}
