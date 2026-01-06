using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeleeWeapon : Weapon
{
    protected float currentAttackInterval;
    protected int currentAttackCount; // how many times this attack will happen

    protected override void Update()
    {
        base.Update();

        // handle multiple swings chained together
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
        // ensure prefab is assigned
        if (!currentStats.projectilePrefab)
        {
            Debug.LogWarning($"Melee prefab not set for {name}");
            ActivateCooldown(true);
            return false;
        }
        if (!CanAttack()) return false;

        // play any proc effect
        if (currentStats.procEffect)
        {
            Destroy(Instantiate(currentStats.procEffect, owner.transform), 5f);
        }

        // spawn the melee prefab
        Melee prefab = Instantiate(
            currentStats.projectilePrefab, 
            owner.transform.position, 
            Quaternion.identity
        ).GetComponent<Melee>();

        prefab.weapon = this;
        prefab.owner = owner;

        ActivateCooldown(true);

        attackCount--;

        // handle chained swings
        if (attackCount > 0)
        {
            currentAttackCount = attackCount;
            currentAttackInterval = ((WeaponData)data).baseStats.projectileInterval;
        }

        return true;
    }
}
