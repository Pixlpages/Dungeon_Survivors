using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//base class for all weapons/passives. The base class is used so that both WeaponData and PassiveItemData are able to be used
//interchangeably if required

public abstract class ItemData : ScriptableObject
{
    public Sprite icon;
    public int maxLevel;

    [SerializeField] public int id;        // Unique identifier for MDP rules
    public int Id => id;

    [SerializeField] public Rarity rarity; 
    public Rarity Rarity => rarity;


    [System.Serializable]
    public struct Evolution
    {
        public string name;
        public enum Condition { auto, treasureChest }
        public Condition condition;

        [System.Flags]
        public enum Consumption { passives = 1, weapons = 2 }
        public Consumption consumes;

        public int evolutionLevel;
        public Config[] catalysts;
        public Config outcome;

        [System.Serializable]
        public struct Config
        {
            public ItemData itemType;
            public int level;
        }
    }

    public Evolution[] evolutionData;

    public abstract Item.LevelData GetLevelData(int level);
}
