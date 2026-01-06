using System.Collections.Generic;
using Unity.Collections;
//using UnityEditor.EditorTools;
using UnityEngine;

/// <summary>
/// True MDP manager (S, A, P, R, gamma) for Director-style decisions.
/// Calls Step(...) to take actions.
/// Uses PredictivePlayerModel.Instance.CurrentProfile if no profile is supplied.
/// </summary>
public class MDPManager : MonoBehaviour
{

    #region Enums (S and A)

    public enum GameState
    {
        Dominating,
        Relaxed,
        Neutral,
        Tense,
        Suffering
    }

    public enum GameAction
    {
        SpawnEnemies,
        TriggerMobEvent,
        AdjustLoot,
        AdjustCurse
    }

    #endregion

    #region Config and Initialization
    // -----------------------
    //  Config / runtime
    // -----------------------
    public static MDPManager Instance { get; private set; }

    [Header("MDP Settings")]
    [Tooltip("The discount factor")]
    [Range(0f, 1f)] public float gamma = 0.9f; // future reward discount
    [Tooltip("Initial assumed state")]
    public GameState CurrentState = GameState.Neutral;

    [Header("Debug Info")]
    public bool enableDetailedLogs = false;
    [SerializeField, ReadOnly] public float lastReward = 0f;
    public float LastReward => lastReward;

    [Header("State smoothing")]
    [SerializeField, Tooltip("How long (s) a DERIVED state must persist before we accept the change")]
    public float stateConfirmTime = 2f;

    private GameState candidateState;
    private bool hasCandidate = false;
    private float candidateStartTime = 0f;

    // P(s' | s, a) stored as transitions[s][a] => dictionary of nextState->probability
    private Dictionary<GameState, Dictionary<GameAction, Dictionary<GameState, float>>> transitions;

    [Header("Director Hooks")]

    public SpawnManager spawnManager;
    public EventManager eventManager;
    public LootManager lootManager;
    public CurseManager curseManager;

    [Header("Standalone Mode")]
    public bool enableStandalone = true;  // Enable standalone MDP loop when combined is disabled

    // Pending decisions (MDP suggests, managers execute when ready)
    private WaveData pendingWave = null;
    private EventData pendingEvent = null;

    // Store last computed metrics for curse adjustment
    private float lastDpsRatio;
    private float lastDamageRatio;
    private float lastHpPercent;

    // Track last state for transition detection
    private GameState lastState;
    private float stateEnterTime = 0f;

    public WaveData GetPendingWave()
    {
        var w = pendingWave;
        pendingWave = null;
        return w;
    }

    public EventData GetPendingEvent()
    {
        var e = pendingEvent;
        pendingEvent = null;
        return e;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitializeTransitions();
        if (!spawnManager) spawnManager = FindObjectOfType<SpawnManager>();
        if (!eventManager) eventManager = FindObjectOfType<EventManager>();
    }

    void Update()
    {
        if (!enableStandalone) 
            return;
        var profile = PredictivePlayerModel.Instance?.CurrentProfile;
        if (profile == null) return;

        GameState observed = DeriveStateFromPPM(profile);

        // First-time baseline for lastState (no adjustment/logging yet)
        if (lastState.Equals(default(GameState)))
        {
            lastState = observed;
            stateEnterTime = Time.time;
        }

        // If observed == current -> reset candidate
        if (observed == CurrentState)
        {
            hasCandidate = false;
            return;
        }

        // New observed != current -> start/continue confirmation timer
        if (!hasCandidate || candidateState != observed)
        {
            candidateState = observed;
            candidateStartTime = Time.time;
            hasCandidate = true;
        }
        else
        {
            // observed persisted long enough → accept the change
            if (Time.time - candidateStartTime >= stateConfirmTime)
            {
                if (enableDetailedLogs) Debug.Log($"[MDP] Confirmed state change {CurrentState} -> {observed} after {stateConfirmTime}s");

                // Track confirmed transition baseline (optional, helps analytics/dwell-time)
                lastState = CurrentState;
                stateEnterTime = Time.time;

                // Instead of hardcoded SpawnEnemies, choose the best action:
                GameAction best = ChooseBestAction(profile);

                // perform step + apply effects
                var (next, reward, directive) = StepAndApply(best, profile, useMARL: false);

                // update debug reward
                lastReward = reward;

                // state already updated in Step()
                hasCandidate = false;
            }
        }
        
    }

