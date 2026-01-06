using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class EnemyStats : EntityStats
{
    [System.Serializable]
    public struct Resistances
    {
        [Range(-1f, 1f)] public float kill, debuff;

        //To allow us to multiply the resistances
        public static Resistances operator *(Resistances r, float factor)
        {
            r.kill = Mathf.Min(1, r.kill * factor);
            r.debuff = Mathf.Min(1, r.debuff * factor);
            return r;
        }

        public static Resistances operator +(Resistances r, Resistances r2)
        {
            r.kill = r2.kill;
            r.debuff = r2.debuff;
            return r;
        }

        //Allows us to multiply resistances by one another, for multiplicative buffs
        public static Resistances operator *(Resistances r1, Resistances r2)
        {
            r1.kill = Mathf.Min(1, r1.kill * r2.kill);
            r1.debuff = Mathf.Min(1, r1.debuff * r2.debuff);
            return r1;
        }
    }

    [System.Serializable]
    public struct Stats
    {
        public float maxHealth, moveSpeed, damage;
        public float knockbackMultiplier;
        public Resistances resistances;

        [System.Flags]
        public enum Boostable { health = 1, moveSpeed = 2, damage = 4, knockbackMultiplier = 8, resistances = 16 }
        public Boostable curseBoosts, levelBoosts;

        private static Stats Boost(Stats s1, float factor, Boostable boostable)
        {
            float delta = factor - 1f; // how much curse is adding
            if ((boostable & Boostable.health) != 0)
                s1.maxHealth *= 1f + delta * 1.0f;   // full effect (assume 30%)
            if ((boostable & Boostable.moveSpeed) != 0)
                s1.moveSpeed *= 1f + delta * 0.5f;   // half effect (only half of 30%)
            if ((boostable & Boostable.damage) != 0)
                s1.damage *= 1f + delta * 0.25f;      // gentle effect (only 25% of 30%)
            if ((boostable & Boostable.knockbackMultiplier) != 0)
                s1.knockbackMultiplier /= 1f + delta * 1.0f; // inverse scaling
            if ((boostable & Boostable.resistances) != 0)
                s1.resistances *= 1f + delta * 0.3f; // mild effect
            return s1;
        }

        //use the multiply operator for curse
        public static Stats operator *(Stats s1, float factor)
        {
            return Boost(s1, factor, s1.curseBoosts);
        }

        //use the XOR operator for level boosted stats
        public static Stats operator ^(Stats s1, float factor)
        {
            return Boost(s1, factor, s1.levelBoosts);
        }

        //use the add operator to add stats to the enemy
        public static Stats operator +(Stats s1, Stats s2)
        {
            s1.maxHealth += s2.maxHealth;
            s1.moveSpeed += s2.moveSpeed;
            s1.damage += s2.damage;
            s1.knockbackMultiplier += s2.knockbackMultiplier;
            s1.resistances += s2.resistances;
            return s1;
        }

        //use the multiply operator to scale stats
        //used by the buff/debuff system
        public static Stats operator *(Stats s1, Stats s2)
        {
            s1.maxHealth *= s2.maxHealth;
            s1.moveSpeed *= s2.moveSpeed;
            s1.damage *= s2.damage;
            s1.knockbackMultiplier *= s1.knockbackMultiplier;  // Fixed: was s1.knockbackMultiplier *= s2.knockbackMultiplier; (typo in original)
            s1.resistances *= s2.resistances;
            return s1;
        }
    }

    public Stats baseStats = new Stats { maxHealth = 10, moveSpeed = 1, damage = 3, knockbackMultiplier = 1 };
    public Stats actualStats;
    public Stats Actual
    {
        get { return actualStats; }
    }

    public BuffInfo[] attackEffects;

    [Header("Damage FeedBack")]
    //temp damage feedback, use my old animation code for this, mr future me
    public Color damageColor = new Color(1, 0, 0, 1); // flash color
    public float damageFlashDuration = 0.2f;    //how long 
    public float deathFadeTime = 0.2f; //how long till fade
    EnemyMovement movement;

    public static int count;
    private AgentAnimations agentAnimations;

    Collider2D col;  // Now properly assigned below
    public bool isDead = false;
    private float firstHitTime = -1f;

    public static System.Collections.Generic.List<EnemyStats> AllEnemies = new System.Collections.Generic.List<EnemyStats>();  // NEW: Cached list for fast homing

    void Awake()
    {
        count++;
        sprite = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();  // FIX: Assign the collider here
        AllEnemies.Add(this);  // NEW: Add to cached list
    }

    protected override void Start()
    {
        base.Start();
        RecalculateStats();
        health = actualStats.maxHealth;

        movement = GetComponent<EnemyMovement>();
        agentAnimations = GetComponent<AgentAnimations>();
    }

    public override bool ApplyBuff(BuffData data, int variant = 0, float durationMultiplier = 1f)
    {
        //if the debuff is a generic debuff, we check for debuff resistance, change the type if I plan to add specific resistances
        //roll a number and if it succeeds, ignore the debuff
        if ((data.type & BuffData.Type.debuff) > 0)
            if (Random.value <= Actual.resistances.debuff)
                return false;

        return base.ApplyBuff(data, variant, durationMultiplier);
    }

    //calculates the actual stats of the enemy based on a variety of factors
    public override void RecalculateStats()
    {
        //calculate curse boosts
        float curseMultiplier = GameManager.GetCumulativeCurse(),
              levelMultiplier = GameManager.GetCumulativeLevels();
        actualStats = baseStats * curseMultiplier * levelMultiplier;

        //Create a variable to store all the cumulative multiplier values
        Stats multiplier = new Stats
        {
            maxHealth = 1f,
            moveSpeed = 1f,
            damage = 1f,
            knockbackMultiplier = 1f,
            resistances = new Resistances { debuff = 1f, kill = 1f }
        };

        foreach (Buff b in activeBuffs)
        {
            BuffData.Stats bd = b.GetData();
            switch (bd.modifierType)
            {
                case BuffData.ModifierType.additive:
                    actualStats += bd.enemyModifier;
                    break;
                case BuffData.ModifierType.multiplicative:
                    multiplier *= bd.enemyModifier;
                    break;
            }
        }

        //apply the multipliers last
        actualStats *= multiplier;
    }

    public override void TakeDamage(float dmg)
    {
        // mark first hit time
        if (firstHitTime < 0f)
            firstHitTime = GameManager.Instance.GetElapsedTime();

        health -= dmg;
        PredictivePlayerModel.Instance?.RecordDamageDealt(dmg);
        //if damage is exactly equal to maximum health, we assume it is an insta kill
        //check for the kill resistance to see if we can dodge this damage
        if (dmg == actualStats.maxHealth)
        {
            //Roll a die to check if we can dodge the damage
            //gets a random value between 0 to 1, and if the number is below the kill resistance, then we avoid getting killed
            if (Random.value < actualStats.resistances.kill)
            {
                return; //don't take damage
            }
        }

        //create text popup when enemy takes damage
        if (dmg > 0)
        {
            //StartCoroutine(DamageFlash());
            GameManager.GenerateFloatingText(Mathf.FloorToInt(dmg).ToString(), transform);
        }

        //kill enemy if health = 0
        if (health <= 0)
        {
            Kill();
        }
        else
        {
            agentAnimations?.PlayHitAnimation();
        }
    }

    //this function always needs at least 2 values, the amount of damage dealth <dmg> as well as
    //where the damage is coming from, which is passed as <sourcePosition>, used to calculate knockback direction
    public void TakeDamage(float dmg, Vector2 sourcePosition, float knockbackForce = 5f, float knockbackDuration = 0.2f)
    {
        TakeDamage(dmg);

        //apply knockback if not zero
        if (knockbackForce > 0 && !isDead)
        {
            //gets the direction of knockback
            Vector2 dir = (Vector2)transform.position - sourcePosition;
            movement.Knockback(dir.normalized * knockbackForce, knockbackDuration);
        }
    }

    public override void RestoreHealth(float amount)
    {
        if (health < actualStats.maxHealth)
        {
            health += amount;
            if (health > actualStats.maxHealth)
            {
                health = actualStats.maxHealth;
            }
        }
    }

    public override void Kill()
    {
        if (isDead) return;
        isDead = true;

        //  Report TTK to PPM
        if (firstHitTime >= 0f)
        {
            float killTime = GameManager.Instance.GetElapsedTime();
            float ttk = killTime - firstHitTime;
            PredictivePlayerModel.Instance?.RecordEnemyKill(ttk);
        }

        //enable drops if the enemy is killed
        //drops aare disabled by default
        DropRateManager drops = GetComponent<DropRateManager>();
        if (drops)
            drops.active = true;

        // stop movement
        if (movement != null) movement.enabled = false;

        // disable collisions immediately to prevent weapon interactions
        if (col != null) col.enabled = false;

        agentAnimations?.PlayDeathAnimation();

        StartCoroutine(KillFade());
    }

    private void OnTriggerStay2D(Collider2D col)
    {
        if (isDead)
            return;

        if (Mathf.Approximately(Actual.damage, 0))
            return;

        //reference the script from the collided collider then damage with TakeDamage()
        if (col.TryGetComponent(out PlayerStats p))
        {
            p.TakeDamage(Actual.damage);
            foreach (BuffInfo b in attackEffects)
                p.ApplyBuff(b);
            
            GetComponent<MARLAgent>()?.OnSuccessfulHitPlayer();
        }
    }

    //coroutine for enmey flash when taking damage
    IEnumerator DamageFlash()
    {
        ApplyTint(damageColor);
        yield return new WaitForSeconds(damageFlashDuration);
        RemoveTint(damageColor);
    }

    IEnumerator KillFade()
    {
        //wait for a single frame
        WaitForEndOfFrame w = new WaitForEndOfFrame();
        float t = 0, origAlpha = sprite.color.a;

        while (t < deathFadeTime)
        {
            yield return w;
            t += Time.deltaTime;

            //set the color for this frame
            sprite.color = new Color(sprite.color.r, sprite.color.g, sprite.color.b, (1 - t / deathFadeTime) * origAlpha);
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        count--;
        AllEnemies.Remove(this);  // NEW: Remove from cached list
    }

    public void ResetStats()
    {
        health = actualStats.maxHealth;
        isDead = false;
    }
}