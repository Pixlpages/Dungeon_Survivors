using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : WeaponEffect
{
    public enum DamageSource { projectile, owner }
    public DamageSource damageSource = DamageSource.projectile;
    public bool hasAutoAim = false;
    public Vector3 rotationSpeed = new Vector3(0, 0, 0);
    public bool isBoomerang = false;  // Enable boomerang behavior

    [Header("Orbit Settings")]
    public bool isOrbiting = false;  // Enable orbiting around the owner
    public float orbitRadius = 2f;  // Distance from owner to orbit
    public float orbitSpeedMultiplier = 1f;  // Multiplier for orbit speed based on weapon speed

    [Header("Homing Settings")]
    public bool isHoming = false;  // Enable homing toward nearest enemy
    public float homingSpeed = 5f;  // How fast it turns toward targets
    public float homingAcceleration = 10f;  // Acceleration toward targets

    [Header("Bouncing Settings")]
    public bool isBouncing = false;  // Enable bouncing off screen edges
    public int maxBounces = 5;  // Max screen bounces before destroying
    public float bounceDamping = 0.9f;  // Speed loss per bounce (0.9 = 10% loss)

    protected Rigidbody2D rb;
    protected int piercing;
    private float startTime;  // Time when projectile was created
    private bool isReturning = false;  // Track return state
    private float lifespan;  // Store weapon's lifespan
    public float orbitAngle = 0f;  // Current orbit angle

    [Tooltip("Toggle if this projectile should scale its size with area")]
    public bool scaleWithArea = false;

    [Header("AOE Settings")]
    public bool isAOE = false;
    public LayerMask enemyLayer;

    // Optimization additions
    public bool hasHit = false;  // Flag to prevent multiple AOE queries
    public Dictionary<Collider2D, EnemyStats> cachedEnemies = new Dictionary<Collider2D, EnemyStats>();
    public List<EnemyStats> pendingHits = new List<EnemyStats>();

    // Homing/Bouncing variables
    private Transform currentTarget;
    private float currentSpeed;
    private int currentBounces = 0;
    private Camera mainCamera;

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        Weapon.Stats stats = weapon.GetStats();
        startTime = Time.time;  // Record start time
        lifespan = stats.lifespan;  // Store lifespan
        currentSpeed = stats.speed * weapon.Owner.Stats.speed;  // Initial speed for homing/bouncing

        if (isBoomerang && isOrbiting)
        {
            Debug.LogWarning("Projectile: Cannot enable both boomerang and orbiting. Disabling orbiting.");
            isOrbiting = false;
        }

        if (isBoomerang)
        {
            rb.gravityScale = 0f;  // Disable gravity for boomerang to prevent falling
            if (rb.bodyType != RigidbodyType2D.Dynamic)
            {
                Debug.LogWarning("Projectile: Rigidbody should be Dynamic for boomerang to work properly.");
            }
        }

        if (isOrbiting && owner != null)
        {
            // Initialize orbit angle based on current position relative to owner
            Vector2 direction = (transform.position - owner.transform.position).normalized;
            orbitAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        if (isHoming || isBouncing)
        {
            mainCamera = Camera.main;  // Cache camera for bouncing
            if (isHoming) FindInitialTarget();
        }

        if (rb.bodyType == RigidbodyType2D.Dynamic && !isOrbiting)
        {
            rb.angularVelocity = rotationSpeed.z;
            rb.velocity = transform.right * stats.speed * weapon.Owner.Stats.speed;
        }

        float area = weapon.GetArea();
        if (area <= 0) area = 1;

        if (scaleWithArea)
        {
            transform.localScale = new Vector3(
                area * Mathf.Sign(transform.localScale.x),
                area * Mathf.Sign(transform.localScale.y),
                1
            );
        }

        piercing = stats.piercing;

        if (stats.lifespan > 0) Destroy(gameObject, stats.lifespan);

        if (hasAutoAim)
        {
            AcquireAutoAimFacing();
        }
    }

    protected virtual void FixedUpdate()
    {
        // Process pending hits in batches to reduce callback overhead
        if (pendingHits.Count > 0)
        {
            Weapon.Stats stats = weapon.GetStats();
            Vector3 source = damageSource == DamageSource.owner && owner ? owner.transform.position : transform.position;
            float damageDealt = GetDamage();

            foreach (EnemyStats es in pendingHits)
            {
                es.TakeDamage(damageDealt, source);
                weapon.ApplyBuffs(es);
                piercing--;
            }

            // Spawn VFX once per batch
            if (!string.IsNullOrEmpty(stats.vfxKey) && VFXPool.Instance != null)
            {
                VFXPool.Instance.Get(stats.vfxKey, transform.position, Quaternion.identity);
            }
            else if (stats.hitEffect)
            {
                var fx = Instantiate(stats.hitEffect, transform.position, Quaternion.identity);
                Destroy(fx.gameObject, 5f);
            }

            pendingHits.Clear();
            if (piercing <= 0) Destroy(gameObject);
        }

        if (isOrbiting && owner != null)
        {
            // Update orbit angle using weapon's speed stat
            Weapon.Stats stats = weapon.GetStats();
            orbitAngle += (stats.speed * weapon.Owner.Stats.speed * orbitSpeedMultiplier) * Time.fixedDeltaTime;

            // Calculate new position around owner
            Vector2 orbitPosition = (Vector2)owner.transform.position + new Vector2(
                Mathf.Cos(orbitAngle * Mathf.Deg2Rad) * orbitRadius,
                Mathf.Sin(orbitAngle * Mathf.Deg2Rad) * orbitRadius
            );

            // Move to orbit position
            rb.MovePosition(orbitPosition);
            transform.Rotate(rotationSpeed * Time.fixedDeltaTime);  // Optional: Rotate while orbiting
        }
        else if (rb.bodyType == RigidbodyType2D.Dynamic)
        {
            Weapon.Stats stats = weapon.GetStats();

            if (isBoomerang)
            {
                // Check if halfway through lifespan to start returning
                if (!isReturning && Time.time - startTime >= lifespan / 2f)
                {
                    isReturning = true;
                }

                if (isReturning && owner != null)
                {
                    // Return at the same speed as firing
                    Vector2 directionToPlayer = ((Vector2)owner.transform.position - (Vector2)transform.position).normalized;
                    rb.velocity = directionToPlayer * stats.speed * weapon.Owner.Stats.speed;
                }
                else if (!isReturning)
                {
                    // Move forward initially
                    rb.velocity = transform.right * stats.speed * weapon.Owner.Stats.speed;
                }
            }
            else if (isHoming && currentTarget != null)
            {
                // Homing logic
                Vector2 direction = (currentTarget.position - transform.position).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, 0, angle), homingSpeed * Time.fixedDeltaTime);

                // Accelerate toward target
                currentSpeed += homingAcceleration * Time.fixedDeltaTime;
                rb.velocity = transform.right * currentSpeed;
            }
            else
            {
                // Normal movement
                rb.velocity = transform.right * stats.speed * weapon.Owner.Stats.speed;
            }

            transform.Rotate(rotationSpeed * Time.fixedDeltaTime);
        }

        // Handle bouncing if enabled
        if (isBouncing)
        {
            CheckScreenBounce();
        }
    }

    private void FindInitialTarget()
    {
        // Use cached list instead of FindObjectsOfType for O(1) performance
        float minDistance = float.MaxValue;
        foreach (EnemyStats enemy in EnemyStats.AllEnemies)
        {
            if (enemy.isDead) continue;  // Skip dead enemies
            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                currentTarget = enemy.transform;
            }
        }
    }