    // New method: greedy action chooser
    public GameAction ChooseBestAction(PlayerProfile profile)
    {
        GameAction bestAction = GameAction.SpawnEnemies;
        float bestValue = float.NegativeInfinity;

        foreach (GameAction action in System.Enum.GetValues(typeof(GameAction)))
        {
            GameState next = GetNextState(CurrentState, action);

            // immediate reward
            float reward = GetReward(CurrentState, action, next, profile);

            // add discounted estimate of future value
            float futureValue = EstimateStateValue(next, profile);

            float totalValue = reward + gamma * futureValue;

            if (totalValue > bestValue)
            {
                bestValue = totalValue;
                bestAction = action;
            }
        }

        if (enableDetailedLogs) Debug.Log($"[MDP] Best action chosen: {bestAction} (value={bestValue:F2})");
        return bestAction;
    }

    private float EstimateStateValue(GameState state, PlayerProfile profile)
    {
        float best = float.NegativeInfinity;

        foreach (GameAction action in System.Enum.GetValues(typeof(GameAction)))
        {
            GameState next = GetNextState(state, action);
            float reward = GetReward(state, action, next, profile);
            if (reward > best) best = reward;
        }

        return best == float.NegativeInfinity ? 0f : best;
    }

    // -----------------------
    //  Transition initialization
    // -----------------------
    void InitializeTransitions()
    {
        transitions = new Dictionary<GameState, Dictionary<GameAction, Dictionary<GameState, float>>>();

        // Helper to ensure dictionary structure exists and add mapping
        void EnsureStateAction(GameState s, GameAction a)
        {
            if (!transitions.ContainsKey(s)) transitions[s] = new Dictionary<GameAction, Dictionary<GameState, float>>();
            if (!transitions[s].ContainsKey(a)) transitions[s][a] = new Dictionary<GameState, float>();
        }

        void AddTransitions(GameState s, GameAction a, params (GameState to, float prob)[] pairs)
        {
            EnsureStateAction(s, a);
            var dict = transitions[s][a];
            dict.Clear();

            // use clamped weights and sum those
            float clampedTotal = 0f;
            foreach (var (to, prob) in pairs)
            {
                float v = Mathf.Clamp01(prob);
                dict[to] = v;
                clampedTotal += v;
            }

            // if the listed probs don't add up to 1, give remainder to staying in same state
            if (clampedTotal < 0.999f)
            {
                dict[s] = dict.ContainsKey(s) ? dict[s] + (1f - clampedTotal) : (1f - clampedTotal);
            }

            // Normalize final dictionary values (guard against tiny rounding errors)
            float total = 0f;
            foreach (var kv in dict) total += kv.Value;
            if (total <= 0f)
            {
                dict.Clear();
                dict[s] = 1f;
            }
            else
            {
                List<GameState> keys = new List<GameState>(dict.Keys);
                foreach (var k in keys)
                    dict[k] = dict[k] / total;
            }
        }

        // ---------- Example transitions (tweak to taste) ----------
        // From Dominating
        AddTransitions(GameState.Dominating, GameAction.SpawnEnemies,
            (GameState.Dominating, 0.5f), (GameState.Relaxed, 0.3f), (GameState.Neutral, 0.2f));
        AddTransitions(GameState.Dominating, GameAction.TriggerMobEvent,
            (GameState.Dominating, 0.4f), (GameState.Tense, 0.4f), (GameState.Neutral, 0.2f));
        AddTransitions(GameState.Dominating, GameAction.AdjustLoot,
            (GameState.Dominating, 0.5f), (GameState.Relaxed, 0.3f), (GameState.Neutral, 0.2f));
        AddTransitions(GameState.Dominating, GameAction.AdjustCurse,
            (GameState.Dominating, 0.2f), (GameState.Neutral, 0.4f), (GameState.Tense, 0.4f));

        // From Relaxed
        AddTransitions(GameState.Relaxed, GameAction.SpawnEnemies,
            (GameState.Relaxed, 0.5f), (GameState.Neutral, 0.3f), (GameState.Tense, 0.2f));
        AddTransitions(GameState.Relaxed, GameAction.TriggerMobEvent,
            (GameState.Relaxed, 0.4f), (GameState.Tense, 0.4f), (GameState.Neutral, 0.2f));
        AddTransitions(GameState.Relaxed, GameAction.AdjustLoot,
            (GameState.Relaxed, 0.6f), (GameState.Dominating, 0.25f), (GameState.Neutral, 0.15f));
        AddTransitions(GameState.Relaxed, GameAction.AdjustCurse,
            (GameState.Relaxed, 0.3f), (GameState.Neutral, 0.4f), (GameState.Tense, 0.3f));


        // From Neutral
        AddTransitions(GameState.Neutral, GameAction.SpawnEnemies,
            (GameState.Neutral, 0.5f), (GameState.Tense, 0.3f), (GameState.Relaxed, 0.2f));
        AddTransitions(GameState.Neutral, GameAction.TriggerMobEvent,
            (GameState.Neutral, 0.4f), (GameState.Relaxed, 0.3f), (GameState.Tense, 0.3f));
        AddTransitions(GameState.Neutral, GameAction.AdjustLoot,
            (GameState.Neutral, 0.5f), (GameState.Relaxed, 0.3f), (GameState.Dominating, 0.2f));
        AddTransitions(GameState.Neutral, GameAction.AdjustCurse,
            (GameState.Dominating, 0.2f), (GameState.Neutral, 0.4f), (GameState.Tense, 0.4f));

        // From Tense
        AddTransitions(GameState.Tense, GameAction.SpawnEnemies,
            (GameState.Tense, 0.5f), (GameState.Suffering, 0.3f), (GameState.Neutral, 0.2f));
        AddTransitions(GameState.Tense, GameAction.TriggerMobEvent,
            (GameState.Tense, 0.4f), (GameState.Suffering, 0.4f), (GameState.Neutral, 0.2f));
        AddTransitions(GameState.Tense, GameAction.AdjustLoot,
            (GameState.Tense, 0.5f), (GameState.Neutral, 0.3f), (GameState.Relaxed, 0.2f));
        AddTransitions(GameState.Tense, GameAction.AdjustCurse,
            (GameState.Tense, 0.4f), (GameState.Suffering, 0.4f), (GameState.Neutral, 0.2f));

        // From Suffering
        AddTransitions(GameState.Suffering, GameAction.SpawnEnemies,
            (GameState.Suffering, 0.6f), (GameState.Tense, 0.3f), (GameState.Neutral, 0.1f));
        AddTransitions(GameState.Suffering, GameAction.TriggerMobEvent,
            (GameState.Suffering, 0.7f), (GameState.Tense, 0.3f));
        AddTransitions(GameState.Suffering, GameAction.AdjustLoot,
            (GameState.Suffering, 0.5f), (GameState.Relaxed, 0.3f), (GameState.Neutral, 0.2f));
        AddTransitions(GameState.Suffering, GameAction.AdjustCurse,
            (GameState.Suffering, 0.5f), (GameState.Tense, 0.3f), (GameState.Neutral, 0.2f));

    }

