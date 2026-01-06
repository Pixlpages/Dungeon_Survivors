using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LifestealKnives : ProjectileWeapon
{
    [Header("Fan Settings")]
    [Tooltip("Total fan angle in degrees (spread for multiple projectiles)")]
    public float fanAngle = 60f;  // Total spread angle

    protected override bool Attack(int attackCount = 1)
    {
        if (!currentStats.projectilePrefab)
        {
            Debug.LogWarning(string.Format("Projectile prefab has not been set for {0}", name));
            ActivateCooldown(true);
            return false;
        }
        if (!CanAttack()) return false;

        // Play proc effect if set
        if (currentStats.procEffect)
        {
            Destroy(Instantiate(currentStats.procEffect, owner.transform), 5f);
        }

        // Get base spawn angle (player's facing direction)
        float baseSpawnAngle = GetSpawnAngle();

        // Calculate number of projectiles to fire
        int projectileCount = Mathf.Max(1, attackCount);  // Ensure at least 1

        // Fire projectiles in a fan shape
        for (int i = 0; i < projectileCount; i++)
        {
            // Calculate angle offset for fan spread
            float angleOffset = 0f;
            if (projectileCount > 1)
            {
                // Evenly distribute across the fan angle
                float halfFan = fanAngle / 2f;
                angleOffset = Mathf.Lerp(-halfFan, halfFan, (float)i / (projectileCount - 1));
            }

            float spawnAngle = baseSpawnAngle + angleOffset;

            // Calculate spawn position with variance
            Vector2 spawnOffset = GetSpawnOffset(spawnAngle);
            Vector3 spawnPosition = owner.transform.position + (Vector3)spawnOffset;

            // Instantiate projectile
            WeaponEffect prefab = Instantiate(currentStats.projectilePrefab, spawnPosition, Quaternion.Euler(0, 0, spawnAngle));
            prefab.weapon = this;
            prefab.owner = owner;
        }

        // Reset cooldown
        ActivateCooldown(true);

        // Handle additional attacks if needed
        attackCount -= projectileCount;
        if (attackCount > 0)
        {
            currentAttackCount = attackCount;
            currentAttackInterval = ((WeaponData)data).baseStats.projectileInterval;
        }

        return true;
    }
}
