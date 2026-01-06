using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Boss Event Data", menuName = "NewScriptables/Event Data/Boss")]
public class BossEventData : EventData
{
    [Header("Boss Data")]
    [Range(0f, 360f)] public float possibleAngles = 360f;  // Angle range for spawn position
    [Min(0)] public float spawnRadius = 2f;  // Random offset from spawn distance
    [Min(0)] public float spawnDistance = 30f;  // Distance from player (increased for boss drama)

    public override bool Activate(PlayerStats player = null)
    {
        // Only activate if the player is present
        if (player == null) return false;

        // Spawn one boss enemy outside the screen at a random angle
        float randomAngle = Random.Range(0, possibleAngles) * Mathf.Deg2Rad;
        GameObject[] spawns = GetSpawns();
        if (spawns.Length == 0) return false;

        // Pick the first (or only) enemy prefab for the boss
        GameObject bossPrefab = spawns[0];  // Assumes the first in the list is the boss; adjust if needed

        Instantiate(bossPrefab, player.transform.position + new Vector3(
            (spawnDistance + Random.Range(-spawnRadius, spawnRadius)) * Mathf.Cos(randomAngle),
            (spawnDistance + Random.Range(-spawnRadius, spawnRadius)) * Mathf.Sin(randomAngle)
        ), Quaternion.identity);

        return true;  // Indicate success
    }
}