    #endregion

    #region CoreMDP

    // -----------------------
    //  Core MDP methods
    // -----------------------

    /// <summary>
    /// Sample next state given current state and action using P(s'|s,a).
    /// If transitions missing -> returns same state.
    /// </summary>
    public GameState GetNextState(GameState s, GameAction a)
    {
        if (!transitions.ContainsKey(s) || !transitions[s].ContainsKey(a))
            return s;

        var probs = transitions[s][a];

        float r = Random.value;
        float cumulative = 0f;
        foreach (var kv in probs)
        {
            cumulative += kv.Value;
            if (r <= cumulative) return kv.Key;
        }

        // fallback: return same
        return s;
    }

    /// <summary>
    /// Reward function R(s, a, s', profile)
    /// Heuristic; adjust or make data-driven as needed.
    /// </summary>
    public float GetReward(GameState s, GameAction a, GameState sPrime, PlayerProfile profile = null)
    {
        if (profile == null && PredictivePlayerModel.Instance != null)
            profile = PredictivePlayerModel.Instance.CurrentProfile;

        float reward = 0f;

        // Determine phase
        float elapsed = profile != null ? profile.sessionTime : 0f;
        bool early = elapsed < 150f;
        bool mid = elapsed >= 150f && elapsed < 390f;
        bool late = elapsed >= 390f;

        // Encourage states near Neutral/Tense (engaging but fair)
        if (sPrime == GameState.Neutral) reward += 3.5f;
        if (sPrime == GameState.Tense) reward += 5f;

        // Penalize extremes
        if (sPrime == GameState.Dominating) reward -= 4f; // player might be bored 
        if (sPrime == GameState.Suffering) reward -= 4f;  // too punishing 

        // Extra: if AdjustLoot and player likes predicted category, big positive
        if (a == GameAction.AdjustLoot && profile != null)
        {
            if (profile.predictedItemCategory == profile.favoredItemCategory)
                reward += 6f;
            else
                reward += 2f; // still small positive if giving loot
        }

        // Phase-based Prioritizations
        if (a == GameAction.AdjustCurse)
        {
            float baseCurseReward = 0f;
            if (s == GameState.Dominating) baseCurseReward = 4.5f;
            if (s == GameState.Relaxed) baseCurseReward = 3f;
            if (s == GameState.Suffering) baseCurseReward = 2.5f;
            if (s == GameState.Neutral) baseCurseReward = 1.0f;

            // Phase-based scaling: Early deprioritizes curse, late prioritizes
            if (early) reward += baseCurseReward * 0.5f;  // Low priority (e.g., 2.25 in Dominating)
            else if (mid) reward += baseCurseReward;      // Balanced
            else if (late) reward += baseCurseReward * 3.0f;  // High priority (e.g., 9.0 in Dominating)
        }

        if (a == GameAction.SpawnEnemies)
        {
            float baseSpawnReward = 4f;  // Base for spawning
            if (early) reward += baseSpawnReward * 1.5f;  // High priority (6.0) - Build early tension
            else if (mid) reward += baseSpawnReward;      // Balanced (4.0)
            else if (late) reward += baseSpawnReward * 0.5f;  // Low priority (2.0) - Let curse handle late difficulty
        }

        if (a == GameAction.TriggerMobEvent)
        {
            float baseEventReward = 3.5f;  // Base for events
            if (early) reward += baseEventReward * 1.2f;  // Moderate priority (4.2) - Add variety early
            else if (mid) reward += baseEventReward;      // Balanced (3.5)
            else if (late) reward += baseEventReward * 0.8f;  // Slight priority (2.8) - Events for late-game spice
        }

        // Reward survival progress (small)
        if (a == GameAction.AdjustLoot)
        {
            float baseLootReward = 4f;  // Base for loot
            if (early) reward += baseLootReward * 1.5f;  // High priority (6.0)
            else if (mid) reward += baseLootReward;      // Balanced (4.0)
            else if (late) reward += baseLootReward * 0.5f;  // Low priority (2.0)
        }
        // Survival progress and costs
        if (profile != null)
        {
            reward += Mathf.Log(1 + profile.sessionTime);
        }
        reward -= 0.2f;  // Small cost per action
        return reward;
        }
        

