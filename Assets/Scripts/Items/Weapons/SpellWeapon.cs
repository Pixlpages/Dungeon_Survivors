using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellWeapon : ProjectileWeapon
{
    List<EnemyStats> allSelectedEnemies = new List<EnemyStats>();

    protected override bool Attack(int attackCount = 1)
    {
        //if no projectile prefab is assigned, leave a message
        if (!currentStats.hitEffect)
        {
            Debug.LogWarning(string.Format("Hit effect prefab not set for {0}", name));
            ActivateCooldown(true);
            return false;
        }

        //if there is no projectile assigned, set the weapon on cooldown
        if (!CanAttack())
            return false;

        //if cooldown is > 0, fire the weapon for the first time.
        //refresh the array of selected enemies
        if (currentCoolDown <= 0)
        {
            allSelectedEnemies = new List<EnemyStats>(FindObjectsOfType<EnemyStats>());
            ActivateCooldown(true);
            currentAttackCount = attackCount;
        }

        //find an enemy in the map to strike with magic
        EnemyStats target = PickEnemy();
        if (target)
        {
            DamageArea(target.transform.position, GetArea(), GetDamage());

            Instantiate(currentStats.hitEffect, target.transform.position, Quaternion.identity);
        }

        //if there is a proc effect, play it
        if (currentStats.procEffect)
        {
            Destroy(Instantiate(currentStats.procEffect, owner.transform), 5f);
        }

        //If we have more than 1 attack count
        if (attackCount > 0)
        {
            currentAttackCount = attackCount - 1;
            currentAttackInterval = currentStats.projectileInterval;
        }

        return true;
    }

    //Randomly picks an enemy on screen
    EnemyStats PickEnemy()
    {
        EnemyStats target = null;
        while (!target && allSelectedEnemies.Count > 0)
        {
            int idx = Random.Range(0, allSelectedEnemies.Count);
            target = allSelectedEnemies[idx];

            //if the target is dead, remove it and skip it
            if (!target)
            {
                allSelectedEnemies.RemoveAt(idx);
                continue;
            }

            //check if the enemy is on screen
            //if enemy is missing a renderer, it cannot be struck, as we cannot
            //check whether it is on the screen or not
            Renderer r = target.GetComponent<Renderer>();
            if (!r || !r.isVisible)
            {
                allSelectedEnemies.Remove(target);
                target = null;
                continue;
            }
        }

        allSelectedEnemies.Remove(target);
        return target;
    }

    //Deals Damage in an area
    void DamageArea(Vector2 position, float radius, float damage)
    {
        Collider2D[] targets = Physics2D.OverlapCircleAll(position, radius);
        foreach (Collider2D t in targets)
        {
            EnemyStats es = t.GetComponent<EnemyStats>();
            if (es) es.TakeDamage(damage, transform.position);
            ApplyBuffs(es);
        }
    }
}
