using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PlayerInventory : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public Item item;

        public void Assign(Item assignedItem)
        {
            item = assignedItem;
            if (item is Weapon)
            {
                Weapon w = item as Weapon;
            }
            else
            {
                Passive p = item as Passive;
            }
            Debug.Log(string.Format("Assigned {0} to player", item.name));
        }

        public void Clear()
        {
            item = null;
        }

        public bool IsEmpty()
        {
            return item == null;
        }
    }

    public List<Slot> weaponSlots = new List<Slot>(6);
    public List<Slot> passiveSlots = new List<Slot>(6);
    public UIInventoryIconsDisplay weaponUI, passiveUI;

    [Header("UI Elements")]
    public List<WeaponData> availableWeapons = new List<WeaponData>(); // List of upgrade options for weapons
    public List<PassiveData> availablePassives = new List<PassiveData>(); // List of upgrade options for Passives
    public List<StatBoostData> availableStatBoosts = new List<StatBoostData>(); // List of flat stat boosts options

    [Header("Level Up Effects")]
    [Tooltip("Sound effect to play when leveling up")]
    public AudioClip levelUpSound;
    private AudioSource audioSource;

    public UIUpgradeWindow upgradeWindow;
    PlayerStats player;

    // Raised when a weapon is successfully added to the inventory.
    // Other systems (PPM, trackers, analytics) can subscribe at runtime.
    public event Action<WeaponData> WeaponAdded;
    // Raised when a passive is successfully added.
    public event Action<PassiveData> PassiveAdded;

    // Storage for applied stat boosts (persist through run)
    [System.Serializable]
    public class AppliedStatBoost
    {
        public StatBoostData data;
        public int level; // 1-based
        public AppliedStatBoost(StatBoostData d, int lvl) { data = d; level = lvl; }
    }
    private List<AppliedStatBoost> appliedStatBoosts = new List<AppliedStatBoost>();

    void Start()
    {
        player = GetComponent<PlayerStats>();
        audioSource = GetComponent<AudioSource>();  // Get AudioSource for playing sound
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();  // Fallback: add one if missing
        }
    }

    // Checks if the inventory has an item of a certain type
    public bool Has(ItemData type)
    {
        return Get(type);
    }

    public Item Get(ItemData type)
    {
        if (type is WeaponData)
        {
            return Get(type as WeaponData);
        }
        else if (type is PassiveData)
        {
            return Get(type as PassiveData);
        }
        return null;
    }

    // Find a passive of a certain type in the inventory
    public Passive Get(PassiveData type)
    {
        foreach (Slot s in passiveSlots)
        {
            Passive p = s.item as Passive;
            if (p && p.data == type)
                return p;
        }
        return null;
    }

    // Find a weapon of a certain type in the inventory
    public Weapon Get(WeaponData type)
    {
        foreach (Slot s in weaponSlots)
        {
            Weapon w = s.item as Weapon;
            if (w && w.data == type)
                return w;
        }
        return null;
    }

    // Removes a weapon of a particular type as specified by <data>
    public bool Remove(WeaponData data, bool removeUpgradeAvailability = false)
    {
        // Remove this weapon from the upgrade pool
        if (removeUpgradeAvailability)
            availableWeapons.Remove(data);

        for (int i = 0; i < weaponSlots.Count; i++)
        {
            Weapon w = weaponSlots[i].item as Weapon;
            if (w.data == data)
            {
                weaponSlots[i].Clear();
                w.OnUnequip();
                Destroy(w.gameObject);
                return true;
            }
        }
        return false;
    }

    // Removes a passive of a particular type as specified by <data>
    public bool Remove(PassiveData data, bool removeUpgradeAvailability = false)
    {
        // Remove this weapon from the upgrade pool
        if (removeUpgradeAvailability)
            availablePassives.Remove(data);

        for (int i = 0; i < passiveSlots.Count; i++)
        {
            Passive p = passiveSlots[i].item as Passive;
            if (p.data == data)
            {
                passiveSlots[i].Clear();
                p.OnUnequip();
                Destroy(p.gameObject);
                return true;
            }
        }
        return false;
    }

    // If an ItemData is passed, determine what type it is and call the respective overload
    // There is also an optional boolean to remove this item from the upgrade list
    public bool Remove(ItemData data, bool removeUpgradeAvailability = false)
    {
        if (data is PassiveData)
        {
            return Remove(data as PassiveData, removeUpgradeAvailability);
        }
        else if (data is WeaponData)
        {
            return Remove(data as WeaponData, removeUpgradeAvailability);
        }
        return false;
    }

    // Finds an empty slot and adds a weapon of a certain type, returns the slot number that the item was put in
    public int Add(WeaponData data)
    {
        int slotNum = -1;

        // Try to find an empty slot
        for (int i = 0; i < weaponSlots.Capacity; i++)
        {
            if (weaponSlots[i].IsEmpty())
            {
                slotNum = i;
                break;
            }
        }

        // If there is no empty slot, exit
        if (slotNum < 0)
            return slotNum;

        // Otherwise create the weapon in the slot
        // Get the type of the weapon we want to spawn
        Type weaponType = Type.GetType(data.behaviour);

        if (weaponType != null)
        {
            // Spawn the weapon gameobject
            GameObject go = new GameObject(data.baseStats.name + "Controller");
            Weapon spawnedWeapon = (Weapon)go.AddComponent(weaponType);
            spawnedWeapon.transform.SetParent(transform); // Sets the weapon to be a child of the player
            spawnedWeapon.transform.localPosition = Vector2.zero;
            spawnedWeapon.Initialise(data);
            spawnedWeapon.OnEquip();

            // Assign the weapon to the slot
            weaponSlots[slotNum].Assign(spawnedWeapon);
            weaponUI.Refresh();

            // Record for PPM
            WeaponAdded?.Invoke(data);

            // Close the level up UI if it is on
            if (GameManager.Instance != null && GameManager.Instance.choosingUpgrades)
            {
                GameManager.Instance.EndLevelUp();
            }

            return slotNum;
        }
        else
        {
            Debug.LogWarning(string.Format("Invalid Weapon type specified for {0}", data.name));
        }

        return -1;
    }

    // Finds an empty slot and adds a passive of a certain type, returns the slot number the item was put in
    public int Add(PassiveData data)
    {
        int slotNum = -1;

        // Try to find an empty slot
        for (int i = 0; i < passiveSlots.Capacity; i++)
        {
            if (passiveSlots[i].IsEmpty())
            {
                slotNum = i;
                break;
            }
        }

        // If there is no empty slot, exit
        if (slotNum < 0)
            return slotNum;

        // Otherwise create the passive in the slot.
        // Get the type of the passive we want to spawn
        GameObject go = new GameObject(data.baseStats.name + "Passive");
        Passive p = go.AddComponent<Passive>();
        p.Initialise(data);
        p.transform.SetParent(transform); // Set the weapon to be a child of the player
        p.transform.localPosition = Vector2.zero;

        // Assign the passive to the slot
        passiveSlots[slotNum].Assign(p);
        passiveUI.Refresh();

        // Record for PPM
        PassiveAdded?.Invoke(data);

        if (GameManager.Instance != null && GameManager.Instance.choosingUpgrades)
        {
            GameManager.Instance.EndLevelUp();
        }
        player.RecalculateStats();

        return slotNum;
    }

    // Add statboost (applies as flat boost)
    public bool Add(StatBoostData data)
    {
        if (data == null) return false;
        var existing = GetAppliedStatBoost(data);
        if (existing != null)
        {
            // If already applied and not at max, level it up
            if (existing.level < data.GetMaxLevel())
            {
                existing.level++;
                player.RecalculateStats();
                if (GameManager.Instance != null && GameManager.Instance.choosingUpgrades)
                    GameManager.Instance.EndLevelUp();
                return true;
            }
            else
            {
                // Already maxed
                return false;
            }
        }
        else
        {
            // Apply new at level 1
            appliedStatBoosts.Add(new AppliedStatBoost(data, 1));
            player.RecalculateStats();
            if (GameManager.Instance != null && GameManager.Instance.choosingUpgrades)
                GameManager.Instance.EndLevelUp();
            return true;
        }
    }

    // If I don't know what item is being added, this function will determine that
    public int Add(ItemData data)
    {
        if (data is WeaponData) return Add(data as WeaponData);
        if (data is PassiveData) return Add(data as PassiveData);
        if (data is StatBoostData)
        {
            bool ok = Add(data as StatBoostData);
            return ok ? 1 : -1; // Not a real slot number; UI only uses callback
        }
        return -1;
    }

    public List<Weapon> GetAllWeapons()
    {
        List<Weapon> result = new List<Weapon>();

        foreach (Slot s in weaponSlots)
        {
            if (s.item is Weapon w)
                result.Add(w);
        }

        return result;
    }

    public List<Passive> GetAllPassives()
    {
        List<Passive> result = new List<Passive>();

        foreach (Slot s in passiveSlots)
        {
            if (s.item is Passive p)
                result.Add(p);
        }

        return result;
    }

    // Find a stat boost applied by data
    public AppliedStatBoost GetAppliedStatBoost(StatBoostData data)
    {
        return appliedStatBoosts.Find(s => s.data == data);
    }

    public List<AppliedStatBoost> GetAllAppliedStatBoosts()
    {
        return appliedStatBoosts;
    }

    // Overload so that we can use both ItemData or Item to level up an item in inventory
    public bool LevelUp(ItemData data)
    {
        if (data is StatBoostData)
        {
            return LevelUp(data as StatBoostData);
        }

        Item item = Get(data);
        if (item)
            return LevelUp(item);
        return false;
    }

    // Level up a selected weapon in the player inventory
    public bool LevelUp(Item item)
    {
        // Tries to level up the item
        if (!item.DoLevelUp())
        {
            Debug.LogWarning(string.Format("Failed to Level up {0}", item.name));
            return false;
        }

        weaponUI.Refresh();
        passiveUI.Refresh();

        // Close the level up screen afterwards
        if (GameManager.Instance != null && GameManager.Instance.choosingUpgrades)
        {
            GameManager.Instance.EndLevelUp();
        }

        // If it is a passive, recalculate player stats
        if (item is Passive)
            player.RecalculateStats();
        return true;
    }

    // LevelUp for StatBoost
    public bool LevelUp(StatBoostData data)
    {
        var existing = GetAppliedStatBoost(data);
        if (existing == null)
        {
            // Not applied yet -> apply at level 1
            appliedStatBoosts.Add(new AppliedStatBoost(data, 1));
            player.RecalculateStats();
            if (GameManager.Instance != null && GameManager.Instance.choosingUpgrades)
                GameManager.Instance.EndLevelUp();
            return true;
        }
        else
        {
            if (existing.level < data.GetMaxLevel())
            {
                existing.level++;
                player.RecalculateStats();
                if (GameManager.Instance != null && GameManager.Instance.choosingUpgrades)
                    GameManager.Instance.EndLevelUp();
                return true;
            }
            return false; // Already max
        }
    }

    // Checks a list of slots to see if there are any slots left
    int GetSlotsLeft(List<Slot> slots)
    {
        int count = 0;
        foreach (Slot s in slots)
        {
            if (s.IsEmpty())
                count++;
        }
        return count;
    }

    // Determines what upgrade options should appear
    void ApplyUpgradeOptions()
    {
        // <availableUpgrades> is an empty list that will be filtered from
        // <allUpgrades>, which is the list of ALL upgrades in PlayerInventory
        // Not all upgrades can be applied as some may have already been maxed out by
        // the player, or the player may not have enough inventory slots
        List<ItemData> availableUpgrades = new List<ItemData>();
        List<ItemData> allUpgrades = new List<ItemData>(availableWeapons);
        allUpgrades.AddRange(availablePassives);
        foreach (var s in availableStatBoosts) allUpgrades.Add(s);

        // We need to know how many weapon / passive slots are left
        int weaponSlotsLeft = GetSlotsLeft(weaponSlots);
        int passiveSlotsLeft = GetSlotsLeft(passiveSlots);

        // Play level up sound
        if (levelUpSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(levelUpSound);
        }

        // Filters through the available weapons and passives and add those that can possibly be an option
        foreach (ItemData data in allUpgrades)
        {
            // StatBoostData handled differently: always allow up to max level (no slot required)
            if (data is StatBoostData)
            {
                var sdata = data as StatBoostData;
                var applied = GetAppliedStatBoost(sdata);
                if (applied == null || applied.level < sdata.GetMaxLevel())
                    availableUpgrades.Add(data);
                continue;
            }

            // If a weapon of this type exists, allow for the upgrade if the 
            // level of the weapon is not already maxed out
            Item obj = Get(data);
            if (obj)
            {
                if (obj.currentLevel < data.maxLevel)
                    availableUpgrades.Add(data);
            }
            else
            {
                // If we don't have this item in the inventory yet, check if
                // we still have enough slots to take new items
                if (data is WeaponData && weaponSlotsLeft > 0)
                    availableUpgrades.Add(data);
                else if (data is PassiveData && passiveSlotsLeft > 0)
                    availableUpgrades.Add(data);
            }
        }

        // Apply MDP biases via LootManager before showing UI
        if (LootManager.Instance != null)
        {
            // Trigger MDP bias update for level-up
            MDPManager.Instance?.OnPlayerLevelUp();
            availableUpgrades = LootManager.Instance.GetBiasedUpgrades(availableUpgrades, 4); // Bias the list to 4 options
        }

        // Show the UI upgrade window if we still have available upgrades left
        int availUpgradeCount = availableUpgrades.Count;
        if (availUpgradeCount > 0)
        {
            // bool getExtraItem = 1f > UnityEngine.Random.value;
            // if (getExtraItem || availUpgradeCount < 4)
            //     upgradeWindow.SetUpgrades(this, availableUpgrades, 4);
            // else
                upgradeWindow.SetUpgrades(this, availableUpgrades, 3, "Choose An Item!!"); // MDP can also work with this
        }
        else if (GameManager.Instance != null && GameManager.Instance.choosingUpgrades)
        {
            GameManager.Instance.EndLevelUp();
        }
    }

    public void RemoveAndApplyUpgrades()
    {
        ApplyUpgradeOptions();
    }
    
    // Accessor for PlayerStats/RecalculateStats
    public List<AppliedStatBoost> GetAppliedStatBoostsPublic() => appliedStatBoosts;
}