    /// <summary>
    /// Perform one MDP step: take an action, sample next state using P, compute reward.
    /// Returns (nextState, reward).
    /// Note: action effects on the game (spawning, loot) should be performed by the director after Step
    /// or by extending this method to call spawner/inventory hooks.
    /// </summary>
    public (GameState nextState, float reward) Step(GameAction action, PlayerProfile profile = null)
    {
        GameState prev = CurrentState;
        GameState next = GetNextState(prev, action);

        float reward = GetReward(prev, action, next, profile);

        // store for debug
        lastReward = reward;
        CurrentState = next;

        if (enableDetailedLogs) Debug.Log($"[MDP] {prev} + {action} => {next}  (reward={reward:F2})");

        return (next, reward);
    }

    #region StepAndApply
    // Perform one step and also trigger game systems
    public (GameState nextState, float reward, GameDirective directive) StepAndApply(GameAction action, PlayerProfile profile = null, bool useMARL = false)
    {
        // Step 1: Run base MDP transition logic
        var (nextState, reward) = Step(action, profile);

        // Step 2: Trigger game systems based on action
        switch (action)
        {
            case GameAction.SpawnEnemies:
                if (spawnManager != null)
                {
                    if (enableDetailedLogs) Debug.Log("[MDP] Spawning enemies for state " + nextState);
                    QueueWaveForState(nextState);
                }
                break;

            case GameAction.TriggerMobEvent:
                if (eventManager != null)
                {
                    if (enableDetailedLogs) Debug.Log("[MDP] Triggering event via EventManager");
                    QueueEventForState(nextState);
                }
                break;

            case GameAction.AdjustLoot:
                if (lootManager != null && profile != null)
                {
                    if (enableDetailedLogs) Debug.Log("[MDP] Adjusting loot for state " + nextState);
                    ApplyLootBias(nextState, profile);
                }
                break;

            case GameAction.AdjustCurse:
                if (curseManager != null)
                {
                    if (enableDetailedLogs) Debug.Log("[MDP] Adjusting curse for state " + nextState);
                    ApplyCurseAdjustment(nextState);
                }
                break;
        }

        // Step 3-7: Only apply MARL adjustments if in combined mode
        if (useMARL && MARLManager.Instance != null)
        {
            // Normalize MARL reward (centered at 10)
            float normalized = MARLManager.Instance.LastGlobalReward - 10f;

            // Step 4: Adjust nextState based on MARL performance
            if (normalized < -2f)
            {
                if (enableDetailedLogs) Debug.Log("[MDP] MARL underperforming — suppressing escalation");
                switch (CurrentState)
                {
                    case GameState.Dominating: nextState = GameState.Relaxed; break;
                    case GameState.Relaxed: nextState = GameState.Neutral; break;
                    case GameState.Neutral: nextState = GameState.Tense; break;
                    case GameState.Tense: nextState = GameState.Suffering; break;
                    default: nextState = GameState.Suffering; break;
                }
            }
            else if (normalized > 2f)
            {
                if (enableDetailedLogs) Debug.Log("[MDP] MARL overperforming — accelerating pacing");
                switch (nextState)
                {
                    case GameState.Suffering: nextState = GameState.Tense; break;
                    case GameState.Tense: nextState = GameState.Neutral; break;
                    case GameState.Neutral: nextState = GameState.Relaxed; break;
                    case GameState.Dominating: nextState = GameState.Dominating; break;
                    default: nextState = GameState.Dominating; break;
                }
            }

            // Step 5: Adjust curse based on MARL performance
            float curse = GetCurseAdjustmentForState(nextState);
            if (normalized < -2f)
            {
                curse -= 0.1f;
                if (enableDetailedLogs) Debug.Log("[MDP] MARL underperforming — reducing curse by 0.1");
            }
            else if (normalized > 2f)
            {
                curse += 0.1f;
                if (enableDetailedLogs) Debug.Log("[MDP] MARL overperforming — increasing curse by 0.1");
            }

            // Step 6: Adjust loot bias based on MARL performance
            LootBias loot = GetLootBiasForState(nextState);
            if (normalized < -2f)
            {
                loot = LootBias.Survival;
                if (enableDetailedLogs) Debug.Log("[MDP] MARL underperforming — overriding loot bias to Survival");
            }
            else if (normalized > 2f)
            {
                loot = LootBias.DPS;
                if (enableDetailedLogs) Debug.Log("[MDP] MARL overperforming — overriding loot bias to DPS");
            }
            else
            {
                loot = LootBias.Balanced;
            }

            // Step 7: Construct directive using final nextState and adjusted values
            GameDirective directive = new GameDirective(
                target: (TargetState)nextState,
                loot: loot,
                wave: GetWaveTypeForState(nextState),
                curse: curse,
                action: action
            );

            return (nextState, reward, directive);
        }
        else
        {
            // Standalone mode: No MARL adjustments, use default values
            LootBias loot = GetLootBiasForState(nextState);
            float curse = GetCurseAdjustmentForState(nextState);
            WaveType wave = GetWaveTypeForState(nextState);

            GameDirective directive = new GameDirective(
                target: (TargetState)nextState,
                loot: loot,
                wave: wave,
                curse: curse,
                action: action
            );

            return (nextState, reward, directive);
        }
    }
    #endregion

