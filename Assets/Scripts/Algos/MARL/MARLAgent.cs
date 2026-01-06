using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Lightweight MARL agent for a high-enemy-count 2D game.
/// Executes behavior chosen by EnemyAgent; reports events to MARLManager.
/// Optimized with caching and precomputations from MARLManager.
/// </summary>
[RequireComponent(typeof(EnemyMovement))]
public class MARLAgent : MonoBehaviour
{
    private EnemyMovement move;
    private MARLBehavior currentBehavior;

    private float decisionTimer;
    public float decisionInterval = 0.25f; // micro-decisions

    private float lastHitTime = -999f;
    private float hitReportCooldown = -0.3f;

    // state timers for time-boxed behaviors
    private float stateTimer;
    public float encircleDuration = 1.5f;
    public float clusterGatherTime = 1.5f;

    // flags for counter-offense
    private bool recentlyDodged;

    public bool IsAlive => move != null && enabled;

    void Awake()
    {
        move = GetComponent<EnemyMovement>();
    }

    void Start()
    {
        MARLManager.Instance?.RegisterAgent(this);
    }

    void OnDestroy()
    {
        MARLManager.Instance?.ReportDeath(this);
        MARLManager.Instance?.UnregisterAgent(this);
    }

    void Update()
    {
        decisionTimer += Time.deltaTime;
        stateTimer += Time.deltaTime;

        // Frequent micro-decisions (reactivity)
        if (decisionTimer >= decisionInterval)
        {
            decisionTimer = 0f;
            PerformBehavior();
        }

        // NOTE: Removed call to MARLManager.RequestNewBehavior
        // EnemyAgent (ML-Agents) is now the sole behavior selector.
    }

    /// <summary>
    /// Execute movement influenced by current behavior.
    /// Uses cached/precomputed values from MARLManager for performance.
    /// </summary>
    void PerformBehavior()
    {
        if (move == null || move.Player == null) return;

        Vector2 pos = move.transform.position;
        Vector2 playerPos = (Vector2)move.Player.position;

        switch (currentBehavior)
        {
            case MARLBehavior.Encircle:
                float innerRadius = 6f;
                float outerRadius = 7f;
                Vector2 offset = pos - playerPos;
                float currentRadius = offset.magnitude;
                float clampedRadius = Mathf.Clamp(currentRadius, innerRadius, outerRadius);
                Vector2 targetPos = playerPos + offset.normalized * clampedRadius;

                if (stateTimer < encircleDuration)
                    move.SetExternalTarget(targetPos, 1f);
                else
                    move.SetExternalTarget(playerPos, 0.5f);
                break;

            case MARLBehavior.CounterOffense:
                if (DetectPlayerFacingDangerCone(out Vector2 escapeDir))
                {
                    move.SetExternalTarget(pos + escapeDir * 2f, 1.5f);
                    recentlyDodged = true;
                    stateTimer = 0f;
                }
                else if (recentlyDodged && stateTimer < 3f) // dodge timer
                {
                    move.SetExternalTarget(playerPos, 0.5f);
                }
                else
                {
                    recentlyDodged = false;
                    move.SetExternalTarget(playerPos, 0.2f);
                }
                break;

            case MARLBehavior.Cluster:
            {
                Vector2Int cell = new Vector2Int(Mathf.FloorToInt(pos.x / MARLManager.GRID_CELL_SIZE), Mathf.FloorToInt(pos.y / MARLManager.GRID_CELL_SIZE));
                
                List<MARLAgent> nearbyAgents = new List<MARLAgent>();
                // Check own cell and neighbors
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        Vector2Int checkCell = cell + new Vector2Int(x, y);
                        if (MARLManager.Instance.spatialGrid.TryGetValue(checkCell, out var agentsInCell))
                        {
                            nearbyAgents.AddRange(agentsInCell.Where(a => a != null && a.IsAlive));
                        }
                    }
                }
                
                Vector2 localCenter = pos;  // Default to own position
                if (nearbyAgents.Count > 1)  // Need at least 2 for clustering
                {
                    Vector2 sum = Vector2.zero;
                    foreach (var a in nearbyAgents) sum += (Vector2)a.transform.position;
                    localCenter = sum / nearbyAgents.Count;
                    // Bias slightly toward player
                    Vector2 playerPosVec = (Vector2)move.Player.position;
                    localCenter = Vector2.Lerp(localCenter, playerPosVec, 0.3f);
                }
                
                // Add per-agent randomness for spread
                Vector2 randomOffset = Random.insideUnitCircle * 2f;
                Vector2 clusterTargetPos = localCenter + randomOffset;
                
                if (stateTimer < clusterGatherTime)
                    move.SetExternalTarget(clusterTargetPos, 1f);
                else
                    move.SetExternalTarget(playerPos, 0.5f);
                break;
            }

