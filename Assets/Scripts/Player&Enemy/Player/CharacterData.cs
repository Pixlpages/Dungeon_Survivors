using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "NewScriptables/Character")]
public class CharacterData : ScriptableObject
{
    [SerializeField]
    Sprite icon;
    public Sprite Icon { get => icon; private set => icon = value; }

    public RuntimeAnimatorController controller;

    [SerializeField]
    new string name;
    public string Name { get => name; private set => name = value; }

    [SerializeField]
    WeaponData startingWeapon;
    public WeaponData StartingWeapon { get => startingWeapon; private set => startingWeapon = value; }

    [System.Serializable]
    public struct Stats
    {
        public float maxHealth, recovery, armor;
        [Range(-1, 10)] public float moveSpeed, strength, area;
        [Range(-1, 5)] public float speed, duration;
        [Range(-1, 10)] public int amount;
        [Range(-1, 10)] public float cooldown;
        [Min(-1)] public float curse;
        public float magnet;

        public static Stats operator +(Stats s1, Stats s2)
        {
            s1.maxHealth += s2.maxHealth;
            s1.recovery += s2.recovery;
            s1.armor += s2.armor;
            s1.moveSpeed += s2.moveSpeed;
            s1.speed += s2.speed;
            s1.strength += s2.strength;
            s1.area += s2.area;
            s1.amount += s2.amount;
            s1.cooldown += s2.cooldown;
            s1.curse += s2.curse;
            s1.magnet += s2.magnet;

            return s1;
        }
        
        public static Stats operator *(Stats s1, Stats s2)
        {
            s1.maxHealth *= s2.maxHealth;
            s1.recovery *= s2.recovery;
            s1.armor *= s2.armor;
            s1.moveSpeed *= s2.moveSpeed;
            s1.speed *= s2.speed;
            s1.strength *= s2.strength;
            s1.area *= s2.area;
            s1.amount *= s2.amount;
            s1.cooldown *= s2.cooldown;
            s1.curse *= s2.curse;
            s1.magnet *= s2.magnet;
            
            return s1;
        }
    }
    public Stats stats = new Stats
    {
        maxHealth = 100, moveSpeed = 1, strength = 1, amount = 0, area = 1, speed = 1, duration = 1, cooldown = 1, curse = 1
    };
}
