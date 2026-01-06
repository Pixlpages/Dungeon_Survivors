using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Stat Boost")]
public class StatBoostData : ItemData
{
    [System.Serializable]
    public class StatBoostLevelData : Item.LevelData
    {
        public CharacterData.Stats boost;
    }

    [SerializeField] private StatBoostLevelData[] levels;

    public override Item.LevelData GetLevelData(int level)
    {
        if (level <= 0) level = 1;
        int idx = level - 1;
        if (idx < 0 || idx >= levels.Length) return null;
        return levels[idx];
    }

    // safe getter by 1-based level
    public CharacterData.Stats GetBoost(int level)
    {
        if (level <= 0) level = 1;
        int idx = level - 1;
        if (idx < 0 || idx >= levels.Length) return default;
        return levels[idx].boost;
    }

    public int GetMaxLevel() => levels != null ? levels.Length : 0;
}
