using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerProfile
{
    [Header("Time")]
    [Tooltip("Time elapsed")]
    public float sessionTime;

    [Header("Damage Recieved")]
    [Tooltip("damage taken in short-term rolling 30s window")]
    public float dmgTakenRate;
    [Tooltip("session-long average damage taken normalized into 'per 30s'")]
    public float sessionDamageTakenRate;
    [Tooltip("Forcasted damage taken")]
    public float predictedDamageTakenRate;


    [Header("DPS")]
    [Tooltip("damage per second")]
    public float dps;
    [Tooltip("recent DPS (last 30s)")]
    public float rollingDPS;
    public float sessionDPS;
    [Tooltip("Forecasted DPS")]
    public float predictedDPS;
    [Tooltip("time to kill since the first hit")]

    [Header("kills/s, TTK")]
    public float avgTTK;
    [Tooltip("kills per second")]
    public float kps;
    [Tooltip("kills per second (last 30s window)")]
    public float rollingKPS;
    [Tooltip("session-long kills/sec")]
    public float sessionKPS;
    [Tooltip("Forecasted KPS")]
    public float predictedKPS;
    public int totalKills;

    [Header("Playstyle, Inventory")]
    public string predictedPlaystyle; //old string for logging
    //public WeaponCategory? predictedWeaponCategory;
    //public PassiveCategory? predictedPassiveCategory;
    public ItemCategory? predictedItemCategory;
    

    public float CurrentHP { get; private set; }
    public float MaxHP { get; private set; }

    public void UpdateHP(float current, float max)
    {
        CurrentHP = current;
        MaxHP = max;
    }
    public float GetHPPercent()
    {
        if (MaxHP <= 0f) return 0f;
        return Mathf.Clamp01(CurrentHP / MaxHP);
    }

    // weapon/passive preferences
    public string favoredWeapon;
    public string favoredPassive;
    public ItemCategory favoredItemCategory;
    //public WeaponCategory favoredWeaponCategory;
    //public PassiveCategory favoredPassiveCategory;
    public Dictionary<Rarity,int> rarityUsage;

    // overall playstyle tag
    public string playstyle;
    public string timePhase;

    //Replayability
    public bool continuedFromPrevious = false; // true if profile was carried over
    public int replayCount = 0;

    //Movement
    [Header("Movement Prediction")]
    [Tooltip("Predicted movement style based on recent input pattern")]
    public string predictedMovementStyle;

    [Tooltip("Rolling average of movement direction vectors")]
    public Vector3 avgMovementDir;

    [Tooltip("Rolling average of movement magnitude (speed)")]
    public float avgMovementSpeed;


    public PlayerProfile()
    {
        sessionTime = 0f;
        avgTTK = 0f;
        dmgTakenRate = 0f;
        dps = 0f;

        timePhase = "Early";
        favoredWeapon = null;
        //favoredWeaponCategory = default;
        //favoredPassiveCategory = default;
        favoredItemCategory = default;
        playstyle = "Balanced";

        rarityUsage = new Dictionary<Rarity, int>();

        predictedDPS = 0f;
        predictedDamageTakenRate = 0f;
        predictedPlaystyle = "Balanced";
        //predictedWeaponCategory = default;
        predictedItemCategory = default;

        continuedFromPrevious = false;
        replayCount = 0;
    }
}
