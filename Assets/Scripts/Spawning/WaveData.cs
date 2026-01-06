using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Wave Data", menuName = "NewScriptables/Wave Data")]
public class WaveData : SpawnData
{
    public enum GamePhase { Early, Mid, Late }
    public enum Difficulty { Easy, Medium, Hard }

    [Header("Wave Classification")]
    public GamePhase phase;
    public Difficulty difficulty;

    [Header("Wave Data")]
    [Min(0)] public int startingCount = 0;
    [Min(1)] public uint totalSpawns = uint.MaxValue;

    [System.Flags]
    public enum ExitCondition { waveDuration = 1, reachedTotalSpawns = 2 }

    public ExitCondition exitConditions = ExitCondition.waveDuration;
    public bool mustKillAll = false;

    [HideInInspector] public uint spawnCount;

    [System.Serializable]
    public struct EnemySpawnEntry
    {
        public GameObject prefab;
        [Range(0f, 100f)] public float spawnChance;
    }

    [Header("Enemy Spawn Settings")]
    public EnemySpawnEntry[] enemyEntries;

    void OnValidate()
    {
        if (possibleSpawnPrefabs == null)
            possibleSpawnPrefabs = new GameObject[0];

        List<EnemySpawnEntry> updatedEntries = new List<EnemySpawnEntry>();

        foreach (GameObject prefab in possibleSpawnPrefabs)
        {
            if (prefab != null)
            {
                bool entryExists = false;
                if (enemyEntries != null)
                {
                    foreach (var entry in enemyEntries)
                    {
                        if (entry.prefab == prefab)
                        {
                            updatedEntries.Add(entry);
                            entryExists = true;
                            break;
                        }
                    }
                }

                if (!entryExists)
                {
                    EnemySpawnEntry newEntry = new EnemySpawnEntry
                    {
                        prefab = prefab,
                        spawnChance = 1f
                    };
                    updatedEntries.Add(newEntry);
                    Debug.Log($"[WaveData] Added new entry for prefab: {prefab.name}");
                }
            }
        }

        enemyEntries = updatedEntries.ToArray();
    }

    public override GameObject[] GetSpawns(int totalEnemies = 0)
    {
        if (enemyEntries == null || enemyEntries.Length == 0)
        {
            Debug.LogWarning("[WaveData] No enemy entries defined!");
            return new GameObject[0];
        }

        int count = Random.Range(spawnsPerTick.x, spawnsPerTick.y + 1);

        if (totalEnemies + count < startingCount)
            count = startingCount - totalEnemies;

        float totalWeight = 0f;
        foreach (var e in enemyEntries) totalWeight += Mathf.Max(0, e.spawnChance);

        GameObject[] result = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            float roll = Random.value * totalWeight;
            float cumulative = 0f;

            foreach (var entry in enemyEntries)
            {
                cumulative += Mathf.Max(0, entry.spawnChance);
                if (roll <= cumulative)
                {
                    result[i] = entry.prefab;
                    break;
                }
            }

            if (result[i] == null)
                result[i] = enemyEntries[enemyEntries.Length - 1].prefab;
        }

        return result;
    }

    public (GamePhase, Difficulty) GetKey() => (phase, difficulty);
}