using System.Collections.Generic;
using UnityEngine;

// PredictivePlayerModel
// - EMA-based predictions for DPS, damage-taken-rate and KPS
// - Rolling 30s windows for "recent" metrics (uses queues of timestamped events)
// - Session totals preserved (totalDamageDealt, totalDamageTaken, totalEnemiesKilled)
// - Configurable smoothing and floors to avoid negative/unstable predictions
public class PredictivePlayerModel : MonoBehaviour
{
    public static PredictivePlayerModel Instance;

    public PlayerProfile CurrentProfile = new PlayerProfile();

    [Header("Debug (Inspector View)")]
    [SerializeField] private PlayerProfile debugProfile;

    [Header("Prediction / Window Settings")]
    [Tooltip("Seconds used for short-term rolling window")]
    [SerializeField ]public float recentWindowSeconds = 15f;

    [Range(0f, 1f), Tooltip("EMA smoothing alpha for DPS (higher -> more responsive)")]
    [SerializeField] public float alphaDPS;

    [Range(0f, 1f), Tooltip("EMA smoothing alpha for Damage Taken Rate")]
    [SerializeField] public float alphaDamage;

    [Range(0f, 1f), Tooltip("EMA smoothing alpha for KPS")]
    [SerializeField] public float alphaKPS;

    [Tooltip("Minimum fraction of session-average to keep predictions from collapsing")]
    [Range(0f, 1f)]
    [SerializeField] public float predictionFloorFraction;

    // --- Rolling event buffers ---
    private Queue<(float time, float dmg)> damageTakenEvents = new Queue<(float time, float dmg)>();
    private Queue<(float time, float dmg)> damageDealtEvents = new Queue<(float time, float dmg)>();

    // --- Small history/diagnostic queues ---
    private Queue<float> dpsHistory = new Queue<float>();
    private Queue<float> dmgRateHistory = new Queue<float>();
    private Queue<float> kpsHistory = new Queue<float>();
    private Queue<float> killEvents = new Queue<float>(); 
    private const int historyWindow = 10;

    // --- Session totals ---
    private float totalDamageTaken = 0f;
    private float totalDamageDealt = 0f;
    private int totalEnemiesKilled = 0;
    private float totalTTK = 0f;

    // --- EMA state (internals) ---
    private float emaDPS = 0f;
    private float emaDamageRate = 0f; // damage taken per second (recent)
    private float emaKPS = 0f;

    // --- inventory / metadata trackers (unchanged) ---
    private Dictionary<string, int> weaponUsage = new Dictionary<string, int>();
    private Dictionary<string, int> passiveUsage = new Dictionary<string, int>();
    //private Dictionary<WeaponCategory, int> weaponCategoryUsage = new Dictionary<WeaponCategory, int>();
    //private Dictionary<PassiveCategory, int> passiveCategoryUsage = new Dictionary<PassiveCategory, int>();
    private Dictionary<ItemCategory, int> ItemCategoryUsage = new Dictionary<ItemCategory, int>();
    private Dictionary<Rarity, int> rarityUsage = new Dictionary<Rarity, int>();
    private HashSet<PlayerInventory> subscribedInventories = new HashSet<PlayerInventory>();

    // HP tracking exposed to player stats
    [HideInInspector] public float currentMaxHP = 100f;
    [HideInInspector] public float currentHP = 100f;

    // replayability
    private PlayerProfile lastProfile = null;

    // --Player Movement and Input Checking--
    private Queue<(float time, Vector2 dir, float mag)> movementSamples = new Queue<(float time, Vector2 dir, float mag)>();
    [SerializeField] private float movementWindow = 5f; // seconds to analyze input trends
    [SerializeField] private float movementUpdateInterval = 0.1f; // frequency of tracking
    private float lastMoveCheck = 0f;
    public string CurrentMovement { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Subscribe to any existing PlayerInventory objects in the scene (if used)
        PlayerInventory[] all = FindObjectsOfType<PlayerInventory>();
        foreach (var inv in all)
            SubscribeToInventory(inv);
    }

    void Update()
    {
        UpdateProfile();
        PredictTrends();
        TrackMovementInput();
        debugProfile = CurrentProfile;
    }

    #region HP helpers
    public void UpdateMaxHP(float maxHP)
    {
        currentMaxHP = maxHP;
        if (currentHP > currentMaxHP)
            currentHP = currentMaxHP;
    }

