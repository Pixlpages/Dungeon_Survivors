using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Wall Event Data", menuName = "NewScriptables/Event Data/Wall")]
public class WallEventData : EventData
{
    [Header("Wall Data")]
    public ParticleSystem spawnEffectPrefab;  // Particle effect for spawns
    public Vector2 scale = new Vector2(1, 1);  // Scale for wall positioning
    [Min(0)] public float wallLength = 20f;   // Length of the wall
    [Min(0)] public float wallDistance = 10f; // Distance from player
    [Min(0)] public float lifespan = 15f;     // How long enemies live before auto-destroy
    [Min(1)] public int enemyCount = 10;      // Number of enemies in the wall

    public override bool Activate(PlayerStats player = null)
    {
        int maxEnemies = SpawnManager.instance != null ? SpawnManager.instance.maximumEnemyCount : 300;
        if (player && EnemyStats.count < maxEnemies)  // Check max enemies
        {
            GameObject[] spawns = GetSpawns();
            if (spawns.Length == 0) return false;

            Debug.Log($"[WallEventData] Spawning wall with {enemyCount} enemies. Current count: {EnemyStats.count}/{maxEnemies}");

            // Calculate positions for a straight wall in front of the player
            Vector3 playerPos = player.transform.position;
            Vector3 wallStart = playerPos + player.transform.up * wallDistance;  // Assuming "up" is forward
            float angleOffset = wallLength / Mathf.Max(1, enemyCount - 1);

            for (int i = 0; i < enemyCount && i < spawns.Length; i++)
            {
                GameObject prefab = spawns[i];
                // Position along the wall line
                Vector3 spawnPosition = wallStart + player.transform.right * (i * angleOffset - wallLength / 2);

                // Spawn effect
                if (spawnEffectPrefab)
                    Instantiate(spawnEffectPrefab, spawnPosition, Quaternion.identity);

                // Spawn enemy
                GameObject s = Instantiate(prefab, spawnPosition, Quaternion.identity);
                if (s.GetComponent<EnemyStats>() == null)
                {
                    s.AddComponent<EnemyStats>();
                    Debug.Log("[WallEventData] Added EnemyStats to spawned enemy.");
                }

                // Auto-destroy after lifespan
                if (lifespan > 0)
                    Destroy(s, lifespan);
            }
            return true;
        }
        else
        {
            Debug.Log($"[WallEventData] Skipped wall spawn. Count: {EnemyStats.count}/{maxEnemies} (Max exceeded or no player)");
            return false;
        }
    }
}