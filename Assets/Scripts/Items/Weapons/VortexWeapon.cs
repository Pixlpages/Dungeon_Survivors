using UnityEngine;
using System.Collections.Generic;

public class VortexWeapon : Weapon  // Inherits from Weapon.cs
{
    List<EnemyStats> allSelectedEnemies = new List<EnemyStats>();  // Like in SpellWeapon

    protected override bool Attack(int attackCount = 1)
    {
        if (!CanAttack()) return false;

        allSelectedEnemies = new List<EnemyStats>(FindObjectsOfType<EnemyStats>());  // Refresh list
        int numberToSpawn = currentStats.number;  // Use the number field

        if (allSelectedEnemies.Count > 0)
        {
            int enemiesToProcess = Mathf.Min(numberToSpawn, allSelectedEnemies.Count);
            for (int i = 0; i < enemiesToProcess; i++)
            {
                EnemyStats target = PickEnemy();  // Pick randomly
                if (target && target.gameObject.activeInHierarchy)
                {
                    Aura aura = Instantiate(currentStats.auraPrefab, target.transform.position, Quaternion.identity, transform) as Aura;  // As child
                    aura.weapon = this;  // Link for stats
                    aura.owner = owner;
                    float area = GetArea();
                    aura.transform.localScale = new Vector3(area, area, area);
                    // Set to use world position internally
                    if (aura.pullCenter != null)
                    {
                        aura.pullCenter.position = target.transform.position;  // Fix to world position
                    }
                }
            }

            ActivateCooldown(true);
            return true;
        }
        else
        {
            Debug.LogWarning("No enemies found for VortexWeapon attack.");
            ActivateCooldown(true);
            return false;
        }
    }

    EnemyStats PickEnemy()
    {
        EnemyStats target = null;
        while (!target && allSelectedEnemies.Count > 0)
        {
            int idx = Random.Range(0, allSelectedEnemies.Count);
            target = allSelectedEnemies[idx];

            if (!target || !target.gameObject.activeInHierarchy)
            {
                allSelectedEnemies.RemoveAt(idx);
                target = null;
                continue;
            }

            Renderer r = target.GetComponent<Renderer>();
            if (!r || !r.isVisible)
            {
                allSelectedEnemies.Remove(target);
                target = null;
                continue;
            }

            allSelectedEnemies.Remove(target);
        }
        return target;
    }
}
