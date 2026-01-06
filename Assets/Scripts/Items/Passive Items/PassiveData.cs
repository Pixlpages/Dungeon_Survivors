using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Replacement for passiveItemScriptableObject. Store all passive item level data in a single object

[CreateAssetMenu(fileName = "Passive Data", menuName = "NewScriptables/Passive")]
public class PassiveData : ItemData
{
    [Header("Prefab for Instantiation")]
    [SerializeField] public GameObject prefab;  // Add this field for the passive prefab

    [Header("Categorization for PPM")]
    [SerializeField] public ItemCategory category;
    public ItemCategory Category => category;

    public Passive.Modifier baseStats;
    public Passive.Modifier[] growth;

    public override Item.LevelData GetLevelData(int level)
    {
        if (level <= 1)
            return baseStats;

        // Pick the stats from the next level
        if (level - 2 < growth.Length)
            return growth[level - 2];

        // Return an empty value and warning
        Debug.LogWarning(string.Format("Passive doesnt have its level up stats configured to level {0}", level));
        return new Passive.Modifier();
    }
}
