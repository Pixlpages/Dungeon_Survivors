using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

//component to be attached to all weapon prefabs. The weapon prefab works together with the WeaponData ScriptableObjects
//to manage and run the behaviours of all weapons

public abstract class Weapon : Item
{
    [System.Serializable]
    public class Stats : LevelData //allows variables to be declared that also contain smaller variables inside
    {

        [Header("Visuals")]
        //public Projectile projectilePrefab; 
        public WeaponEffect projectilePrefab; // If attached a projectile will spawn every time the weapon is fired
        public Aura auraPrefab; // If attached, an aura will spawn when weapon is equipped
        [Tooltip("Hit effects")] public ParticleSystem hitEffect, procEffect;
        [Tooltip("If set, uses VFXPool instead of instantiating prefab directly.")]
        public string vfxKey;
        [Tooltip("TBA")] public Rect spawnVariance;

        [Header("Values")]
        [Tooltip("Projectile lifespan, if 0 last forever")] public float lifespan; // if 0, last forever
        [Tooltip("Base Damage of Weapon")] public float damage;
        [Tooltip("Damage Range the weapon will have")] public float damageVariance;
        [Tooltip("Size of AOE")] public float area;
        [Tooltip("Projectile Speed")] public float speed;
        [Tooltip("Time required before the weapon can be used again")] public float cooldown;
        [Tooltip("Time required for an additional projectile to be fired between each cooldown")] public float projectileInterval;
        [Tooltip("Knockback strength")] public float knockback;
        [Tooltip("Amount fired or spawned")] public int number;
        [Tooltip("Number of enemies the weapon can hit in a single projectile")] public int piercing;
        [Tooltip("max # of objects in scene")] public int maxInstances;

        public EntityStats.BuffInfo[] appliedBuffs;

        //Allows us to use the + operator to add 2 Stats together
        //very important later when we want to increase our weapon stats.

        public static Stats operator +(Stats s1, Stats s2) //the (+) allows us to add multiple stat objects
        {
            Stats result = new Stats();
            result.name = s2.name ?? s1.name;
            result.description = s2.description ?? s1.description;
            result.projectilePrefab = s2.projectilePrefab ?? s1.projectilePrefab;
            result.auraPrefab = s2.auraPrefab ?? s1.auraPrefab;
            result.vfxKey = !string.IsNullOrEmpty(s2.vfxKey) ? s2.vfxKey : s1.vfxKey;
            result.hitEffect = s2.hitEffect == null ? s1.hitEffect : s2.hitEffect;
            result.spawnVariance = s2.spawnVariance;
            result.lifespan = s1.lifespan + s2.lifespan;
            result.damage = s1.damage + s2.damage;
            result.damageVariance = s1.damageVariance + s2.damageVariance;
            result.area = s1.area + s2.area;
            result.speed = s1.speed + s2.speed;
            result.cooldown = s1.cooldown + s2.cooldown;
            result.number = s1.number + s2.number;
            result.piercing = s1.piercing + s2.piercing;
            result.projectileInterval = s1.projectileInterval + s2.projectileInterval;
            result.knockback = s1.knockback + s2.knockback;
            result.appliedBuffs = s2.appliedBuffs == null || s2.appliedBuffs.Length <= 0 ? s1.appliedBuffs : s2.appliedBuffs;
            return result;
        }

        //Get damage dealt
        public float GetDamage()
        {
            return damage + Random.Range(0, damageVariance);
        }
    }

    protected Stats currentStats;
    protected float currentCoolDown;
    protected PlayerMovement movement;

    //for dynamically created weapons, call initialise to set everything up
    public virtual void Initialise(WeaponData data)
    {
        base.Initialise(data);
        this.data = data;
        currentStats = data.baseStats;
        movement = GetComponentInParent<PlayerMovement>();
        ActivateCooldown();
    }

    protected virtual void Update()
    {
        currentCoolDown -= Time.deltaTime;
        if (currentCoolDown <= 0f)
        {
            Attack(currentStats.number + owner.Stats.amount);
        }
    }

    //levels up the weapon by 1, and calculates the corresponding stats
    public override bool DoLevelUp()
    {
        //prevent level up if already at max level
        if (!CanLevelUp())
        {
            Debug.LogWarning(string.Format("Cannot level up at {0} to Level {1}, max level of {2} already reached", name, currentLevel, data.maxLevel));
            return false;
        }

        //otherwise, add stats of the next level to our weapon
        currentStats += (Stats)data.GetLevelData(++currentLevel);
        
        // Increment the PPM tracker when weapon is leveled up
        if (data is WeaponData weaponData)
        {
            PredictivePlayerModel.Instance?.RecordItemCategory(weaponData.category);
        }
 

        return true;
    }

    //checks whether this weapon can attack at the current moment
    public virtual bool CanAttack()
    {
        if (Mathf.Approximately(owner.Stats.strength, 0))
            return false;
        return currentCoolDown <= 0;
    }

    //performs an attack with the weapon
    //returns true if the attack was successful
    //this doesnt do anything,. We have to override this at the chilld class to add a behaviour
    protected virtual bool Attack(int attackCount = 1)
    {
        if (CanAttack())
        {
            currentCoolDown += Owner.Stats.cooldown;
            return true;
        }
        return false;
    }

    public virtual float GetDamage()
    {
        return currentStats.GetDamage() * owner.Stats.strength;
    }

    //get the area. including modifications from the player's stats
    public virtual float GetArea()
    {
        return currentStats.area + owner.Stats.area;
    }

    //for retrieving the weapon's stats
    public virtual Stats GetStats()
    {
        return currentStats;
    }

    public virtual bool ActivateCooldown(bool strict = false)
    {
        //when <strict> is enabled and the cooldown is not yet finished
        //do not refresh the cooldown
        if (strict && currentCoolDown > 0)
            return false;

        //calculate what the cooldown is going to be, factoring in the cooldown
        //reduction stat in the player character
        float actualCooldown = currentStats.cooldown * Owner.Stats.cooldown;

        //limit the maximum cooldown to the actual cooldown, so we cannot increase
        //the cooldown above the cooldown stat if we accidentally call this function
        //multiple times
        currentCoolDown = Mathf.Min(actualCooldown, currentCoolDown + actualCooldown);
        return true;
    }

    //Makes the weapon apply its buff to a targeted EntityStats object
    public void ApplyBuffs(EntityStats e)
    {
        //apply all assigned buffs to the target
        foreach (EntityStats.BuffInfo b in GetStats().appliedBuffs)
            e.ApplyBuff(b, owner.Actual.duration);
    }
}