    public void UpdateCurrentHP(float hp)
    {
        currentHP = Mathf.Clamp(hp, 0f, currentMaxHP);
    }

    public float GetHPPercent()
    {
        if (currentMaxHP <= 0f) return 0f;
        return Mathf.Clamp01(currentHP / currentMaxHP);
    }
    #endregion

    #region Inventory subscribe helpers
    public void SubscribeToInventory(PlayerInventory inv)
    {
        if (inv == null) return;
        if (subscribedInventories.Contains(inv)) return;

        inv.WeaponAdded += OnWeaponAdded;
        inv.PassiveAdded += OnPassiveAdded;
        subscribedInventories.Add(inv);
    }

    public void UnsubscribeFromInventory(PlayerInventory inv)
    {
        if (inv == null) return;
        if (!subscribedInventories.Contains(inv)) return;

        inv.WeaponAdded -= OnWeaponAdded;
        inv.PassiveAdded -= OnPassiveAdded;
        subscribedInventories.Remove(inv);
    }

    void OnDestroy()
    {
        foreach (var inv in subscribedInventories)
        {
            if (inv != null)
            {
                inv.WeaponAdded -= OnWeaponAdded;
                inv.PassiveAdded -= OnPassiveAdded;
            }
        }
        subscribedInventories.Clear();
    }

    private void OnWeaponAdded(WeaponData weapon)
    {
        if (weapon == null) return;
        if (!weaponUsage.ContainsKey(weapon.name)) weaponUsage[weapon.name] = 0;
        weaponUsage[weapon.name]++;
        if (!ItemCategoryUsage.ContainsKey(weapon.Category)) ItemCategoryUsage[weapon.Category] = 0;
        ItemCategoryUsage[weapon.Category]++;
        if (!rarityUsage.ContainsKey(weapon.Rarity)) rarityUsage[weapon.Rarity] = 0;
        rarityUsage[weapon.Rarity]++;
        UpdateProfile();
    }

    private void OnPassiveAdded(PassiveData passive)
    {
        if (passive == null) return;
        if (!passiveUsage.ContainsKey(passive.name)) passiveUsage[passive.name] = 0;
        passiveUsage[passive.name]++;
        if (!ItemCategoryUsage.ContainsKey(passive.Category)) ItemCategoryUsage[passive.Category] = 0;
        ItemCategoryUsage[passive.Category]++;
        if (!rarityUsage.ContainsKey(passive.Rarity)) rarityUsage[passive.Rarity] = 0;
        rarityUsage[passive.Rarity]++;
        UpdateProfile();
    }
    #endregion

    #region Recording events (call from Player/Enemy systems)
    // Call whenever the player takes damage (from PlayerStats.TakeDamage)
    public void RecordDamageTaken(float dmg)
    {
        if (dmg <= 0f) return;

        totalDamageTaken += dmg;
        float now = GameManager.Instance ? GameManager.Instance.GetElapsedTime() : Time.time;
        damageTakenEvents.Enqueue((now, dmg));

        // prune old events outside recentWindowSeconds
        while (damageTakenEvents.Count > 0 && damageTakenEvents.Peek().time < now - recentWindowSeconds)
            damageTakenEvents.Dequeue();

        // compute rolling lastWindow sum (damage taken)
        float lastSum = 0f;
        foreach (var e in damageTakenEvents) lastSum += e.dmg;

        // store rolling damage per second (consistent unit)
        CurrentProfile.dmgTakenRate = lastSum / recentWindowSeconds;

        UpdateProfile();
    }

    // Call whenever player deals damage to enemies (e.g. EnemyStats.TakeDamage)
    public void RecordDamageDealt(float dmg)
    {
        if (dmg <= 0f) return;

        totalDamageDealt += dmg;
        float now = GameManager.Instance ? GameManager.Instance.GetElapsedTime() : Time.time;
        damageDealtEvents.Enqueue((now, dmg));

        // prune old events
        while (damageDealtEvents.Count > 0 && damageDealtEvents.Peek().time < now - recentWindowSeconds)
            damageDealtEvents.Dequeue();

        // compute rolling DPS (sum/seconds)
        float lastSum = 0f;
        foreach (var e in damageDealtEvents) lastSum += e.dmg;
        CurrentProfile.rollingDPS = lastSum / recentWindowSeconds;

        UpdateProfile();
    }

