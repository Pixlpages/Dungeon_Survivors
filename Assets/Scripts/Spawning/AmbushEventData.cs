using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Ambush Event Data", menuName = "NewScriptables/Event Data/Ambush")]
public class AmbushEventData : EventData
{
    [Header("Ambush Data")]
    public ParticleSystem spawnEffectPrefab;  // Particle effect for spawns
    [Min(0)] public float spawnRadius = 15f;  // Radius around player for random spawns
    [Min(0)] public float lifespan = 10f;     // How long enemies live before auto-destroy
    [Min(1)] public int burstCount = 5;      // Number of enemies per burst
    [Min(0)] public float burstInterval = 0.5f;  // Time between bursts (for quick spawning)

    public override bool Activate(PlayerStats player = null)
    {
        int maxEnemies = SpawnManager.instance != null ? SpawnManager.instance.maximumEnemyCount : 300;
        if (player && EnemyStats.count < maxEnemies)  // Check max enemies
        {
            GameObject[] spawns = GetSpawns();
            if (spawns.Length == 0) return false;

            Debug.Log($"[AmbushEventData] Triggering ambush with {burstCount} enemies per burst. Current count: {EnemyStats.count}/{maxEnemies}");

            // Start a coroutine for burst spawning (to make it quick and staggered)
            player.StartCoroutine(SpawnBursts(player, spawns));
            return true;
        }
        else
        {
            Debug.Log($"[AmbushEventData] Skipped ambush. Count: {EnemyStats.count}/{maxEnemies} (Max exceeded or no player)");
            return false;
        }
    }

    private IEnumerator SpawnBursts(PlayerStats player, GameObject[] spawns)
    {
        for (int burst = 0; burst < spawns.Length / burstCount + 1; burst++)
        {
            for (int i = 0; i < burstCount && burst * burstCount + i < spawns.Length; i++)
            {
                GameObject prefab = spawns[burst * burstCount + i];
                // Random position around player
                Vector3 randomOffset = new Vector3(
                    Random.Range(-spawnRadius, spawnRadius),
                    Random.Range(-spawnRadius, spawnRadius)
                );
                Vector3 spawnPosition = player.transform.position + randomOffset;

                // Spawn effect
                if (spawnEffectPrefab)
                    Instantiate(spawnEffectPrefab, spawnPosition, Quaternion.identity);

                // Spawn enemy
                GameObject s = Instantiate(prefab, spawnPosition, Quaternion.identity);
                if (s.GetComponent<EnemyStats>() == null)
                {
                    s.AddComponent<EnemyStats>();
                    Debug.Log("[AmbushEventData] Added EnemyStats to spawned enemy.");
                }

                // Auto-destroy after lifespan
                if (lifespan > 0)
                    Destroy(s, lifespan);
            }
            yield return new WaitForSeconds(burstInterval);  // Wait between bursts for quick effect
        }
    }
}