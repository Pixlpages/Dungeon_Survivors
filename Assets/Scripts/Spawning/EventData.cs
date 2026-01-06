using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EventData : SpawnData
{
    public enum GamePhase { Early, Mid, Late }
    public enum Difficulty { Easy, Medium, Hard }
    [Header("Event Classification")]
    public GamePhase phase;
    public Difficulty difficulty;

    [Header("Event Data")]
    [Range(0f, 1f)] public float probability = 1f; //whether this event occurs
    [Range(0f, 1f)] public float luckFactor = 1f; //how much luck affects the probability of this event

    [Tooltip("If a value is Specified, this event will only occur after the level runs for this number of seconds")]
    public float activeAfter = 0;

    public abstract bool Activate(PlayerStats player = null);

    //checks whether this event is currently active
    public bool IsActive()
    {
        if (!GameManager.Instance)
            return false;

        if (GameManager.Instance.GetElapsedTime() > activeAfter)
            return true;

        return false;
    }

    //calculates a random probability of this event happening
    public bool CheckIfWillHappen(PlayerStats s)
    {
        //probability of 1 means it always happens
        if (probability >= 1)
            return true;

        //otherwise, get a random number and see if we pass the probability test
        //if implementing MDP, remove luck factor, maybe
        if (probability / Mathf.Max(1, luckFactor) >= Random.Range(0f, 1f))
            return true;

        return false;
    }
}
