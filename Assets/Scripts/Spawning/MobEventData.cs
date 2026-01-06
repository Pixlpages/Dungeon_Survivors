using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Mob Event Data", menuName = "NewScriptables/Event Data/ Mob")]
public class MobEventData : EventData
{
    [Header("Mob Data")]
    [Range(0f, 360f)] public float possibleAngles = 360f;
    [Min(0)] public float spawnRadius = 2f, spawnDistance = 20f;
    [Min(0)] public float lifespan = 15f;  // Add lifespan to auto-destroy mobs
    public override bool Activate(PlayerStats player = null)
    {
        //only activate if the player is present and not over max enemies
        if (player && !SpawnManager.HasExceededMaxEnemies())  // Check max enemies
        {
            //otherwise, we spawn a mob outside of the screen and move it towards the player
            float randomAngle = Random.Range(0, possibleAngles) * Mathf.Deg2Rad;
            GameObject[] spawns = GetSpawns();
            Debug.Log($"[MobEventData] Spawning {spawns.Length} enemies for mob event '{name}'. Current enemy count: {EnemyStats.count}");
            foreach (GameObject o in spawns)
            {
                GameObject s = Instantiate(o, player.transform.position + new Vector3(
                    (spawnDistance + Random.Range(-spawnRadius, spawnRadius)) * Mathf.Cos(randomAngle),
                    (spawnDistance + Random.Range(-spawnRadius, spawnRadius)) * Mathf.Sin(randomAngle)
                ), Quaternion.identity);
                
                // Destroy after lifespan if set
                if (lifespan > 0)
                    Destroy(s, lifespan);
            }
            return true;  // Indicate success
        }
        else
        {
            Debug.Log($"[MobEventData] Skipped spawning for event '{name}' due to max enemies exceeded ({EnemyStats.count}/{SpawnManager.instance?.maximumEnemyCount ?? 300})");
        }
        return false;
    }

}
