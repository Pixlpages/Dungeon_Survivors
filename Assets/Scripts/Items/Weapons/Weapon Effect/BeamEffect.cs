using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(ParticleSystem))]
public class BeamEffect : WeaponEffect
{
    private BoxCollider2D beamCollider;  // Collider for hit detection
    private ParticleSystem beamParticles; // ParticleSystem for visuals
    private int piercing;                 // Piercing count
    private Vector2 initialColliderSize;  // Store original collider size

    protected virtual void Start()
    {
        if (weapon == null || owner == null)
        {
            Debug.LogWarning("BeamEffect spawned without weapon or owner.");
            Destroy(gameObject);
            return;
        }

        Weapon.Stats stats = weapon.GetStats();
        piercing = stats.piercing;

        // --- Collider setup ---
        beamCollider = GetComponent<BoxCollider2D>();
        beamCollider.isTrigger = true;
        initialColliderSize = beamCollider.size;

        float areaMultiplier = Mathf.Max(0.1f, weapon.GetArea()); // width scaling
        beamCollider.size = new Vector2(initialColliderSize.x, initialColliderSize.y * areaMultiplier);
        beamCollider.offset = new Vector2(beamCollider.size.x * 0.5f, 0f);

        // --- Particle setup ---
        beamParticles = GetComponent<ParticleSystem>();
        if (beamParticles != null)
        {
            var main = beamParticles.main;

            // Scale particle size dynamically based on area stat
            if (main.startSize3D)
            {
                main.startSizeX = new ParticleSystem.MinMaxCurve(main.startSizeX.constant);
                main.startSizeY = new ParticleSystem.MinMaxCurve(main.startSizeY.constant * areaMultiplier);
                main.startSizeZ = new ParticleSystem.MinMaxCurve(main.startSizeZ.constant);
            }
            else
            {
                main.startSize = new ParticleSystem.MinMaxCurve(main.startSize.constant * areaMultiplier);
            }

            // Rotate particle to match player facing
            Vector2 lookDir = owner.GetComponent<PlayerMovement>()?.lastMovedVector ?? Vector2.right;
            if (lookDir == Vector2.zero) lookDir = Vector2.right;
            float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            beamParticles.Play();
        }

        // --- Lifespan ---
        if (stats.lifespan > 0f)
        {
            Destroy(gameObject, stats.lifespan);
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (weapon == null) return;

        if (other.TryGetComponent(out EnemyStats enemy))
        {
            float damage = weapon.GetDamage();
            enemy.TakeDamage(damage, transform.position);
            weapon.ApplyBuffs(enemy);

            // Optional hit effect
            Weapon.Stats stats = weapon.GetStats();
            if (stats.hitEffect)
            {
                ParticleSystem fx = Instantiate(stats.hitEffect, other.transform.position, Quaternion.identity);
                Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax);
            }

            piercing--;
            if (piercing <= 0)
                Destroy(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (beamCollider != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(beamCollider.offset, beamCollider.size);
        }
    }
}
