using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TacticalDirector : MonoBehaviour
{
    [Header("Enable Combined Tactical Director")]
    public bool enableCombinedVersion = true;

    [Header("Timing")]
    private float mdpTimer = 0f;
    private float marlTimer = 0f;
    private float behaviorTimer = 0f;
    private float mdpInterval => MDPManager.Instance != null ? MDPManager.Instance.stateConfirmTime : 0.8f; // e.g., 0.8f
    private float marlInterval => MARLManager.Instance != null ? MARLManager.Instance.marlUpdateInterval : 2f; // e.g., 2f
    private float behaviorInterval => MARLManager.Instance != null ? MARLManager.Instance.behaviorUpdateInterval : 2f; // Changed from 8f to 2f for better frequency

    [Header("Debug")]
    public bool logSnapshots = false;
    public bool enableDetailedLogs = false;  // Master toggle for detailed logs to prevent performance issues

    [Header("Smoothing & Escalation")]
    [Range(0f, 1f)] public float smoothingAlpha = 0.2f;
    public float smoothedMDPReward = 0f;
    public float smoothedMARLReward = 0f;

    [Range(0f, 1f)] public float escalationLevel = 0f;
    public float escalationRate = 0.01f;

    [Header("Behavior Stability")]
    [Range(0f, 1f)] public float behaviorChangeThreshold = 0.1f;  // Only change if new behavior is 10% better
    private MARLBehavior lastStableBehavior = MARLBehavior.Aggressive;  // Track the last applied behavior
    private float cacheTimer = 0f;  // For updating MARL cache in combined mode

    private GameDirective latestDirective;
    private MDPManager.GameState lastMDPState;
    private MDPManager.GameState lastNextState;
    private float lastMDPReward;

    void Start()
    {
        enableCombinedVersion = PlayerPrefs.GetInt("EnableCombinedMode", 1) == 1;
        UpdateStandaloneFlags();
        if (enableDetailedLogs) Debug.Log($"[TacticalDirector] Loaded combined mode: {enableCombinedVersion}");
        // Disable standalone updates in MDP/MARL when combined is enabled
        if (enableCombinedVersion)
        {
            if (MDPManager.Instance != null) MDPManager.Instance.enableStandalone = false;
            if (MARLManager.Instance != null) MARLManager.Instance.enableStandalone = false;
        }
        else
        {
            if (MDPManager.Instance != null) MDPManager.Instance.enableStandalone = true;
            if (MARLManager.Instance != null) MARLManager.Instance.enableStandalone = true;
        }
    }

    void Update()
    {
        if (!enableCombinedVersion) return;  // Let MDP/MARL run standalone

        float delta = Time.deltaTime;
        mdpTimer += delta;
        marlTimer += delta;
        behaviorTimer += delta;
        cacheTimer += delta;

        if (cacheTimer >= 0.5f)
        {
            cacheTimer = 0f;
            if (MARLManager.Instance != null)
            {
                MARLManager.Instance.UpdateAgentCache();
                MARLManager.Instance.PrecomputeSharedValues();
            }
        }

        // MDP Loop: Observe → Derive State → Choose Action → Step/Apply → Send Directive
        if (mdpTimer >= mdpInterval)
        {
            mdpTimer = 0f;
            RunMDPLoop();
        }

        // MARL Loop: Collect States → Select Behavior → Evaluate Reward → Send Feedback
        if (marlTimer >= marlInterval)
        {
            marlTimer = 0f;
            RunMARLRewardLoop(); // Handles reward aggregation
        }

        if (behaviorTimer >= behaviorInterval)
        {
            behaviorTimer = 0f;
            RunMARLBehaviorLoop(); // Handles behavior selection and assignment
        }
    }

    public void SetCombinedMode(bool enable)
    {
        enableCombinedVersion = enable;
        UpdateStandaloneFlags();
    }

    private void UpdateStandaloneFlags()
    {
        if (enableCombinedVersion)
        {
            if (MDPManager.Instance != null) MDPManager.Instance.enableStandalone = false;
            if (MARLManager.Instance != null) MARLManager.Instance.enableStandalone = false;
        }
        else
        {
            if (MDPManager.Instance != null) MDPManager.Instance.enableStandalone = true;
            if (MARLManager.Instance != null) MARLManager.Instance.enableStandalone = true;
        }
        if (enableDetailedLogs) Debug.Log($"[TacticalDirector] Combined mode set to {enableCombinedVersion}. Standalone flags updated.");
    }

    #region MDP Loop
    private void RunMDPLoop()
    {
        var ppm = PredictivePlayerModel.Instance?.CurrentProfile;
        if (ppm == null || MDPManager.Instance == null)
        {
            if (enableDetailedLogs) Debug.LogWarning("[TacticalDirector] PPM profile or MDPManager instance is null. Skipping MDP loop.");
            return;
        }

        if (enableDetailedLogs) Debug.Log("[TacticalDirector] Starting MDP Loop: Observing PPM and deriving state.");

        // MDP: Observe PPM → Derive Observed State
        lastMDPState = MDPManager.Instance.DeriveStateFromPPM(ppm);
        if (enableDetailedLogs) Debug.Log($"[TacticalDirector] MDP derived state: {lastMDPState} from PPM (Phase: {ppm.timePhase}, Style: {ppm.playstyle}).");

        // MDP: Choose Best Action based on Rewards
        var action = MDPManager.Instance.ChooseBestAction(ppm);
        if (enableDetailedLogs) Debug.Log($"[TacticalDirector] MDP chose action: {action} based on rewards.");

        // MDP: Step and Apply (update state, trigger effects) → Generate Directive
        var (nextState, reward, directive) = MDPManager.Instance.StepAndApply(action, ppm, useMARL: true);
        lastNextState = nextState;
        lastMDPReward = reward;
        latestDirective = directive;

        if (enableDetailedLogs) Debug.Log($"[TacticalDirector] MDP stepped: {lastMDPState} + {action} → {nextState} (Reward: {reward:F2}). Directive created: LootBias={directive.LootBias}, Curse={directive.CurseAdjustment:F2}.");

        // Incorporate MARL Feedback for MDP Adjustments
        float marlReward = MARLManager.Instance?.LastGlobalReward ?? 0f;
        float blendedReward = reward + marlReward * 0.5f;
        smoothedMDPReward = smoothingAlpha * blendedReward + (1 - smoothingAlpha) * smoothedMDPReward;

        if (enableDetailedLogs) Debug.Log($"[TacticalDirector] Blended MDP reward with MARL feedback: MDP={reward:F2}, MARL={marlReward:F2}, Blended={blendedReward:F2}, Smoothed={smoothedMDPReward:F2}.");

        // Reinterpret MDP State Based on MARL Reward
        MDPManager.GameState originalState = nextState;

        // Reinterpret Dominating → Neutral if enemies are pushing back
        if (nextState == MDPManager.GameState.Dominating && marlReward > 10f)
            nextState = MDPManager.GameState.Neutral;

        // Reinterpret Suffering → Neutral if player is holding better than expected
        else if (nextState == MDPManager.GameState.Suffering && marlReward < -10f)
            nextState = MDPManager.GameState.Neutral;

        // Reinterpret Relaxed → Tense if MARL reward is high (player may be under more pressure than expected)
        else if (nextState == MDPManager.GameState.Relaxed && marlReward > 5.6f)
            nextState = MDPManager.GameState.Tense;

        // Reinterpret Tense → Relaxed if MARL reward is low (player may be handling pressure better than expected)
        else if (nextState == MDPManager.GameState.Tense && marlReward < -5.6f)
            nextState = MDPManager.GameState.Relaxed;

        if (originalState != nextState)
            if (enableDetailedLogs) Debug.Log($"[TacticalDirector] MDP state adjusted from {originalState} to {nextState} based on MARLReward={marlReward:F2}.");

        lastNextState = nextState; // Update with reinterpreted state
        latestDirective.TargetState = (TargetState)nextState; // Ensure directive reflects adjusted state

        // Adjust Escalation based on Performance
        if (smoothedMDPReward > 0.5f || smoothedMARLReward > 0.5f)
        {
            escalationLevel += escalationRate;
            if (enableDetailedLogs) Debug.Log($"[TacticalDirector] Escalation increased: Level now {escalationLevel:F2} (good performance).");
        }
        else if (smoothedMDPReward < -0.5f || smoothedMARLReward < -0.5f)
        {
            escalationLevel -= escalationRate;
            if (enableDetailedLogs) Debug.Log($"[TacticalDirector] Escalation decreased: Level now {escalationLevel:F2} (poor performance).");
        }
        escalationLevel = Mathf.Clamp(escalationLevel, 0f, 1f);

        // Apply Escalation to Directive
        latestDirective.CurseAdjustment += escalationLevel * 0.5f;
        if (enableDetailedLogs) Debug.Log($"[TacticalDirector] Applied escalation to directive: CurseAdjustment now {latestDirective.CurseAdjustment:F2}.");

        // Send Directive to MARL (for biasing behaviors)
        if (MARLManager.Instance != null)
        {
            MARLManager.Instance.ApplyDirectiveBias(latestDirective);
            if (enableDetailedLogs) Debug.Log($"[TacticalDirector] Sent directive to MARL: TargetState={directive.TargetState}, LootBias={directive.LootBias}, WaveType={directive.WaveType}, Curse={directive.CurseAdjustment:F2}.");
        }
        else
        {
            if (enableDetailedLogs) Debug.LogWarning("[TacticalDirector] MARLManager instance is null. Cannot send directive.");
        }

        if (logSnapshots)
            LogSnapshot(ppm, lastMDPState, lastNextState, lastMDPReward, MARLManager.Instance?.CurrentBehavior ?? MARLBehavior.Aggressive, MARLManager.Instance?.CurrentBehavior ?? MARLBehavior.Aggressive);
    }
    #endregion

    #region MARL Loop

    // Called every marlInterval (e.g., 2s) — handles reward aggregation only
    private void RunMARLRewardLoop()
    {
        var ppm = PredictivePlayerModel.Instance?.CurrentProfile;
        if (ppm == null || MARLManager.Instance == null)
        {
            if (enableDetailedLogs) Debug.LogWarning("[TacticalDirector] Skipping MARL reward loop — missing PPM or MARLManager.");
            return;
        }

        float reward = MARLManager.Instance.AggregateAndDistributeRewards();
        smoothedMARLReward = smoothingAlpha * reward + (1 - smoothingAlpha) * smoothedMARLReward;
        MARLManager.Instance.ClearCounters();

        if (enableDetailedLogs) Debug.Log($"[TacticalDirector] MARL reward updated: Raw={reward:F2}, Smoothed={smoothedMARLReward:F2}");
    }

    // Called every behaviorInterval (e.g., 2s) — handles tactical key evaluation, bias injection, behavior selection, and agent assignment
    private void RunMARLBehaviorLoop()
    {
        var ppm = PredictivePlayerModel.Instance?.CurrentProfile;
        if (ppm == null || MARLManager.Instance == null || latestDirective == null)
        {
            if (enableDetailedLogs) Debug.LogWarning("[TacticalDirector] Skipping MARL behavior loop — missing PPM, MARLManager, or directive.");
            return;
        }

        if (enableDetailedLogs) Debug.Log("[TacticalDirector] Starting MARL Behavior Loop: Collecting states and selecting behavior.");

        // Step 1: Evaluate tactical key from PPM + Inventory
        MARLManager.Instance.EvaluateMARLState();
        var key = MARLManager.Instance.LastState;
        if (enableDetailedLogs) Debug.Log($"[TacticalDirector] MARL collected state: Phase={key.phase}, Style={key.style}, Movement={key.movement}, Category={key.categoryProfile}.");

        // Step 2: Inject strategic bias into MARL weights BEFORE sampling
        if (MARLManager.Instance.actionWeights.TryGetValue(key, out var weights))
        {
            ApplyStrategicBias(weights, key);
        }

        // Step 3: Sample behavior AFTER bias is applied
        var marlChosen = MARLManager.Instance.ChooseBehavior(key.phase, key.style, key.movement, key.categoryProfile);
        MARLManager.Instance.CurrentBehavior = marlChosen;

        // Step 4: Check if the new behavior is significantly better before switching (stability check from first version)
        bool shouldChange = ShouldChangeBehavior(marlChosen, key);
        if (!shouldChange)
        {
            marlChosen = lastStableBehavior;  // Stick with the last stable one
            if (enableDetailedLogs) Debug.Log($"[TacticalDirector] Behavior change rejected (not significantly better). Sticking with {marlChosen}.");
        }
        else
        {
            lastStableBehavior = marlChosen;
            if (enableDetailedLogs) Debug.Log($"[TacticalDirector] Behavior change accepted: {marlChosen}.");
        }

        // Step 5: Update weights based on reward feedback
        MARLManager.Instance.UpdateActionWeights(key, marlChosen, MARLManager.Instance.LastGlobalReward);

        // Step 6: Assign behavior to all agents
        foreach (var agent in MARLManager.Instance.GetAgents())
        {
            if (agent != null && agent.IsAlive)
            {
                agent.SetBehavior(marlChosen);
            }
        }

        // Step 7: Log the selected behavior and snapshot
        if (enableDetailedLogs) Debug.Log($"[TacticalDirector] MARL selected behavior: {marlChosen} for key {key}.");
        if (logSnapshots)
        {
            LogSnapshot(ppm, lastMDPState, lastNextState, lastMDPReward, marlChosen, marlChosen);
        }
    }

    // Injects bias into MARL weights based on MDP state and last action (from second version)
    private void ApplyStrategicBias(Dictionary<MARLBehavior, float> weights, (string phase, string style, string movement, string categoryProfile) key)
    {
        switch (lastMDPState)
        {
            case MDPManager.GameState.Dominating:
                TryBiasWeight(weights, MARLBehavior.Scatter, 0.3f);
                TryBiasWeight(weights, MARLBehavior.CounterOffense, 0.3f);
                break;
            case MDPManager.GameState.Relaxed:
                TryBiasWeight(weights, MARLBehavior.Scatter, 0.2f);
                TryBiasWeight(weights, MARLBehavior.CounterOffense, 0.2f);
                break;
            case MDPManager.GameState.Tense:
                TryBiasWeight(weights, MARLBehavior.Aggressive, 0.3f);
                TryBiasWeight(weights, MARLBehavior.Cluster, 0.2f);
                TryBiasWeight(weights, MARLBehavior.Encircle, 0.2f);
                break;
            case MDPManager.GameState.Suffering:
                TryBiasWeight(weights, MARLBehavior.Aggressive, 0.4f);
                TryBiasWeight(weights, MARLBehavior.Cluster, 0.3f);
                TryBiasWeight(weights, MARLBehavior.Encircle, 0.3f);
                break;
        }

        if (latestDirective is GameDirective directiveWithAction)
        {
            switch (directiveWithAction.LastAction)
            {
                case MDPManager.GameAction.AdjustCurse:
                    if (directiveWithAction.CurseAdjustment > 1.0f)
                    {
                        TryBiasWeight(weights, MARLBehavior.Aggressive, 0.1f);
                        TryBiasWeight(weights, MARLBehavior.Encircle, 0.1f);
                    }
                    break;
                case MDPManager.GameAction.AdjustLoot:
                    if (directiveWithAction.LootBias == LootBias.DPS)
                    {
                        TryBiasWeight(weights, MARLBehavior.Scatter, 0.1f);
                        TryBiasWeight(weights, MARLBehavior.CounterOffense, 0.1f);
                    }
                    else if (directiveWithAction.LootBias == LootBias.Survival)
                    {
                        TryBiasWeight(weights, MARLBehavior.Aggressive, 0.1f);
                        TryBiasWeight(weights, MARLBehavior.Cluster, 0.1f);
                    }
                    break;
            }
        }

        // Normalize weights
        float total = weights.Values.Sum();
        if (total > 0f)
        {
            foreach (var b in weights.Keys.ToList())
                weights[b] /= total;
        }

        if (enableDetailedLogs) Debug.Log($"[TacticalDirector] Strategic bias applied to MARL weights for key {key}.");
    }

    // Utility: safely adds bias to a behavior weight
    private void TryBiasWeight(Dictionary<MARLBehavior, float> weights, MARLBehavior behavior, float amount)
    {
        if (weights.ContainsKey(behavior))
            weights[behavior] += amount;
    }

    #endregion

    #region Behavior Stability (from first version)
    private bool ShouldChangeBehavior(MARLBehavior newBehavior, (string phase, string style, string movement, string categoryProfile) key)
    {
        if (MARLManager.Instance == null) return true;  // Fallback
        var weights = MARLManager.Instance.actionWeights.TryGetValue(key, out var w) ? w : null;
        if (weights == null || !weights.ContainsKey(lastStableBehavior) || !weights.ContainsKey(newBehavior))
            return true;  // If no weights, allow change
        float currentWeight = weights[lastStableBehavior];
        float newWeight = weights[newBehavior];
        float improvement = (newWeight - currentWeight) / Mathf.Max(currentWeight, 0.01f);  // Percentage improvement
        return improvement >= behaviorChangeThreshold;  // Only change if improvement >= threshold
    }
    #endregion

    #region Logging
    private void LogSnapshot(PlayerProfile ppm,
                             MDPManager.GameState mdpState,
                             MDPManager.GameState nextState,
                             float reward,
                             MARLBehavior marlChosen,
                             MARLBehavior adjusted)
    {
        string snapshot = $"[TacticalDirector] Snapshot → " +
                          $"Phase: {ppm.timePhase}, " +
                          $"Style: {ppm.playstyle}, " +
                          $"Move: {PredictivePlayerModel.Instance.CurrentMovement}, " +
                          $"MDP: {mdpState} → {nextState}, " +
                          $"MARL(original): {marlChosen}, " +
                          $"MARL(adjusted): {adjusted}, " +
                          $"Reward: {reward:F2}, " +
                          $"SmoothedMDP: {smoothedMDPReward:F2}, " +
                          $"SmoothedMARL: {smoothedMARLReward:F2}, " +
                          $"Escalation: {escalationLevel:F2}";

        Debug.Log(snapshot);
    }
    #endregion
}