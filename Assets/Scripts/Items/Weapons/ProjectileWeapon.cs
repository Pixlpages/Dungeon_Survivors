using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileWeapon : Weapon
{
    protected float currentAttackInterval;
    protected int currentAttackCount; //Number of times this attack will happen

    protected override void Update()
    {
        base.Update();

        //otherwise, if the attack interval goes from above 0 to below, we also call attack
        if (currentAttackInterval > 0)
        {
            currentAttackInterval -= Time.deltaTime;
            if (currentAttackInterval <= 0)
            {
                Attack(currentAttackCount);
            }
        }
    }

    public override bool CanAttack()
    {
        if (currentAttackCount > 0)
        {
            return true;
        }

        return base.CanAttack();
    }

    protected override bool Attack(int attackCount = 1)
    {
        //if no projectile prefab is assigned, leave a warning message
        if (!currentStats.projectilePrefab)
        {
            Debug.LogWarning(string.Format("Projectile prefab has not been set for {0}", name));
            ActivateCooldown(true);
            return false;
        }
        if (!CanAttack()) return false; //can we attack?

        //otherwise, calculate the angle and offset of spawned projectile
        float spawnAngle = GetSpawnAngle();

        //if there is a proc effect, play it
        if (currentStats.procEffect)
        {
            Destroy(Instantiate(currentStats.procEffect, owner.transform), 5f);
        }

        //and spawn a copy of the projectile
            //Projectile prefab = Instantiate(currentStats.projectilePrefab, owner.transform.position + (Vector3)GetSpawnOffset(spawnAngle), Quaternion.Euler(0, 0, spawnAngle));
            WeaponEffect prefab = Instantiate(currentStats.projectilePrefab, owner.transform.position + (Vector3)GetSpawnOffset(spawnAngle), Quaternion.Euler(0, 0, spawnAngle));

        prefab.weapon = this;

        prefab.weapon = this;
        prefab.owner = owner;

        //reset the cooldown only if this attack was triggered by cooldown
        ActivateCooldown(true);
        attackCount--;

        //do we perform another attack?
        if (attackCount > 0)
        {
            currentAttackCount = attackCount;
            currentAttackInterval = ((WeaponData)data).baseStats.projectileInterval;
        }
        return true;
    }

    //get which direction the projectile should face when spawning
    protected virtual float GetSpawnAngle()
    {
        return Mathf.Atan2(movement.lastMovedVector.y, movement.lastMovedVector.x) * Mathf.Rad2Deg;
    }

    //Generates a random point to spawn the projectile on, and rotates the facing of the point by spawnAngle

    protected virtual Vector2 GetSpawnOffset(float spawnAngle = 0)
    {
        return Quaternion.Euler(0, 0, spawnAngle) * new Vector2(
            Random.Range(currentStats.spawnVariance.xMin, currentStats.spawnVariance.xMax),
            Random.Range(currentStats.spawnVariance.yMin, currentStats.spawnVariance.yMax)
        );
    }
}