    // Call on enemy kill to update TTK/kills
    public void RecordEnemyKill(float timeToKill)
    {
        totalEnemiesKilled++;
        totalTTK += timeToKill;

        float now = GameManager.Instance ? GameManager.Instance.GetElapsedTime() : Time.time;
        killEvents.Enqueue(now);

        // prune old kills outside recent window
        while (killEvents.Count > 0 && killEvents.Peek() < now - recentWindowSeconds)
            killEvents.Dequeue();

        // rolling KPS (kills per second, scaled)
        CurrentProfile.rollingKPS = killEvents.Count / recentWindowSeconds;

        UpdateProfile();
    }

    public void RecordItemCategory(ItemCategory category)
    {
        if (!ItemCategoryUsage.ContainsKey(category))
            ItemCategoryUsage[category] = 0;

        ItemCategoryUsage[category]++;
        UpdateProfile();
    }

    #endregion

    #region Prediction logic (EMA)
    void PredictTrends()
    {
        // Compute session-level metrics (safe)
        float elapsed = CurrentProfile.sessionTime > 0f
            ? CurrentProfile.sessionTime
            : (GameManager.Instance ? GameManager.Instance.GetElapsedTime() : Time.time);

        float sessionDPS = (elapsed > 0f) ? totalDamageDealt / elapsed : 0f;
        float sessionDamageRatePerSec = (elapsed > 0f) ? (totalDamageTaken / elapsed) : 0f; // per second
        float sessionKPS = (elapsed > 0f) ? (float)totalEnemiesKilled / elapsed : 0f;

        // --- DPS: hybrid of rolling and session ---
        float sourceDPS = (0.7f * CurrentProfile.rollingDPS) + (0.3f * sessionDPS);
        if (emaDPS == 0f) emaDPS = sourceDPS;
        emaDPS = EMA(alphaDPS, emaDPS, sourceDPS);

        // --- Damage Taken Rate: hybrid of rolling and session ---
        float sourceDamage = (0.7f * CurrentProfile.dmgTakenRate) + (0.3f * sessionDamageRatePerSec);
        if (emaDamageRate == 0f) emaDamageRate = sourceDamage;
        emaDamageRate = EMA(alphaDamage, emaDamageRate, sourceDamage);

        // --- KPS: hybrid of rolling and session ---
        float sourceKPS = (0.7f * CurrentProfile.rollingKPS) + (0.3f * sessionKPS);
        if (emaKPS == 0f) emaKPS = sourceKPS;
        emaKPS = EMA(alphaKPS, emaKPS, sourceKPS);

        // --- Floors so predictions never collapse to 0 or negative ---
        float dpsFloor = sessionDPS * predictionFloorFraction;
        float damageFloor = sessionDamageRatePerSec * predictionFloorFraction;
        float kpsFloor = sessionKPS * predictionFloorFraction;

        emaDPS = Mathf.Max(emaDPS, dpsFloor, 0f);
        emaDamageRate = Mathf.Max(emaDamageRate, damageFloor, 0f);
        emaKPS = Mathf.Max(emaKPS, kpsFloor, 0f);

        // --- Save into profile ---
        CurrentProfile.sessionDPS = sessionDPS;
        CurrentProfile.sessionDamageTakenRate = sessionDamageRatePerSec;
        CurrentProfile.sessionKPS = sessionKPS;

        CurrentProfile.predictedDPS = emaDPS;
        CurrentProfile.predictedDamageTakenRate = emaDamageRate;
        CurrentProfile.predictedKPS = emaKPS;

        // Optional debug history
        AddToHistory(dpsHistory, CurrentProfile.predictedDPS);
        AddToHistory(dmgRateHistory, CurrentProfile.predictedDamageTakenRate);
        AddToHistory(kpsHistory, CurrentProfile.predictedKPS);
    }


    // Exponential moving average helper
    private float EMA(float alpha, float prevEma, float sample)
    {
        return alpha * sample + (1f - alpha) * prevEma;
    }
    #endregion

    #region Profile building / bookkeeping
    void AddToHistory(Queue<float> q, float value)
    {
        if (q.Count >= historyWindow) q.Dequeue();
        q.Enqueue(value);
    }