    #region State Derivation from PPM
    public GameState DeriveStateFromPPM(PlayerProfile profile)
    {
        float hpPercent = PredictivePlayerModel.Instance.GetHPPercent();

        // Ratios relative to session averages
        float dpsRatio = profile.predictedDPS / (profile.sessionDPS + 0.01f);
        float damageRatio = profile.predictedDamageTakenRate / (profile.sessionDamageTakenRate + 0.01f);

        // Save for curse adjustment
        lastDpsRatio = dpsRatio;
        lastDamageRatio = damageRatio;
        lastHpPercent = hpPercent;

        // --- DOMINATING ---
        // Untouchable or overwhelming offense
        if ((dpsRatio > 1.1f && damageRatio < 0.9f && hpPercent > 0.75f) ||   // Offensive dominance
            (hpPercent > 0.9f && damageRatio < 0.7f))                        // Defensive dominance
            return GameState.Dominating;

        // --- RELAXED ---
        // In control, but not untouchable
        if ((dpsRatio > 1.0f && damageRatio < 1.0f && hpPercent > 0.75f) ||   // Active control
            (hpPercent > 0.85f && damageRatio < 0.7f) ||                     // Untouchable sustain
            (hpPercent > 0.9f && damageRatio < 0.7f && dpsRatio < 0.8f))     // DPS starvation (high hp, low dps due to waiting for enemy to spawn)
            return GameState.Relaxed;

        // --- TENSE ---
        // Yellow zone: pressured but not collapsing
        if ((damageRatio > 1.2f && hpPercent < 0.7f) ||   // Path A: fighting through, HP dropping
            (hpPercent < 0.5f && dpsRatio < 0.9f))       // Path B: retreating, fragile and output low
            return GameState.Tense;

        // --- SUFFERING ---
        // Near-death, critical danger regardless of DPS
        if (hpPercent < 0.20f || (damageRatio > 1.8f && hpPercent < 0.3f))
            return GameState.Suffering;

        // --- DEFAULT ---
        return GameState.Neutral;
    }
    #endregion

