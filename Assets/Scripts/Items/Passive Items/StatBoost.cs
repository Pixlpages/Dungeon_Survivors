using UnityEngine;

public class StatBoost : Item
{
    private CharacterData.Stats currentBoosts;
    private StatBoostData statBoostData;

    public override void Initialise(ItemData data)
    {
        base.Initialise(data);
        statBoostData = (StatBoostData)data;
        currentBoosts = statBoostData.GetBoost(currentLevel);
    }

    public override bool DoLevelUp()
    {
        base.DoLevelUp();

        if (!CanLevelUp())
            return false;

        currentLevel++;
        currentBoosts += statBoostData.GetBoost(currentLevel);

        // Update the player's stats when leveled
        if (owner != null)
            owner.RecalculateStats();

        return true;
    }

    public CharacterData.Stats GetBoosts() => currentBoosts;

    public override void OnEquip()
    {
        if (owner != null)
            owner.RecalculateStats();
    }

    public override void OnUnequip()
    {
        if (owner != null)
            owner.RecalculateStats();
    }
}
