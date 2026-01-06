using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerStats : EntityStats
{
    CharacterData characterData;
    public CharacterData.Stats baseStats;
    [SerializeField] CharacterData.Stats actualStats;

    public CharacterData.Stats Stats
    {
        get { return actualStats; }
        set { actualStats = value; }
    }

    public CharacterData.Stats Actual
    {
        get { return actualStats;  }
    }

    public float CurrentHealth
    {
        get { return health; }

        //if we try and set the current health, the UI interface on the pause screen will also be updated
        set
        {
            if (health != value)
            {
                health = value;
                UpdateHealthBar();
            }
        }
    }

    [Header("Visuals")]
    public ParticleSystem blockEffect;
    public ParticleSystem damageEffect;
    public ParticleSystem healEffect;

    //experience and level of player
    [Header("Experience/Level")]
    public int experience = 0;
    public int level = 1;
    public int experienceCap;

    [Header("UI")]
    public Image healthBar;
    public Image expBar;
    public TMP_Text levelText;

    [Header("Inventory")]
    PlayerInventory inventory;
    PlayerCollector collector;

    [Header("I-Frames")]
    public float invincibilityDuration;
    float invincibilityTimer;
    bool isInvincible;

    AgentAnimations playerAnimator;

    //class for defining a level range and the corresponding experience cap increase for that range
    [System.Serializable]
    public class LevelRange
    {
        public int startLevel;
        public int endLevel;
        public int experienceCapInrease;
    }

    public List<LevelRange> levelRanges;

    protected override void Start()
    {
        base.Start();
        //spawn the starting weapon
        inventory.Add(characterData.StartingWeapon);

        //initialize the experience cap as the firs experience cap increase
        experienceCap = levelRanges[0].experienceCapInrease;


        GameManager.Instance.AssignChosenCharacterUI(characterData);

        UpdateHealthBar();
        UpdateExpBar();
        UpdateLevelText();
    }

    void Awake()
    {
        characterData = CharacterSelector.GetData();
        
        if (CharacterSelector.instance)
            CharacterSelector.instance.DestroySingleton();

        inventory = GetComponent<PlayerInventory>();
        collector = GetComponentInChildren<PlayerCollector>();

        //asign the variables
        baseStats = actualStats = characterData.stats;
        collector.SetRadius(actualStats.magnet);
        health = actualStats.maxHealth;

        playerAnimator = GetComponent<AgentAnimations>();
        if(characterData.controller)
            playerAnimator.SetAnimatorController(characterData.controller);

    }

    protected override void Update()
    {
        base.Update();
        if (invincibilityTimer > 0)
        {
            invincibilityTimer -= Time.deltaTime;
        }
        else if (isInvincible)
        {
            isInvincible = false;
        }

        Recover();
    }

    public override void RecalculateStats()
    {
        actualStats = baseStats;
        foreach (PlayerInventory.Slot s in inventory.passiveSlots)
        {
            Passive p = s.item as Passive;
            if (p)
            {
                actualStats += p.GetBoosts();
            }
        }

        var appliedBoosts = inventory.GetAppliedStatBoostsPublic();
        if (appliedBoosts != null)
        {
            foreach (var ab in appliedBoosts)
            {
                if (ab == null || ab.data == null) continue;
                // ab.level is 1-based; make sure it's valid
                var boost = ab.data.GetBoost(ab.level);
                actualStats += boost;
            }
        }

        PredictivePlayerModel.Instance?.UpdateMaxHP(actualStats.maxHealth);
        PredictivePlayerModel.Instance?.UpdateCurrentHP(CurrentHealth);

        //Create a variable to store all the cumulative multiplier values
        CharacterData.Stats multiplier = new CharacterData.Stats
        {
            maxHealth = 1f,
            recovery = 1f,
            armor = 1f,
            moveSpeed = 1f,
            strength = 1f,
            area = 1f,
            speed = 1f,
            duration = 1f,
            amount = 1,
            cooldown = 1f,
            curse = 1f,
            magnet = 1f
        };

        foreach (Buff b in activeBuffs)
        {
            BuffData.Stats bd = b.GetData();
            switch (bd.modifierType)
            {
                case BuffData.ModifierType.additive:
                    actualStats += bd.playerModifier;
                    break;
                case BuffData.ModifierType.multiplicative:
                    multiplier *= bd.playerModifier;
                    break;
            }
        }
        actualStats *= multiplier;

        // apply curse bias from CurseManager
        if (CurseManager.Instance != null)
        {
            actualStats.curse *= (1f + CurseManager.Instance.GetCurseBias());
        }

        //update the PLayerCollector's radius
        collector.SetRadius(actualStats.magnet);
    }

    public void IncreaseExperience(int amount)
    {
        experience += amount;
        LevelUpChecker();
        UpdateExpBar();
    }

    void LevelUpChecker()
    {
        if (experience >= experienceCap)
        {
            level++;
            experience -= experienceCap;

            int experienceCapInrease = 0;
            foreach (LevelRange range in levelRanges)
            {
                if (level >= range.startLevel && level <= range.endLevel)
                {
                    experienceCapInrease = range.experienceCapInrease;
                    break;
                }
            }
            experienceCap += experienceCapInrease;

            UpdateLevelText();

            GameManager.Instance.StartLevelUp();

            if (experience >= experienceCap)
                LevelUpChecker();
        }
    }

    void UpdateExpBar()
    {
        expBar.fillAmount = (float)experience / experienceCap;
    }

    void UpdateLevelText()
    {
        levelText.text = "LV" + level.ToString();
    }

    public override void RestoreHealth(float amount)
    {
        //only heal if health less than max
        if (CurrentHealth < actualStats.maxHealth)
        {
            CurrentHealth += amount;

            //make sures that the health does not exceed max
            if (CurrentHealth > actualStats.maxHealth)
            {
                CurrentHealth = actualStats.maxHealth;
            }

            ParticleSystem ps = Instantiate(healEffect, transform.position, Quaternion.identity);
            ps.Play();
            Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
        }
    }

    void Recover() //recovers "currentRecovery" per second
    {
        if (CurrentHealth < actualStats.maxHealth)
        {
            CurrentHealth += Stats.recovery * Time.deltaTime;

            //making sure it does not overflow
            if (CurrentHealth > actualStats.maxHealth)
            {
                CurrentHealth = actualStats.maxHealth;
            }
            UpdateHealthBar();
            PredictivePlayerModel.Instance?.UpdateCurrentHP(CurrentHealth);
        }
    }

    public override void TakeDamage(float dmg)
    {
        if (!isInvincible)
        {
            //take armor into account before dealing damage
            dmg -= actualStats.armor;

            if (dmg > 0)
            {
                if (playerAnimator) playerAnimator.PlayHitAnimation();
                CurrentHealth -= dmg;
                StartCoroutine(ControllerVibration(0.5f, 1.0f, 0.3f)); // (low freq, high freq, duration in seconds)


                //PPM Report
                PredictivePlayerModel.Instance?.RecordDamageTaken(dmg);
                PredictivePlayerModel.Instance?.UpdateCurrentHP(CurrentHealth);

                if (damageEffect) // if there is a damage effect asssigned, play it
                {
                    ParticleSystem ps = Instantiate(damageEffect, transform.position, Quaternion.identity);
                    Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
                }

                if (CurrentHealth <= 0)
                {
                    Kill();
                }

                UpdateHealthBar();
            }
            else
            {
                //if there is a blocked effect assigned, play it
                if (blockEffect)
                    Destroy(Instantiate(blockEffect, transform.position, Quaternion.identity), 5f);
            }

            invincibilityTimer = invincibilityDuration;
            isInvincible = true;
        }
    }

    public void UpdateHealthBar()
    {
        healthBar.fillAmount = CurrentHealth / actualStats.maxHealth;
    }

    public override void Kill()
    {
        if (playerAnimator)
            playerAnimator.PlayDeathAnimation();

        // Disable controls immediately
        DisablePlayerControls();

        if (!GameManager.Instance.isGameOver)
        {
            GameManager.Instance.AssignLevelReachedUI(level);
            StartCoroutine(DelayedGameOver());
        }
    }

    private void DisablePlayerControls()
    {
        // Disable movement and related input
        var movement = GetComponent<PlayerMovement>();
        if (movement) movement.enabled = false;


        var rb = GetComponent<Rigidbody2D>();
        if (rb) rb.velocity = Vector2.zero; // stop moving immediately

        // Optional: disable input scripts or components like PlayerInput
        var input = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (input) input.enabled = false;
    }

    private IEnumerator DelayedGameOver()
    {
        // Wait 1 second (allows death animation to finish)
        yield return new WaitForSeconds(0.5f);

        if (!GameManager.Instance.isGameOver)
            GameManager.Instance.GameOver();
    }

    private IEnumerator ControllerVibration(float low, float high, float duration)
    {
        if (Gamepad.current != null)
        {
            Gamepad.current.SetMotorSpeeds(low, high);
            yield return new WaitForSeconds(duration);
            Gamepad.current.SetMotorSpeeds(0, 0); // stop vibration
        }
    }
    
}