    #endregion

    #region Enemy Spawning

    private void QueueWaveForState(GameState state, PlayerProfile profile = null)
    {
        if (spawnManager == null) return;

        float elapsed = profile != null ? profile.sessionTime : GameManager.Instance.GetElapsedTime();

        WaveData.GamePhase phase = elapsed < 150f ? WaveData.GamePhase.Early :
                                   elapsed < 390f && elapsed > 150f ? WaveData.GamePhase.Mid :
                                   WaveData.GamePhase.Late;

        float easyWeight = 0f, mediumWeight = 0f, hardWeight = 0f;
        switch (state)
        {
            case GameState.Dominating: easyWeight = 0f;   mediumWeight = 0.2f; hardWeight = 0.8f; break;
            case GameState.Relaxed:    easyWeight = 0f;   mediumWeight = 0.6f; hardWeight = 0.4f; break;
            case GameState.Neutral:    easyWeight = 0.3f; mediumWeight = 0.5f; hardWeight = 0.2f; break;
            case GameState.Tense:      easyWeight = 0.2f; mediumWeight = 0.6f; hardWeight = 0.2f; break;
            case GameState.Suffering:  easyWeight = 0.7f; mediumWeight = 0.3f; hardWeight = 0f;   break;
        }

        float totalWeight = easyWeight + mediumWeight + hardWeight;
        if (totalWeight <= 0f) { mediumWeight = 1f; totalWeight = 1f; }

        float r = Random.value * totalWeight;
        WaveData.Difficulty chosenDifficulty =
            (r < easyWeight) ? WaveData.Difficulty.Easy :
            (r < easyWeight + mediumWeight) ? WaveData.Difficulty.Medium :
            WaveData.Difficulty.Hard;

        WaveData[] candidates = System.Array.FindAll(spawnManager.data,
            w => w.phase == phase && w.difficulty == chosenDifficulty);

        if (candidates.Length == 0)
            candidates = System.Array.FindAll(spawnManager.data, w => w.phase == phase);

        if (candidates.Length == 0) return;

        WaveData chosen = candidates[Random.Range(0, candidates.Length)];
        pendingWave = chosen;

        if (enableDetailedLogs) Debug.Log($"[MDP] Queued {chosen.name} for state {state} ({chosenDifficulty}, {phase})");
    }

