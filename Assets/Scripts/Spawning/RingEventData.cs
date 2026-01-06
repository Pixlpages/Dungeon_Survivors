using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Ring Event Data", menuName = "NewScriptables/Event Data/Ring")]
public class RingEventData : EventData
{
    [Header("Mob Data")]
    public ParticleSystem spawnEffectPrefab;
    public Vector2 scale = new Vector2(1, 1);
    [Min(0)] public float spawnRadius = 10f, lifespan = 15f;

    public override bool Activate(PlayerStats player = null)
    {
        if (player && !SpawnManager.HasExceededMaxEnemies())  // Check max enemies before spawning
        {
            GameObject[] spawns = GetSpawns();
            Debug.Log($"[RingEventData] Spawning {spawns.Length} enemies in ring for event '{name}'. Current enemy count: {EnemyStats.count}");
            float angleOffset = 2 * Mathf.PI / Mathf.Max(1, spawns.Length);
            float currentAngle = 0;
            foreach (GameObject g in spawns)
            {
                //calculate spawn position
                Vector3 spawnPosition = player.transform.position + new Vector3(
                    spawnRadius * Mathf.Cos(currentAngle) * scale.x,
                    spawnRadius * Mathf.Sin(currentAngle) * scale.y
                );
                //if a particcle effect is assigned, play it on the position
                if (spawnEffectPrefab)
                    Instantiate(spawnEffectPrefab, spawnPosition, Quaternion.identity);
                //then spawn enemy
                GameObject s = Instantiate(g, spawnPosition, Quaternion.identity);
                //if there is a lifespan on the mob, set them to be destroyed
                if (lifespan > 0)
                    Destroy(s, lifespan);
                currentAngle += angleOffset;
            }
            return true;  // Indicate success
        }
        else
        {
            Debug.Log($"[RingEventData] Skipped spawning for event '{name}' due to max enemies exceeded ({EnemyStats.count}/{SpawnManager.instance?.maximumEnemyCount ?? 300})");
        }
        return false;
    }
}
