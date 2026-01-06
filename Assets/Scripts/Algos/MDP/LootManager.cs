using System.Collections.Generic;
using UnityEngine;

public class LootManager : MonoBehaviour
{
    public static LootManager Instance;

    // Biases for categories and rarities
    private Dictionary<ItemCategory, float> categoryBiases = new Dictionary<ItemCategory, float>();
    private Dictionary<Rarity, float> rarityBiases = new Dictionary<Rarity, float>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Apply bias toward an item category.
    /// </summary>
    public void AddCategoryBias(ItemCategory category, float weight = 1f)
    {
        if (categoryBiases.ContainsKey(category))
            categoryBiases[category] += weight;
        else
            categoryBiases[category] = weight;
    }

    /// <summary>
    /// Apply bias toward a rarity.
    /// </summary>
    public void AddRarityBias(Rarity rarity, float weight = 1f)
    {
        if (rarityBiases.ContainsKey(rarity))
            rarityBiases[rarity] += weight;
        else
            rarityBiases[rarity] = weight;
    }

    /// <summary>
    /// Clear all biases.
    /// </summary>
    public void ClearBiases()
    {
        categoryBiases.Clear();
        rarityBiases.Clear();
    }

    /// <summary>
    /// Get a biased list of upgrades using weighted selection.
    /// </summary>
    public List<ItemData> GetBiasedUpgrades(List<ItemData> allUpgrades, int desiredCount = -1)
    {
        if (allUpgrades.Count == 0) return new List<ItemData>(allUpgrades);

        // Calculate weights for each upgrade
        List<(ItemData item, float weight)> weightedList = new List<(ItemData, float)>();
        foreach (var upgrade in allUpgrades)
        {
            float weight = 1f; // Base weight

            // Category bias
            ItemCategory? cat = null;
            if (upgrade is WeaponData w) cat = w.Category;
            else if (upgrade is PassiveData p) cat = p.Category;
            if (cat.HasValue && categoryBiases.ContainsKey(cat.Value))
                weight *= categoryBiases[cat.Value];

            // Rarity bias
            if (rarityBiases.ContainsKey(upgrade.rarity))
                weight *= rarityBiases[upgrade.rarity];

            weightedList.Add((upgrade, weight));
        }

        // Select items using weighted random
        List<ItemData> result = new List<ItemData>();
        int count = desiredCount > 0 ? desiredCount : allUpgrades.Count;
        for (int i = 0; i < count && weightedList.Count > 0; i++)
        {
            float totalWeight = 0;
            foreach (var item in weightedList) totalWeight += item.weight;

            float rand = Random.Range(0f, totalWeight);
            float cumulative = 0;
            for (int j = 0; j < weightedList.Count; j++)
            {
                cumulative += weightedList[j].weight;
                if (rand <= cumulative)
                {
                    result.Add(weightedList[j].item);
                    weightedList.RemoveAt(j);
                    break;
                }
            }
        }

        return result;
    }

    // Public getters for biases (for capping and debugging)
    public Dictionary<ItemCategory, float> GetCategoryBiases() => categoryBiases;
    public Dictionary<Rarity, float> GetRarityBiases() => rarityBiases;
}
