using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class KnifeProjectile : Projectile
{
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        EnemyStats es = other.GetComponent<EnemyStats>();

        if (es)
        {
            Vector3 source = damageSource == DamageSource.owner && owner ? owner.transform.position : transform.position;
            Weapon.Stats stats = weapon.GetStats();

            float damageDealt = GetDamage();  // Get the damage this projectile will deal
            es.TakeDamage(damageDealt, source);
            weapon.ApplyBuffs(es);

            // Lifesteal: Heal player by damage dealt / 2
            ApplyLifesteal(damageDealt);

            // Handle VFX and piercing as in base class
            if (!string.IsNullOrEmpty(stats.vfxKey) && VFXPool.Instance != null)
            {
                VFXPool.Instance.Get(stats.vfxKey, transform.position, Quaternion.identity);
            }
            else if (stats.hitEffect)
            {
                var fx = Instantiate(stats.hitEffect, transform.position, Quaternion.identity);
                Destroy(fx.gameObject, 5f);
            }

            piercing--;

            // Destroy when piercing is exhausted
            if (piercing <= 0)
            {
                Destroy(gameObject);
            }
        }
    }

    // Apply lifesteal: Heal player 
    private void ApplyLifesteal(float damageDealt)
    {
        if (owner != null && owner is PlayerStats playerStats)
        {
            float healAmount = 0.4f;
            playerStats.RestoreHealth(healAmount);  // Use your existing RestoreHealth method
        }
    }
}