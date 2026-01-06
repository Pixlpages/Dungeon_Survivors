using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//class that takes a PassiveData and is used to increment player's stats recieved
public class Passive : Item
{
    [SerializeField] CharacterData.Stats currentBoosts;

    [System.Serializable]
    public class Modifier : LevelData
    {
        public CharacterData.Stats boosts;
    }

    public virtual void Initialise(PassiveData data) //initialise to dynamically create passives
    {
        base.Initialise(data);
        this.data = data;
        currentBoosts = data.baseStats.boosts;
    }

    public virtual CharacterData.Stats GetBoosts()
    {
        return currentBoosts;
    }

    //levels up weapon by 1, and calculates the corresponding stats
    public override bool DoLevelUp()
    {
        base.DoLevelUp();

        //prevent level up if a max level
        if (!CanLevelUp())
        {
            Debug.LogWarning(string.Format("Cannot level up from {0} to Level {1}, max level of {2} has been reached", name, currentLevel, data.maxLevel));
            return false;
        }

        // Increment the PPM tracker when passive is leveled up
        if (data is PassiveData passiveData)
        {
            PredictivePlayerModel.Instance?.RecordItemCategory(passiveData.category);
        }

        //otherwise add stats of the next level to weapon
        currentBoosts += ((Modifier) data.GetLevelData(++currentLevel)).boosts;
        return true;
    }
}