    private void QueueEventForState(GameState state)
    {
        if (eventManager == null || eventManager.events.Length == 0) return;

        float elapsed = GameManager.Instance ? GameManager.Instance.GetElapsedTime() : 0f;
        EventData.GamePhase phase = elapsed < 150f ? EventData.GamePhase.Early :
                                    elapsed < 390f && elapsed > 150f ? EventData.GamePhase.Mid :
                                    EventData.GamePhase.Late;

        float easyWeight = 0f, mediumWeight = 0f, hardWeight = 0f;
        switch (state)
        {
            case GameState.Dominating: easyWeight = 0f;   mediumWeight = 0.2f; hardWeight = 0.8f; break;
            case GameState.Relaxed:    easyWeight = 0f;   mediumWeight = 0.6f; hardWeight = 0.4f; break;
            case GameState.Neutral:    easyWeight = 0.3f; mediumWeight = 0.5f; hardWeight = 0.2f; break;
            case GameState.Tense:      easyWeight = 0.2f; mediumWeight = 0.6f; hardWeight = 0.2f; break;
            case GameState.Suffering:  easyWeight = 0.7f; mediumWeight = 0.3f; hardWeight = 0f;   break;
        }

        // Apply phase-based caps to prevent hard events in early phases
        switch (phase)
        {
            case EventData.GamePhase.Early:
                hardWeight = Mathf.Min(hardWeight, 0.1f);  // Cap hard weight to 10% in early game
                mediumWeight = Mathf.Max(mediumWeight, 0.4f);  // Ensure medium isn't too low
                break;
            case EventData.GamePhase.Mid:
                hardWeight = Mathf.Min(hardWeight, 0.4f);  // Moderate cap for mid game
                break;
            case EventData.GamePhase.Late:
                // No caps, full weights
                break;
        }

        float totalWeight = easyWeight + mediumWeight + hardWeight;
        if (totalWeight <= 0f) { mediumWeight = 1f; totalWeight = 1f; }

        float r = Random.value * totalWeight;
        EventData.Difficulty chosenDifficulty =
            (r < easyWeight) ? EventData.Difficulty.Easy :
            (r < easyWeight + mediumWeight) ? EventData.Difficulty.Medium :
            EventData.Difficulty.Hard;

        EventData[] candidates = System.Array.FindAll(eventManager.events,
            e => e.phase == phase && e.difficulty == chosenDifficulty && e.IsActive());

        if (candidates.Length == 0)
            candidates = System.Array.FindAll(eventManager.events, e => e.phase == phase && e.IsActive());  // Fallback to any in phase

        if (candidates.Length == 0) return;

        EventData chosen = candidates[Random.Range(0, candidates.Length)];
        pendingEvent = chosen;

        if (enableDetailedLogs) Debug.Log($"[MDP] Queued event {chosen.name} for state {state} ({chosenDifficulty}, {phase}) - Phase-constrained");
    }


    #endregion

    #region Loot, Curse Adjustment
    // -----------------------
    // Loot Adjustment
    // -----------------------

    // Unified loot bias method (combines old ApplyLootAdjustment and ApplyLootBiasForLevelUp)
    private void ApplyLootBias(GameState state, PlayerProfile profile)
    {
        if (LootManager.Instance == null || profile == null) return;

        LootManager.Instance.ClearBiases();

        // State-based biases with full coverage
        switch (state)
        {
            case GameState.Dominating:
                LootManager.Instance.AddCategoryBias(ItemCategory.Tactics, 2f);
                LootManager.Instance.AddRarityBias(Rarity.Common, 1.5f);
                if (enableDetailedLogs) Debug.Log("[MDP] Loot bias: Common Items");
                break;
            case GameState.Relaxed:
                LootManager.Instance.AddRarityBias(Rarity.Common, 1.5f);
                break;
            case GameState.Neutral:
                // Mild bias toward balanced options
                LootManager.Instance.AddCategoryBias(ItemCategory.Tactics, 1.2f);
                break;
            case GameState.Tense:
                LootManager.Instance.AddCategoryBias(ItemCategory.Brutality, 2f);
                break;
            case GameState.Suffering:
                LootManager.Instance.AddCategoryBias(ItemCategory.Survival, 3f);
                LootManager.Instance.AddRarityBias(Rarity.Rare, 1.5f);
                if (enableDetailedLogs) Debug.Log("[MDP] Loot bias: Heavy Survival (Suffering)");
                break;
        }

        // PPM-based performance adjustments
        if (profile.predictedDPS < profile.sessionDPS * 0.8f)
        {
            LootManager.Instance.AddCategoryBias(ItemCategory.Brutality, 2f);
            if (enableDetailedLogs) Debug.Log("[MDP] Loot bias → more DPS items for DPS recovery");
        }
        if (profile.predictedDamageTakenRate > 10f)
        {
            LootManager.Instance.AddCategoryBias(ItemCategory.Survival, 2f);
            if (enableDetailedLogs) Debug.Log("[MDP] Loot bias → more Survival items for survivability");
        }

        // PPM predictions and favorites
        if (profile.predictedItemCategory.HasValue)
        {
            LootManager.Instance.AddCategoryBias(profile.predictedItemCategory.Value, Mathf.Min(2f, LootManager.Instance.GetCategoryBiases().GetValueOrDefault(profile.predictedItemCategory.Value, 0) + 2f));  // Cap to prevent overload
            if (enableDetailedLogs) Debug.Log($"[MDP] Loot bias → aligned with predicted Category: {profile.predictedItemCategory.Value}");
        }
        LootManager.Instance.AddCategoryBias(profile.favoredItemCategory, Mathf.Min(1.5f, LootManager.Instance.GetCategoryBiases().GetValueOrDefault(profile.favoredItemCategory, 0) + 1.5f));
    }

