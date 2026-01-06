using System.Collections.Generic;
using UnityEngine;

public class VFXPool : MonoBehaviour
{
    public static VFXPool Instance;

    [System.Serializable]
    public class VFXEntry
    {
        public string key;           // Identifier name for easy access
        public GameObject prefab;    // The prefab to pool
        public int initialSize = 10; // How many to preload
    }

    [Header("VFX Prefabs to Pool")]
    public List<VFXEntry> vfxPrefabs = new List<VFXEntry>();

    private Dictionary<string, Queue<GameObject>> poolDictionary = new Dictionary<string, Queue<GameObject>>();
    private Dictionary<string, GameObject> prefabLookup = new Dictionary<string, GameObject>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePools();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializePools()
    {
        foreach (var entry in vfxPrefabs)
        {
            if (entry.prefab == null || string.IsNullOrEmpty(entry.key))
            {
                Debug.LogWarning($"VFXPool: Skipping invalid entry '{entry.key}'");
                continue;
            }

            if (!poolDictionary.ContainsKey(entry.key))
            {
                poolDictionary[entry.key] = new Queue<GameObject>();
                prefabLookup[entry.key] = entry.prefab;

                for (int i = 0; i < entry.initialSize; i++)
                {
                    GameObject obj = Instantiate(entry.prefab, transform);
                    obj.SetActive(false);
                    poolDictionary[entry.key].Enqueue(obj);
                }
            }
        }

        Debug.Log($"VFXPool initialized with {vfxPrefabs.Count} prefabs.");
    }

    public GameObject Get(string key, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(key))
        {
            Debug.LogWarning($"VFXPool: No pool found for key '{key}'");
            return null;
        }

        GameObject obj;
        if (poolDictionary[key].Count > 0)
        {
            obj = poolDictionary[key].Dequeue();
        }
        else
        {
            obj = Instantiate(prefabLookup[key], transform);
        }

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);

        // Optional: auto-return after effect finishes
        var ps = obj.GetComponent<ParticleSystem>();
        if (ps != null)
            Instance.StartCoroutine(ReturnWhenDone(key, obj, ps.main.duration));

        return obj;
    }

    public void Return(string key, GameObject obj)
    {
        obj.SetActive(false);
        poolDictionary[key].Enqueue(obj);
    }

    private System.Collections.IEnumerator ReturnWhenDone(string key, GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null && Instance != null)
            Return(key, obj);
    }
}