    void UpdateProfile()
    {
        float elapsed = GameManager.Instance ? GameManager.Instance.GetElapsedTime() : 0f;
        CurrentProfile.sessionTime = elapsed;

        // session-level aggregates
        CurrentProfile.dps = (elapsed > 0f) ? totalDamageDealt / elapsed : 0f;
        CurrentProfile.sessionDamageTakenRate = (elapsed > 0f) ? totalDamageTaken / elapsed : 0f; // per second

        // avg TTK
        CurrentProfile.avgTTK = (totalEnemiesKilled > 0) ? totalTTK / totalEnemiesKilled : 0f;

        // KPS and totals
        CurrentProfile.kps = (elapsed > 0f) ? (float)totalEnemiesKilled / elapsed : 0f;
        CurrentProfile.totalKills = totalEnemiesKilled;

        // favored weapon/passives & categories
        CurrentProfile.favoredWeapon = GetHighestLevelWeapon();
        CurrentProfile.favoredPassive = GetHighestLevelPassive();
        CurrentProfile.favoredItemCategory = GetTopCategory(ItemCategoryUsage);
        CurrentProfile.rarityUsage = new Dictionary<Rarity, int>(rarityUsage);

        // time phase
        if (elapsed < 150f) CurrentProfile.timePhase = "Early";
        else if (elapsed < 390f && elapsed > 150f) CurrentProfile.timePhase = "Mid";
        else CurrentProfile.timePhase = "Late";

        // basic playstyle tag (you can tweak thresholds)
        if (CurrentProfile.avgTTK > 0 && CurrentProfile.avgTTK < 2f && CurrentProfile.dmgTakenRate > 10f)
            CurrentProfile.playstyle = "Aggressive";
        else if (CurrentProfile.dmgTakenRate < 5f && CurrentProfile.dps > 50f)
            CurrentProfile.playstyle = "Efficient";
        else if (CurrentProfile.dmgTakenRate < 5f)
            CurrentProfile.playstyle = "Defensive";
        else
            CurrentProfile.playstyle = "Balanced";

        // Note: predicted values are updated in PredictTrends()
    }
    #endregion

    void TrackMovementInput()
    {
        if (Time.time - lastMoveCheck < movementUpdateInterval) return;
        lastMoveCheck = Time.time;

        // Get 2D input from player
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 moveDir = new Vector2(h, v);

        float speed = moveDir.magnitude;

        // Save latest sample for pattern analysis
        if (speed > 0.01f)
        {
            moveDir.Normalize();
            movementSamples.Enqueue((Time.time, moveDir, speed));
        }

        // Remove old samples outside the movement window
        while (movementSamples.Count > 0 && movementSamples.Peek().time < Time.time - movementWindow)
            movementSamples.Dequeue();

        if (movementSamples.Count == 0)
        {
            CurrentProfile.predictedMovementStyle = "Static";
            return;
        }

        // --- Determine if movement is consistent or erratic ---
        Vector2 avgDir = Vector2.zero;
        float totalSpeed = 0f;

        foreach (var s in movementSamples)
        {
            avgDir += s.dir;
            totalSpeed += s.mag;
        }

        avgDir.Normalize();
        float avgSpeed = totalSpeed / movementSamples.Count;

        // Measure directional consistency (dot product variance)
        float dirVariance = 0f;
        foreach (var s in movementSamples)
            dirVariance += 1f - Vector2.Dot(s.dir, avgDir);
        dirVariance /= movementSamples.Count;

        // --- Interpret Style ---
        string style;

        if (speed < 0.05f)
        {
            style = "Static"; // Player standing still
            CurrentMovement = "Static"; // NEW

        }
        else if (dirVariance < 0.2f)
        {
            style = "Aggressive"; // Moving mostly in a single direction
            CurrentMovement = "Aggressive";// NEW
        }
        else
        {
            style = "Erratic"; // Changing direction constantly
            CurrentMovement = "Erratic";// NEW
        }

        // Optional: add direction string for debug (e.g., "Aggressive (Right)")
        Vector2 latestDir = movementSamples.Count > 0 ? movementSamples.Peek().dir : Vector2.zero;
        string directionText = "";

        if (style == "Aggressive")
        {
            if (Mathf.Abs(latestDir.x) > Mathf.Abs(latestDir.y))
                directionText = latestDir.x > 0 ? "→ Right" : "← Left";
            else
                directionText = latestDir.y > 0 ? "↑ Up" : "↓ Down";
        }

        CurrentProfile.predictedMovementStyle = directionText == "" ? style : $"{style} {directionText}";
        CurrentProfile.avgMovementSpeed = avgSpeed;
        CurrentProfile.avgMovementDir = avgDir;
    }