    // Hook into level-up (now calls the unified method)
    public void OnPlayerLevelUp()
    {
        var profile = PredictivePlayerModel.Instance?.CurrentProfile;
        if (profile != null)
        {
            ApplyLootBias(CurrentState, profile);
        }
    }



    // -----------------------
    // Curse Adjustment
    // -----------------------
    private void ApplyCurseAdjustment(GameState state)
    {
        if (curseManager == null) return;

        // Determine phase
        float elapsed = GameManager.Instance ? GameManager.Instance.GetElapsedTime() : 0f;
        bool early = elapsed < 150f;
        bool mid   = elapsed >= 150f && elapsed < 390f;
        bool late  = elapsed >= 390f;

        int steps = 0;

        switch (state)
        {
            case GameState.Dominating:
                if (late && lastDpsRatio > 1.2f && lastHpPercent > 0.9f)
                    steps = 2; // offensive dominance
                else if (lastHpPercent > 0.9f && (late || mid))
                    steps = 2; // non early increase
                else
                    steps = 1;
                if (enableDetailedLogs) Debug.Log("[MDP] Curse ↑ (Dominating)");
                break;

            case GameState.Relaxed:
                if (late && lastHpPercent > 0.70)
                    steps = 1;
                else steps = 1;
                if (enableDetailedLogs) Debug.Log("[MDP] Curse ↑ (Relaxed → keeping tension)");
                break;

            case GameState.Tense:
                if (early || mid) steps = -2; //ease in early game
                else steps = -1;
                if (enableDetailedLogs) Debug.Log("[MDP] Curse ↓ (Tense → easing pressure)");
                break;

            case GameState.Suffering:
                if (early) steps = -3;
                else if (mid) steps = -2;
                else steps = -1;
                if (enableDetailedLogs) Debug.Log("[MDP] Curse ↓ (Suffering)");
                break;

            case GameState.Neutral:
                break;
        }

        if (steps != 0)
        {
            curseManager.AdjustCurse(steps);
            if (enableDetailedLogs) Debug.Log($"[MDP] Curse adjusted by {steps} (state={state}, phase={(early?"Early":mid?"Mid":"Late")})");
        }
    }
    
    #endregion

    // convenience: use this to call Step using current PPM profile quickly
    public (GameState nextState, float reward) StepUsingPPM(GameAction action)
    {
        PlayerProfile profile = PredictivePlayerModel.Instance != null ? PredictivePlayerModel.Instance.CurrentProfile : null;
        return Step(action, profile);
    }

    //for the bridge, making things more explicit
    public void EnqueueAction(GameState state, GameAction action)
    {
        switch (action)
        {
            case GameAction.SpawnEnemies:
                QueueWaveForState(state);
                break;

            case GameAction.TriggerMobEvent:
                QueueEventForState(state);
                break;

            case GameAction.AdjustLoot:
                //ApplyLootAdjustment(state);
                break;
        }
    }

    #region Directive Helpers

    private LootBias GetLootBiasForState(GameState state)
    {
        switch (state)
        {
            case GameState.Dominating: return LootBias.DPS;
            case GameState.Suffering: return LootBias.Survival;
            default: return LootBias.Balanced;
        }
    }

    private WaveType GetWaveTypeForState(GameState state)
    {
        switch (state)
        {
            case GameState.Tense: return WaveType.Hard;
            case GameState.Suffering: return WaveType.Event;
            case GameState.Dominating: return WaveType.Medium;
            default: return WaveType.Easy;
        }
    }

    private float GetCurseAdjustmentForState(GameState state)
    {
        switch (state)
        {
            case GameState.Suffering: return -1f;
            case GameState.Dominating: return +1f;
            default: return 0f;
        }
    }

    #endregion

    // Debug getter
    public float GetLastReward() => lastReward;
}

#region DirectiveEnums

public enum TargetState
{
    Dominating,
    Relaxed,
    Neutral,
    Tense,
    Suffering
}

public enum LootBias
{
    DPS,
    Survival,
    Balanced
}

public enum WaveType
{
    Easy,
    Medium,
    Hard,
    Event
}

#endregion