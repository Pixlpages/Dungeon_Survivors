using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    int currentPhaseWaveIndex = 0;
    int currentWaveSpawnCount = 0;

    public WaveData[] data;
    public Camera referenceCamera;

    [Tooltip("If there are more than this number of enemies, stop spawning any more, for performance :)")]
    [SerializeField] public int maximumEnemyCount; 
    float spawnTimer;
    float currentWaveDuration = 0f;
    public bool boostedByCurse = true;

    [Header("Debug")]
    public bool enableDetailedLogs = false;
    [SerializeField] public WaveData currentWave;
    [SerializeField] public WaveData nextQueuedWave;
    public int CurrentWaveSpawnCount => currentWaveSpawnCount;  // Shows in Inspector
    private bool hasClearedEnemies = false;

    public static SpawnManager instance;

    void Start()
    {
        if (instance)
            Debug.LogWarning("There is more than 1 spawn manager in the scene!");
        instance = this;
        Debug.Log("SpawnManager instance set to " + gameObject.name);
    }

    void Awake()
    {
        // NEW: Clear enemies immediately on SpawnManager load, before any Update spawns
        if (enableDetailedLogs) Debug.Log("[SpawnManager] Calling ClearAllEnemies in Awake.");
        ClearAllEnemies();
        hasClearedEnemies = true;  // Allow spawns after clear
    }

    void Update()
    {
        if (!hasClearedEnemies)
        {
            if (enableDetailedLogs) Debug.Log("[SpawnManager] Blocking spawns until clear completes.");
            return;
        }

        spawnTimer -= Time.deltaTime;
        currentWaveDuration += Time.deltaTime;

        if (spawnTimer <= 0)
        {
            if (HasWaveEnded())
            {
                if (enableDetailedLogs) Debug.Log($"[SpawnManager] Wave '{currentWave?.name}' ended. Spawn Count: {currentWaveSpawnCount}/{currentWave?.totalSpawns}, Duration: {currentWaveDuration:F1}/{currentWave?.duration:F1}");
                if (MDPManager.Instance != null)
                {
                    WaveData next = MDPManager.Instance.GetPendingWave();
                    if (next != null)
                    {
                        nextQueuedWave = next;
                        if (enableDetailedLogs) Debug.Log("Next Queued Wave updated to: " + nextQueuedWave.name);
                        currentWaveDuration = currentWaveSpawnCount = 0;
                        SpawnWave(next);
                        ActivateCooldown();
                        return;
                    }
                }

                // Phase-based fallback
                WaveData.GamePhase phase = GetCurrentPhase();
                List<WaveData> phaseWaves = GetWavesForPhase(phase);
                if (phaseWaves.Count == 0) 
                {
                    if (enableDetailedLogs) Debug.LogWarning("No waves available for phase " + phase + ". Stopping spawns.");
                    return;
                }

                currentPhaseWaveIndex = (currentPhaseWaveIndex + 1) % phaseWaves.Count;
                currentWave = phaseWaves[currentPhaseWaveIndex];
                if (enableDetailedLogs) Debug.Log("Fallback wave set to: " + currentWave.name + " (Phase: " + phase + ", Index: " + currentPhaseWaveIndex + ")");
                currentWaveDuration = currentWaveSpawnCount = 0;
                spawnTimer = 0f;  // Ensure immediate spawn check
                SpawnWave(currentWave);  // Spawn initial enemies for fallback waves
                ActivateCooldown();
                return;
            }

            if (!CanSpawn())
            {
                if (enableDetailedLogs) Debug.Log($"[SpawnManager] Cannot spawn in wave '{currentWave?.name}'. Checking reasons...");
                ActivateCooldown();
                return;
            }

            if (currentWave == null) return;

            GameObject[] spawns = currentWave.GetSpawns(EnemyStats.count);
            if (enableDetailedLogs) Debug.Log($"[SpawnManager] Attempting to spawn {spawns.Length} enemies in wave '{currentWave.name}'. Current count: {currentWaveSpawnCount}/{currentWave.totalSpawns}");
            foreach (GameObject prefab in spawns)
            {
                if (!CanSpawn()) 
                {
                    if (enableDetailedLogs) Debug.Log($"[SpawnManager] Spawn blocked mid-tick in wave '{currentWave.name}'. Current count: {currentWaveSpawnCount}");
                    break;
                }
                Instantiate(prefab, GeneratePosition(), Quaternion.identity);
                currentWaveSpawnCount++;
            }
            if (enableDetailedLogs) Debug.Log($"[SpawnManager] Spawned {currentWaveSpawnCount - (currentWaveSpawnCount - spawns.Length < 0 ? 0 : currentWaveSpawnCount - spawns.Length)} enemies. Total for wave '{currentWave.name}': {currentWaveSpawnCount}/{currentWave.totalSpawns}");

            ActivateCooldown();
        }
    }

    private WaveData.GamePhase GetCurrentPhase()
    {
        float elapsed = GameManager.Instance != null ? GameManager.Instance.GetElapsedTime() : 0f;
        if (elapsed < 150f) return WaveData.GamePhase.Early;
        else if (elapsed < 390f && elapsed > 150f) return WaveData.GamePhase.Mid;
        return WaveData.GamePhase.Late;
    }

    private List<WaveData> GetWavesForPhase(WaveData.GamePhase phase)
    {
        List<WaveData> result = new List<WaveData>();
        foreach (var wave in data)
        {
            if (wave.phase == phase)
                result.Add(wave);
        }
        return result;
    }

    public void ActivateCooldown()
    {
        float curseBoost = boostedByCurse ? GameManager.GetCumulativeCurse() : 1;
        if (currentWave != null)
            spawnTimer += currentWave.GetSpawnInterval() / curseBoost;
    }

    public bool CanSpawn()
    {
        if (HasExceededMaxEnemies()) 
        {
            if (enableDetailedLogs) Debug.Log($"[SpawnManager] Cannot spawn: Exceeded max enemies ({EnemyStats.count}/{maximumEnemyCount})");
            return false;
        }
        if (currentWave == null) 
        {
            if (enableDetailedLogs) Debug.Log("[SpawnManager] Cannot spawn: No current wave");
            return false;
        }
        if (currentWaveSpawnCount >= currentWave.totalSpawns) 
        {
            if (enableDetailedLogs) Debug.Log($"[SpawnManager] Cannot spawn: Reached total spawns ({currentWaveSpawnCount}/{currentWave.totalSpawns})");
            return false;
        }
        if (currentWaveDuration > currentWave.duration) 
        {
            if (enableDetailedLogs) Debug.Log($"[SpawnManager] Cannot spawn: Wave duration exceeded ({currentWaveDuration:F1}/{currentWave.duration:F1})");
            return false;
        }
        return true;
    }

    public static bool HasExceededMaxEnemies()
    {
        if (!instance) return false;
        return EnemyStats.count > instance.maximumEnemyCount;
    }

    public bool HasWaveEnded()
    {
        if (currentWave == null) return true;

        bool durationMet = true;
        if ((currentWave.exitConditions & WaveData.ExitCondition.waveDuration) > 0)
        {
            durationMet = currentWaveDuration >= currentWave.duration;
            if (!durationMet) 
                if (enableDetailedLogs) Debug.Log($"[SpawnManager] Wave '{currentWave.name}' not ended: Duration not met ({currentWaveDuration:F1}/{currentWave.duration:F1})");
        }

        bool spawnsMet = true;
        if ((currentWave.exitConditions & WaveData.ExitCondition.reachedTotalSpawns) > 0)
        {
            spawnsMet = currentWaveSpawnCount >= currentWave.totalSpawns;
            if (!spawnsMet) 
                if (enableDetailedLogs) Debug.Log($"[SpawnManager] Wave '{currentWave.name}' not ended: Spawns not met ({currentWaveSpawnCount}/{currentWave.totalSpawns})");
        }

        bool killAllMet = true;
        if (currentWave.mustKillAll)
        {
            killAllMet = EnemyStats.count == 0;
            if (!killAllMet) 
                if (enableDetailedLogs) Debug.Log($"[SpawnManager] Wave '{currentWave.name}' not ended: Must kill all, but {EnemyStats.count} enemies remain");
        }

        return durationMet && spawnsMet && killAllMet;
    }

    void Reset()
    {
        referenceCamera = Camera.main;
    }

    public static Vector3 GeneratePosition()
    {
        if (!instance.referenceCamera)
            instance.referenceCamera = Camera.main;

        if (!instance.referenceCamera.orthographic)
            Debug.LogWarning("Camera may not be orthographic! enemy may appear within bounds");

        float x = Random.Range(0f, 1f), y = Random.Range(0f, 1f);
        switch (Random.Range(0, 2))
        {
            default: return instance.referenceCamera.ViewportToWorldPoint(new Vector3(Mathf.Round(x), y));
            case 1:  return instance.referenceCamera.ViewportToWorldPoint(new Vector3(x, Mathf.Round(y)));
        }
    }

    public static bool IsWithinBoundaries(Transform checkedObject)
    {
        Camera c = instance && instance.referenceCamera ? instance.referenceCamera : Camera.main;
        Vector2 viewport = c.WorldToViewportPoint(checkedObject.position);
        return !(viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f);
    }

    public void SpawnWave(WaveData wave)
    {
        if (wave == null) return;

        currentWave = wave;
        if (enableDetailedLogs) Debug.Log("Current Wave updated to: " + currentWave.name + " (Total Spawns: " + currentWave.totalSpawns + ", Duration: " + currentWave.duration + ")");

        GameObject[] spawns = wave.GetSpawns(EnemyStats.count);
        int actualSpawned = 0;  // Track actual spawns
        if (enableDetailedLogs) Debug.Log($"[SpawnManager] Initial spawn for wave '{currentWave.name}': Attempting {spawns.Length} enemies");
        foreach (GameObject prefab in spawns)
        {
            if (!CanSpawn()) 
            {
                if (enableDetailedLogs) Debug.Log($"[SpawnManager] Initial spawn blocked for wave '{currentWave.name}'. Current count: {currentWaveSpawnCount}");
                break;
            }
            Instantiate(prefab, GeneratePosition(), Quaternion.identity);
            currentWaveSpawnCount++;
            actualSpawned++;
        }
        if (enableDetailedLogs) Debug.Log($"[SpawnManager] Initial spawn complete for wave '{currentWave.name}': {actualSpawned} enemies actually spawned. Total: {currentWaveSpawnCount}/{currentWave.totalSpawns}");
    }

    //Method to clear all enemies on restart (call from GameManager on death/restart)
    public void ClearAllEnemies()
    {
        EnemyStats[] enemies = FindObjectsOfType<EnemyStats>();
        if (enableDetailedLogs) Debug.Log($"[SpawnManager] Attempting to clear enemies. Found {enemies.Length} EnemyStats objects.");
        foreach (EnemyStats enemy in enemies)
        {
            if (enableDetailedLogs) Debug.Log($"[SpawnManager] Destroying enemy: {enemy.gameObject.name}");
            Destroy(enemy.gameObject);
        }
        if (enableDetailedLogs) Debug.Log($"[SpawnManager] Cleared {enemies.Length} enemies. EnemyStats.count should now be 0.");
    }
}
