using System.Collections;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class Melee : WeaponEffect
{
    [Header("Settings")]
    public LayerMask enemyLayer = ~0;
    public float spawnDistance = 0.6f;   // distance in front of player

    private int piercing;
    private float swingDuration;  // Now pulled from stats

    // Optimization additions
    private Dictionary<Collider2D, EnemyStats> cachedEnemies = new Dictionary<Collider2D, EnemyStats>();
    private List<EnemyStats> pendingHits = new List<EnemyStats>();

    void Start()
    {
        if (weapon == null || owner == null)
        {
            Debug.LogWarning("Melee spawned without weapon or owner.");
            Destroy(gameObject);
            return;
        }

        Weapon.Stats stats = weapon.GetStats();

        // Set swing duration from stats lifespan
        swingDuration = stats.lifespan;
        if (swingDuration <= 0) swingDuration = 0.2f;  // Fallback to default

        // Scale hitbox size with area
        float area = weapon.GetArea();
        if (area <= 0) area = 1;
        transform.localScale = Vector3.one * area;

        // Get player facing direction
        Vector2 lookDir = owner.GetComponent<PlayerMovement>()?.lastMovedVector ?? Vector2.right;
        if (lookDir == Vector2.zero) lookDir = Vector2.right;
        lookDir.Normalize();

        // Rotate only the visual FX child, not the collider
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;
        Transform slashFX = transform.Find("FX");
        if (slashFX != null)
            slashFX.rotation = Quaternion.Euler(0, 0, angle);

        // Spawn hitbox in front of player
        transform.position = (Vector2)owner.transform.position + lookDir * spawnDistance * area;

        // Piercing count
        piercing = stats.piercing;

        // Destroy after swing duration
        Destroy(gameObject, swingDuration);
    }

    private void FixedUpdate()
    {
        // Process pending hits in batches
        if (pendingHits.Count > 0)
        {
            Weapon.Stats stats = weapon.GetStats();

            foreach (EnemyStats enemy in pendingHits)
            {
                enemy.TakeDamage(GetDamage(), transform.position);
                weapon.ApplyBuffs(enemy);

                // Apply knockback from stats
                if (stats.knockback > 0 && enemy.TryGetComponent(out Rigidbody2D rb))
                {
                    Vector2 knockbackDirection = (enemy.transform.position - transform.position).normalized;
                    rb.AddForce(knockbackDirection * stats.knockback, ForceMode2D.Impulse);
                }

                // Spawn hit effect on the enemy (per enemy, not per batch)
                SpawnHitEffect(stats, enemy.transform.position);

                piercing--;
            }

            pendingHits.Clear();
            if (piercing <= 0) Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (weapon == null) return;

        if (!cachedEnemies.TryGetValue(other, out EnemyStats enemy))
        {
            enemy = other.GetComponent<EnemyStats>();
            if (enemy) cachedEnemies[other] = enemy;
        }

        if (enemy && !pendingHits.Contains(enemy))
        {
            // Collect hits for batch processing
            pendingHits.Add(enemy);
        }
    }

    void SpawnHitEffect(Weapon.Stats stats, Vector3 pos)
    {
        if (!string.IsNullOrEmpty(stats.vfxKey) && VFXPool.Instance != null)
        {
            VFXPool.Instance.Get(stats.vfxKey, pos, Quaternion.identity);  // Use the passed position (enemy's location)
        }
        else if (stats.hitEffect)
        {
            var fx = Instantiate(stats.hitEffect, pos, Quaternion.identity);  // Use the passed position (enemy's location)
            Destroy(fx.gameObject, 5f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (weapon != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, weapon.GetArea() * 0.5f);
        }
    }
}
