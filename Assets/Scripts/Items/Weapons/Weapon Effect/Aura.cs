using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Used for DOT effects in an area, with optional vortex (pull) behavior.
// Pulling moves enemy.transform directly (no Rigidbody2D).
public class Aura : WeaponEffect
{
    public bool isPulling = false;      // Checkbox to enable pulling
    public bool useLifetime = false;    // Use weapon lifespan to auto-destroy
    public bool lockPosition = false;   // Lock aura to its initial position
    public float pullStrength = 5f;     // Public pull strength (used directly)
    public Transform pullCenter;        // Center to pull towards (optional)
    
    private Vector3 initialPosition;    // Store initial world position

    Dictionary<EnemyStats, float> affectedTargets = new Dictionary<EnemyStats, float>();
    List<EnemyStats> targetsToUnaffect = new List<EnemyStats>();

    void Start()
    {
        // Save initial position and ensure pullCenter exists
        initialPosition = transform.position;
        if (pullCenter == null)
        {
            // If no pull center set, default to aura's own transform
            pullCenter = transform;
        }
        
        // Use weapon lifespan if requested
        if (weapon != null && useLifetime)
        {
            Weapon.Stats stats = weapon.GetStats();
            if (stats.lifespan > 0f)
                Destroy(gameObject, stats.lifespan);
        }
    }

    void Update()
    {
        if (lockPosition)
        {
            transform.position = initialPosition;
        }

        // Continuous pulling: move any affected enemies toward the pull center every frame
        if (isPulling && pullCenter != null && affectedTargets.Count > 0)
        {
            // Move each affected enemy toward the center (work directly with transforms)
            foreach (var kv in affectedTargets)
            {
                EnemyStats enemy = kv.Key;
                if (enemy) // null-check
                    PullEnemyDirect(enemy);
            }
        }

        // DOT damage tick logic (keeps original behavior but fixed to check updated timers)
        if (affectedTargets.Count > 0)
        {
            // Copy keys to safely iterate while mutating dictionary
            List<EnemyStats> keys = new List<EnemyStats>(affectedTargets.Keys);
            foreach (EnemyStats es in keys)
            {
                // If entry was removed elsewhere, continue
                if (!affectedTargets.ContainsKey(es)) continue;

                affectedTargets[es] -= Time.deltaTime;

                if (affectedTargets[es] <= 0f)
                {
                    if (targetsToUnaffect.Contains(es))
                    {
                        // marked for removal: remove and clear marker
                        affectedTargets.Remove(es);
                        targetsToUnaffect.Remove(es);
                    }
                    else
                    {
                        // deal damage, reset timer and apply buffs
                        Weapon.Stats weaponStats = weapon != null ? weapon.GetStats() : new Weapon.Stats();
                        float cooldownTick = (weapon != null) ? weaponStats.cooldown * owner.Stats.cooldown : 1f;
                        affectedTargets[es] = cooldownTick;

                        // Use WeaponEffect.GetDamage() so it respects weapon.might etc.
                        es.TakeDamage(GetDamage(), transform.position, weaponStats.knockback);
                        if (weapon != null)
                            weapon.ApplyBuffs(es);

                        // play hit effect if present
                        if (weapon != null && weaponStats.hitEffect)
                        {
                            Destroy(Instantiate(weaponStats.hitEffect, es.transform.position, Quaternion.identity), 5f);
                        }

                        // Note: pulling is continuous above, but if you also want to pull on damage tick only,
                        // uncomment the next line to call PullEnemyDirect(es) here instead.
                        // if (isPulling) PullEnemyDirect(es);
                    }
                }
            }
        }
    }

    // Pull enemy by moving its transform directly (works when Enemy has no Rigidbody2D)
    void PullEnemyDirect(EnemyStats enemy)
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy) return;

        Vector3 centerPos = pullCenter != null ? pullCenter.position : transform.position;
        Vector3 enemyPos = enemy.transform.position;
        Vector3 dir = (centerPos - enemyPos);
        float dist = dir.magnitude;
        if (dist <= 0.001f) return;
        dir.Normalize();

        // movement step this frame (do not multiply by weapon knockback; uses public pullStrength)
        float step = pullStrength * Time.deltaTime;

        // clamp to avoid overshooting the center
        if (step > dist) step = dist;

        enemy.transform.position = enemyPos + dir * step;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out EnemyStats es))
        {
            if (!affectedTargets.ContainsKey(es))
            {
                // start with interval 0 so it will be damaged on next Update tick
                affectedTargets.Add(es, 0f);
            }
            else
            {
                if (targetsToUnaffect.Contains(es))
                    targetsToUnaffect.Remove(es);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out EnemyStats es))
        {
            if (affectedTargets.ContainsKey(es))
            {
                // mark for removal; we don't remove immediately so DOT timers complete correctly
                targetsToUnaffect.Add(es);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // visualize pull center
        if (pullCenter != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pullCenter.position, 0.25f);
        }
    }
}
