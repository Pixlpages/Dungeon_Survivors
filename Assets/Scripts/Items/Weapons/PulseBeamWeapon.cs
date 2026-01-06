using UnityEngine;

public class PulseBeamWeapon : ProjectileWeapon
{
    protected override bool Attack(int attackCount = 1)
    {
        if (!CanAttack()) return false;

        // Check if we have a projectile prefab (which should be the BeamEffect prefab)
        if (!currentStats.projectilePrefab)
        {
            Debug.LogWarning(string.Format("Projectile prefab has not been set for {0}", name));
            ActivateCooldown(true);
            return false;
        }

        float spawnAngle = GetSpawnAngle();  // Get the direction from the player

        // Spawn the BeamEffect at the player's position, oriented by spawnAngle
        WeaponEffect prefab = Instantiate(currentStats.projectilePrefab, 
            owner.transform.position + (Vector3)GetSpawnOffset(spawnAngle), 
            Quaternion.Euler(0, 0, spawnAngle));
        
        prefab.weapon = this;  // Link back to the weapon
        prefab.owner = owner;  // Link to the owner

        // The BeamEffect will handle its own lifespan and effects
        ActivateCooldown(true);  // Reset cooldown after spawning
        attackCount--;

        if (attackCount > 0)
        {
            // If multiple attacks, handle intervals (though for a beam, this might not be common)
            currentAttackCount = attackCount;
            currentAttackInterval = currentStats.projectileInterval;
        }

        return true;
    }

    // Optionally override other methods if needed, but this should suffice for now
}