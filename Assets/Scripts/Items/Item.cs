using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//base class for bothe the passive and the weapon classes. Primarily intended to handle weapon evolution

public abstract class Item : MonoBehaviour
{
    public int currentLevel = 1, maxLevel = 1;
    [HideInInspector] public ItemData data;
    protected ItemData.Evolution[] evolutionData;
    protected PlayerInventory inventory;
    protected PlayerStats owner;

    public PlayerStats Owner { get { return owner; }}

    [System.Serializable]
    public class LevelData
    {
        public string name, description;
    }

    public virtual void Initialise(ItemData data)
    {
        maxLevel = data.maxLevel;

        //store the evolution data as we have to track whether
        //all the catalysts are in the inventory so we can evolve
        evolutionData = data.evolutionData;

        //we have to find a better way to reference the player inventory in the future, this is inefficient
        inventory = FindObjectOfType<PlayerInventory>();
        owner = FindObjectOfType<PlayerStats>();
    }

    public virtual ItemData.Evolution[] CanEvolve()
    {
        List<ItemData.Evolution> possibleEvolutions = new List<ItemData.Evolution>();

        //check each listed evolution and whether it is in the inventory
        foreach (ItemData.Evolution e in evolutionData)
        {
            if (CanEvolve(e)) possibleEvolutions.Add(e);
        }

        return possibleEvolutions.ToArray();
    }

    //checks if a specific evolution is possible
    public virtual bool CanEvolve(ItemData.Evolution evolution, int levelUpAmount = 1)
    {
        //cannot evolve if the item hasn't reached the level to evolve
        if (evolution.evolutionLevel > currentLevel + levelUpAmount)
        {
            Debug.LogWarning(string.Format("Evolution failed. Current level {0}, evolution level {1}", currentLevel, evolution.evolutionLevel));
            return false;
        }

        //checks to see if all the catalysts are in the inventory
        foreach (ItemData.Evolution.Config c in evolution.catalysts)
        {
            Item item = inventory.Get(c.itemType);
            if (!item || item.currentLevel < c.level)
            {
                Debug.LogWarning(string.Format("Evolution failed, Mising {0}", c.itemType.name));
                return false;
            }
        }

        return true;
    }

    //AttemptEvolution will spawn a new weapon for the character, aand remove all the weapons that are consumed
    public virtual bool AttemptEvolution(ItemData.Evolution evolutionData, int levelUpAmount = 1)
    {
        if (!CanEvolve(evolutionData, levelUpAmount))
            return false;

        //do we consume passive or weapons?
        bool consumePassives = (evolutionData.consumes & ItemData.Evolution.Consumption.passives) > 0;
        bool consumeWeapons = (evolutionData.consumes & ItemData.Evolution.Consumption.weapons) > 0;

        //loop through all the catalysts and check if we should consume them
        foreach (ItemData.Evolution.Config c in evolutionData.catalysts)
        {
            if (c.itemType is PassiveData && consumePassives) inventory.Remove(c.itemType, true);
            if (c.itemType is WeaponData && consumeWeapons) inventory.Remove(c.itemType, true);
        }

        //should we consume ourselves as well???
        if (this is Passive && consumePassives) inventory.Remove((this as Passive).data, true);
        if (this is Weapon && consumeWeapons) inventory.Remove((this as Weapon).data, true);

        //add the new weapon in our inventory
        inventory.Add(evolutionData.outcome.itemType);

        return true;
    }

    public virtual bool CanLevelUp()
    {
        return currentLevel <= maxLevel;
    }

    //whenever an item levels up, attempt to make it evolve
    public virtual bool DoLevelUp()
    {
        if (evolutionData == null)
            return true;

        //tries to evolve into every listed evolution of this weapon,
        //if the weapons's evolution condition is levelling up
        foreach (ItemData.Evolution e in evolutionData)
        {
            if (e.condition == ItemData.Evolution.Condition.auto)
                AttemptEvolution(e);
        }
        return true;
    }

    //what effects you recieve on equipping an item
    public virtual void OnEquip()
    {

    }

    //what effects are removed on unequipping an item
    public virtual void OnUnequip()
    {
        
    }
}

