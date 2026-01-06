using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

//summary
//replacement for the WeaponScriptableObject class, The idea is to store all weapon evolution data in one single object
//instead of multiple objects to store a single weapon

[CreateAssetMenu(fileName = "Weapon Data", menuName = "NewScriptables/Weapon")]
public class WeaponData : ItemData
{
    [Header("Categorization for PPM")]
    [SerializeField] public ItemCategory category;
    public ItemCategory Category => category;

    [HideInInspector] public string behaviour;
    public Weapon.Stats baseStats;

    [Tooltip("Only non 0 stats will be overriden, this is also incremental meaning 1+1=2")]
    public Weapon.Stats[] linearGrowth;
    
    [Tooltip("A way for the weapon to continue growing even if you did not supply enough levels in linear growth")]
    public Weapon.Stats[] randomGrowth;

    //gives us the stat growth / decscription of the next level

    public override Item.LevelData GetLevelData(int level)
    {
        if (level <= 1)
            return baseStats;

        //pick the stats from the next level
        if (level - 2 < linearGrowth.Length)
        {
            return linearGrowth[level - 2];
        }

        //otherwise, pick one of the stats from the random growth array
        if (randomGrowth.Length > 0)
        {
            return randomGrowth[Random.Range(0, randomGrowth.Length)];
        }

        //return empty value
        Debug.LogWarning(string.Format("Weapon doesnt gave its level up stats configured for Level {0}!", level));
        return new Weapon.Stats();
    }
}