    #region Utility getters used elsewhere
    public int GetTotalEnemiesKilled() => totalEnemiesKilled;
    public float GetTotalDamageDealt() => totalDamageDealt;
    public float GetTotalDamageTaken() => totalDamageTaken;
    #endregion

    #region Helpers for weapon/passive introspection
    private string GetHighestLevelWeapon()
    {
        int bestLevel = -1;
        string bestWeaponName = null;
        foreach (var inv in subscribedInventories)
        {
            if (inv == null) continue;
            var weapons = inv.GetAllWeapons();
            if (weapons == null) continue;
            foreach (var weapon in weapons)
            {
                if (weapon == null || weapon.data == null) continue;
                int lvl = weapon.currentLevel;
                string name = weapon.data.name;
                if (lvl > bestLevel) { bestLevel = lvl; bestWeaponName = name; }
                else if (lvl == bestLevel && bestWeaponName != null)
                {
                    int cur = weaponUsage.ContainsKey(name) ? weaponUsage[name] : 0;
                    int best = weaponUsage.ContainsKey(bestWeaponName) ? weaponUsage[bestWeaponName] : 0;
                    if (cur > best) bestWeaponName = name;
                }
            }
        }
        return bestWeaponName;
    }

    private string GetHighestLevelPassive()
    {
        int bestLevel = -1;
        string bestPassiveName = null;
        ItemCategory bestPassiveCategory = default;
        foreach (var inv in subscribedInventories)
        {
            if (inv == null) continue;
            var passives = inv.GetAllPassives();
            if (passives == null) continue;
            foreach (var passive in passives)
            {
                if (passive == null || passive.data == null) continue;
                PassiveData pData = passive.data as PassiveData;
                if (pData == null) continue;
                int lvl = passive.currentLevel;
                string name = pData.name;
                var category = pData.Category;
                if (lvl > bestLevel) { bestLevel = lvl; bestPassiveName = name; bestPassiveCategory = category; }
                else if (lvl == bestLevel && bestPassiveName != null)
                {
                    int cur = passiveUsage.ContainsKey(name) ? passiveUsage[name] : 0;
                    int best = passiveUsage.ContainsKey(bestPassiveName) ? passiveUsage[bestPassiveName] : 0;
                    if (cur > best) { bestPassiveName = name; bestPassiveCategory = category; }
                }
            }
        }
        return bestPassiveName;
    }

    T GetTopCategory<T>(Dictionary<T, int> dict)
    {
        if (dict.Count == 0) return default;
        T top = default;
        int max = -1;
        foreach (var kv in dict)
            if (kv.Value > max) { max = kv.Value; top = kv.Key; }
        return top;
    }
    #endregion

    #region Reset / Run lifecycle
    public void StartRun(bool continueFromPrevious)
    {
        if (continueFromPrevious && lastProfile != null)
            CurrentProfile = lastProfile;
        else
            CurrentProfile = new PlayerProfile();

        ResetSessionCounters();
    }

    void ResetSessionCounters()
    {
        totalDamageTaken = 0f;
        totalDamageDealt = 0f;
        totalEnemiesKilled = 0;
        totalTTK = 0f;

        damageTakenEvents.Clear();
        damageDealtEvents.Clear();

        emaDPS = emaDamageRate = emaKPS = 0f;

        weaponUsage.Clear();
        passiveUsage.Clear();
        ItemCategoryUsage.Clear();
        rarityUsage.Clear();
    }

    public void EndRun(bool keepProfile)
    {
        if (keepProfile)
        {
            lastProfile = CurrentProfile;
            lastProfile.continuedFromPrevious = true;
            lastProfile.replayCount++;
        }
        else lastProfile = null;
    }

    public void ResetSession()
    {
        ResetSessionCounters();
        CurrentProfile = new PlayerProfile();
    }
    #endregion
    
}