private void CheckScreenBounce()
{
    if (mainCamera == null) return;

    // Get screen bounds in world space (accounts for moving camera)
    Vector3 bottomLeft = mainCamera.ScreenToWorldPoint(new Vector3(0, 0, mainCamera.nearClipPlane));
    Vector3 topRight = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, mainCamera.nearClipPlane));
    float screenLeft = bottomLeft.x;
    float screenRight = topRight.x;
    float screenBottom = bottomLeft.y;
    float screenTop = topRight.y;

    Vector2 pos = transform.position;
    Vector2 newVelocity = rb.velocity;
    bool bounced = false;

    // Check and reflect for each edge
    if (pos.x < screenLeft)
    {
        newVelocity.x = Mathf.Abs(newVelocity.x);  // Reflect rightward
        pos.x = screenLeft + 0.1f;  // Small offset to prevent sticking
        bounced = true;
    }
    else if (pos.x > screenRight)
    {
        newVelocity.x = -Mathf.Abs(newVelocity.x);  // Reflect leftward
        pos.x = screenRight - 0.1f;
        bounced = true;
    }

    if (pos.y < screenBottom)
    {
        newVelocity.y = Mathf.Abs(newVelocity.y);  // Reflect upward
        pos.y = screenBottom + 0.1f;
        bounced = true;
    }
    else if (pos.y > screenTop)
    {
        newVelocity.y = -Mathf.Abs(newVelocity.y);  // Reflect downward
        pos.y = screenTop - 0.1f;
        bounced = true;
    }

    if (bounced)
    {
        rb.velocity = newVelocity * bounceDamping;
        transform.position = pos;  // Apply offset position
        currentBounces++;

        // Update rotation to match new direction
        float angle = Mathf.Atan2(newVelocity.y, newVelocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Destroy if max bounces reached
        if (currentBounces >= maxBounces)
        {
            Destroy(gameObject);
        }
    }
}


    public virtual void AcquireAutoAimFacing()
    {
        float aimAngle;

        EnemyStats[] targets = FindObjectsOfType<EnemyStats>();

        if (targets.Length > 0)
        {
            EnemyStats selectedTarget = targets[Random.Range(0, targets.Length)];
            Vector2 difference = selectedTarget.transform.position - transform.position;
            aimAngle = Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg;
        }
        else
        {
            aimAngle = Random.Range(0f, 360f);
        }

        transform.rotation = Quaternion.Euler(0, 0, aimAngle);
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!cachedEnemies.TryGetValue(other, out EnemyStats es))
        {
            es = other.GetComponent<EnemyStats>();
            if (es) cachedEnemies[other] = es;
        }

        if (es)
        {
            if (isAOE && !hasHit)
            {
                // AOE: Query once and damage all, then destroy
                hasHit = true;
                Vector3 source = damageSource == DamageSource.owner && owner ? owner.transform.position : transform.position;
                Weapon.Stats stats = weapon.GetStats();
                float damageDealt = GetDamage();

                Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, stats.area, enemyLayer);
                foreach (Collider2D hit in hits)
                {
                    EnemyStats target = hit.GetComponent<EnemyStats>();
                    if (target)
                    {
                        target.TakeDamage(damageDealt, source);
                        weapon.ApplyBuffs(target);
                    }
                }

                // Spawn VFX once
                if (!string.IsNullOrEmpty(stats.vfxKey) && VFXPool.Instance != null)
                {
                    VFXPool.Instance.Get(stats.vfxKey, transform.position, Quaternion.identity);
                }
                else if (stats.hitEffect)
                {
                    var fx = Instantiate(stats.hitEffect, transform.position, Quaternion.identity);
                    Destroy(fx.gameObject, 5f);
                }

                Destroy(gameObject);
            }
            else if (!isAOE && !pendingHits.Contains(es))
            {
                // Non-AOE: Collect hits for batch processing
                pendingHits.Add(es);
            }

            // Re-target on hit if homing
            if (isHoming)
            {
                FindInitialTarget();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (isAOE && weapon != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, weapon.GetArea());
        }
        if (isOrbiting && owner != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(owner.transform.position, orbitRadius);
        }
    }
}