            case MARLBehavior.Scatter:
                // Use cached nearby count and precomputed separation from MARLManager
                int nearbyCount = MARLManager.Instance.GetNearbyCount(this, 3f);
                if (nearbyCount > 2)
                {
                    Vector2 separation = MARLManager.Instance.PrecomputedSeparations.TryGetValue(this, out var sep) ? sep : Vector2.zero;
                    move.SetExternalTarget(pos + separation * 2f, 1f);
                }
                else
                {
                    move.SetExternalTarget(playerPos, 0.5f);
                }
                break;
        }
    }

    // ---------- Event hooks ----------
    public void OnSuccessfulHitPlayer()
    {
        if (Time.time - lastHitTime >= hitReportCooldown)
        {
            MARLManager.Instance?.ReportHit(this);
            lastHitTime = Time.time;
        }
    }
    public void OnDeath() => MARLManager.Instance?.ReportDeath(this);

    #region Agent Behavior
    public void SetBehavior(MARLBehavior behavior)
    {
        currentBehavior = behavior;
        stateTimer = 0f;
    }

    public MARLBehavior CurrentBehavior => currentBehavior;

    public void ReceiveGlobalReward(float reward)
    {
        // placeholder for custom learners; ML-Agents reward is handled in EnemyAgent
    }

    public void ResetAgent()
    {
        currentBehavior = MARLBehavior.Scatter; // neutral default
        decisionTimer = 0f;
        stateTimer = 0f;
        recentlyDodged = false;
    }
    #endregion

    #region Player Detection
    private bool DetectPlayerFacingDangerCone(out Vector2 escapeDir)
    {
        escapeDir = Vector2.zero;
        if (move == null || move.Player == null) return false;

        PlayerMovement pm = move.Player.GetComponent<PlayerMovement>();
        Vector2 playerForward = pm != null ? pm.LookDirection.normalized : Vector2.right;
        Vector2 playerPos = move.Player.position;
        Vector2 agentPos = transform.position;
        Vector2 toAgent = agentPos - playerPos;

        float distance = toAgent.magnitude;
        float angle = Vector2.Angle(playerForward, toAgent);

        float coneLength = 60f;
        float coneAngle = 45f;

        if (distance < coneLength && angle < coneAngle)
        {
            // Compute escape vector that moves agent outside the cone
            Vector2 coneEdgeDir = Quaternion.Euler(0, 0, angle > 0 ? coneAngle : -coneAngle) * playerForward;
            Vector2 lateralEscape = Vector2.Perpendicular(playerForward);
            Vector2 awayFromPlayer = (agentPos - playerPos).normalized;

            // Blend lateral and outward escape
            escapeDir = (lateralEscape + awayFromPlayer).normalized;
            return true;
        }

        return false;
    }
    #endregion

    // Removed ComputeClusterCenter, CountNearbyAgents, ComputeSeparationVector
    // These are now handled by MARLManager's caching and precomputations

    #if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || move == null || move.Player == null) return;

        Vector3 pos = transform.position;
        Vector3 playerPos = move.Player.position;

        // Color by behavior
        switch (currentBehavior)
        {
            case MARLBehavior.Aggressive: Gizmos.color = Color.red; break;
            case MARLBehavior.Scatter: Gizmos.color = Color.yellow; break;
            case MARLBehavior.Encircle: Gizmos.color = Color.green; break;
            case MARLBehavior.Cluster: Gizmos.color = Color.cyan; break;
            case MARLBehavior.CounterOffense: Gizmos.color = Color.magenta; break;
            default: Gizmos.color = Color.white; break;
        }

        // Agent marker
        Gizmos.DrawWireSphere(pos, 0.5f);
        Gizmos.DrawLine(pos, pos + transform.right * 1.5f);

        // Behavior-specific overlays
        switch (currentBehavior)
        {
            case MARLBehavior.Encircle:
                float innerRadius = 2.5f;
                float outerRadius = 4.5f;
                Gizmos.DrawWireSphere(playerPos, innerRadius);
                Gizmos.DrawWireSphere(playerPos, outerRadius);
                break;

            case MARLBehavior.Cluster:
                Vector2 clusterCenter = MARLManager.Instance.PrecomputedClusterCenter;
                Gizmos.DrawWireSphere(clusterCenter, 2f);
                Gizmos.DrawLine(pos, clusterCenter);
                break;

            case MARLBehavior.Scatter:
                Gizmos.DrawWireSphere(pos, 3f); // local density check radius
                break;

            case MARLBehavior.CounterOffense:
                foreach (var proj in ProjectileManager.Active)
                {
                    if (proj == null) continue;
                    Vector3 projPos = proj.Position;
                    Vector3 projVel = proj.Velocity.normalized;
                    Gizmos.DrawLine(projPos, projPos + (Vector3)projVel * 2f);
                    Gizmos.DrawWireSphere(projPos, 0.3f);
                }
                break;
        }
    }
    #endif
}
