using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Enhanced MARL manager:
/// - Aggregates rewards (alive, hits, deaths)
/// - Chooses behaviors using:
///   * PredictivePlayerModel (PPM) signals (playstyle, timePhase, movement)
///   * Player's dominant ItemCategory from PlayerInventory // --- MODIFIED ---
/// - Uses a phase × playstyle × movement × category matrix // --- MODIFIED ---
/// - Adapts action probabilities based on past rewards (one-step memory)
/// </summary>
public class MARLManager : MonoBehaviour
{
    public static MARLManager Instance { get; private set; }

    public bool marlEnabled = true;

    [Header("Standalone Mode")]
    public bool enableStandalone = true;  // Enable standalone MARL loop when combined is disabled

    [Header("Timing")]
    public float marlUpdateInterval = 2f;
    public float behaviorUpdateInterval = 8f;
    private float marlTimer;
    private float behaviorTimer;
    
    [Header("Dependencies")]
    [Tooltip("Assign the player's PlayerInventory component here.")]
    public PlayerInventory playerInventory; // Assign in Inspector

    [Header("Behavior Settings")]
    public MARLBehavior CurrentBehavior = MARLBehavior.Aggressive;

    private readonly List<MARLAgent> agents = new List<MARLAgent>();

    private int hitCount = 0;
    private int deathCount = 0;

    public float LastGlobalReward { get; private set; }

    // --- MODIFIED ---
    // One-step memory, now a 4-part tuple
    private (string phase, string style, string movement, string categoryProfile) lastState;
    private MARLBehavior lastAction;
    private float lastReward;

    // --- NEW: Hard-coded item lists for profiling ---
    private readonly HashSet<string> brutalityItems = new HashSet<string>
    {
        "CurveSickle", "Ice Breaker", "Kunai", "Rocket Launcher", "Scythe",
        "Soul Calibre", "Thunderbolt", "Cursed Relic", "Strength Sushi"
    };

    private readonly HashSet<string> tacticsItems = new HashSet<string>
    {
        "Dark Vortex", "Glaive God", "Hype Aura", "Spell Cards",
        "Time Piece", "Weapon Pouch", "QuickDraw", "Runetracer"
    };

    private readonly HashSet<string> survivalItems = new HashSet<string>
    {
        "Heart Piece", "Regen Pot", "Armor", "Vampiric Knives", "Greedy Amulet", "Weapon Enhancer"
    };

    [Header("Optimization")]
    public bool enableDetailedLogs = false;
    public List<MARLAgent> cachedAgents = new List<MARLAgent>();
    public Dictionary<Vector2Int, List<MARLAgent>> spatialGrid = new Dictionary<Vector2Int, List<MARLAgent>>();
    public const int GRID_CELL_SIZE = 8;  // Match cluster grid size
    private float cacheUpdateInterval = 0.5f;  // Update cache every 0.5s
    private float cacheTimer;
    // Precomputed values for agents
    public Vector2 PrecomputedClusterCenter { get; private set; }
    public Dictionary<MARLAgent, Vector2> PrecomputedSeparations { get; private set; } = new Dictionary<MARLAgent, Vector2>();

    #region States
    // --- MODIFIED ---
    // Fully populated 144-state matrix based on your logic:
    // vs. Brutality: CounterOffense, Encircle, Cluster
    // vs. Tactics: Scatter, Aggressive
    // vs. Survival: Aggressive, Encircle, Cluster
    // vs. Balanced: Uses the original lists from your file
    private readonly Dictionary<(string phase, string style, string movement, string categoryProfile), List<MARLBehavior>> behaviorMatrix =
        new Dictionary<(string, string, string, string), List<MARLBehavior>>
        {
        // ---------------- Early Phase, Static Movement ----------------
        { ("Early","Aggressive","Static", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Encircle, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Early","Aggressive","Static", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Encircle, MARLBehavior.Scatter, MARLBehavior.Cluster } },
        { ("Early","Aggressive","Static", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Encircle, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Early","Aggressive","Static", "Survival"), new List<MARLBehavior>{ MARLBehavior.Encircle, MARLBehavior.Aggressive, MARLBehavior.Cluster } },

        { ("Early","Defensive","Static", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Encircle, MARLBehavior.Aggressive, MARLBehavior.Cluster } },
        { ("Early","Defensive","Static", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Encircle, MARLBehavior.Aggressive, MARLBehavior.Cluster } },
        { ("Early","Defensive","Static", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Encircle, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Early","Defensive","Static", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        { ("Early","Efficient","Static", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Early","Efficient","Static", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Early","Efficient","Static", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Aggressive } },
        { ("Early","Efficient","Static", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        { ("Early","Balanced","Static", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Early","Balanced","Static", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Early","Balanced","Static", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Aggressive } },
        { ("Early","Balanced","Static", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        // ---------------- Early Phase, Aggressive Movement ----------------
        { ("Early","Aggressive","Aggressive", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Cluster } },
        { ("Early","Aggressive","Aggressive", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Cluster, MARLBehavior.Aggressive } },
        { ("Early","Aggressive","Aggressive", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Early","Aggressive","Aggressive", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Cluster } },

        { ("Early","Defensive","Aggressive", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Cluster } },
        { ("Early","Defensive","Aggressive", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Cluster, MARLBehavior.Aggressive } },
        { ("Early","Defensive","Aggressive", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Early","Defensive","Aggressive", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Cluster } },

        { ("Early","Efficient","Aggressive", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Early","Efficient","Aggressive", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Cluster, MARLBehavior.Aggressive } },
        { ("Early","Efficient","Aggressive", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Early","Efficient","Aggressive", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Cluster } },

        { ("Early","Balanced","Aggressive", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Aggressive, MARLBehavior.Cluster } },
        { ("Early","Balanced","Aggressive", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Aggressive, MARLBehavior.Cluster } },
        { ("Early","Balanced","Aggressive", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Early","Balanced","Aggressive", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Cluster } },

        // ---------------- Early Phase, Erratic Movement ----------------
        { ("Early", "Aggressive", "Erratic", "Balanced"), new List<MARLBehavior> { MARLBehavior.Scatter, MARLBehavior.Cluster, MARLBehavior.Aggressive } },
        { ("Early", "Aggressive", "Erratic", "Brutality"), new List<MARLBehavior> { MARLBehavior.Scatter, MARLBehavior.Cluster, MARLBehavior.Aggressive } },
        { ("Early", "Aggressive", "Erratic", "Tactics"), new List<MARLBehavior> { MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Early", "Aggressive", "Erratic", "Survival"), new List<MARLBehavior> { MARLBehavior.Aggressive, MARLBehavior.Cluster } },

        { ("Early","Defensive","Erratic", "Balanced"), new List<MARLBehavior> { MARLBehavior.Scatter, MARLBehavior.Cluster, MARLBehavior.Aggressive } },
        { ("Early","Defensive","Erratic", "Brutality"), new List<MARLBehavior> { MARLBehavior.Scatter, MARLBehavior.Cluster, MARLBehavior.Aggressive } },
        { ("Early","Defensive","Erratic", "Tactics"), new List<MARLBehavior> { MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Early","Defensive","Erratic", "Survival"), new List<MARLBehavior> { MARLBehavior.Aggressive, MARLBehavior.Cluster } },

        { ("Early","Efficient","Erratic", "Balanced"), new List<MARLBehavior> { MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Early","Efficient","Erratic", "Brutality"), new List<MARLBehavior> { MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Early","Efficient","Erratic", "Tactics"), new List<MARLBehavior> { MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Early","Efficient","Erratic", "Survival"), new List<MARLBehavior> { MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        { ("Early","Balanced","Erratic", "Balanced"), new List<MARLBehavior> { MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Early","Balanced","Erratic", "Brutality"), new List<MARLBehavior> { MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Early","Balanced","Erratic", "Tactics"), new List<MARLBehavior> { MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Early","Balanced","Erratic", "Survival"), new List<MARLBehavior> { MARLBehavior.Aggressive, MARLBehavior.Cluster } },

        // ---------------- Mid Phase, Static Movement ----------------
        { ("Mid","Aggressive","Static", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Aggressive","Static", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Aggressive","Static", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Aggressive } },
        { ("Mid","Aggressive","Static", "Survival"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        { ("Mid","Defensive","Static", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Defensive","Static", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Defensive","Static", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Aggressive } },
        { ("Mid","Defensive","Static", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        { ("Mid","Efficient","Static", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Efficient","Static", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Efficient","Static", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Aggressive } },
        { ("Mid","Efficient","Static", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        { ("Mid","Balanced","Static", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Balanced","Static", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Balanced","Static", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Aggressive } },
        { ("Mid","Balanced","Static", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        // ---------------- Mid Phase, Aggressive Movement ----------------
        { ("Mid","Aggressive","Aggressive", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Scatter } },
        { ("Mid","Aggressive","Aggressive", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Scatter } },
        { ("Mid","Aggressive","Aggressive", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Mid","Aggressive","Aggressive", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Scatter } },

        { ("Mid","Defensive","Aggressive", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Defensive","Aggressive", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Defensive","Aggressive", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Mid","Defensive","Aggressive", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        { ("Mid","Efficient","Aggressive", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Efficient","Aggressive", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Efficient","Aggressive", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Mid","Efficient","Aggressive", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        { ("Mid","Balanced","Aggressive", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Balanced","Aggressive", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Balanced","Aggressive", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Mid","Balanced","Aggressive", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } }, 

        // ---------------- Mid Phase, Erratic Movement ----------------
        { ("Mid","Aggressive","Erratic", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Aggressive","Erratic", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Aggressive","Erratic", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Mid","Aggressive","Erratic", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        { ("Mid","Defensive","Erratic", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Defensive","Erratic", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Defensive","Erratic", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Mid","Defensive","Erratic", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        { ("Mid","Efficient","Erratic", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Efficient","Erratic", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Efficient","Erratic", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Aggressive } },
        { ("Mid","Efficient","Erratic", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        { ("Mid","Balanced","Erratic", "Balanced"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Balanced","Erratic", "Brutality"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Cluster } },
        { ("Mid","Balanced","Erratic", "Tactics"), new List<MARLBehavior>{ MARLBehavior.Scatter, MARLBehavior.Encircle, MARLBehavior.Aggressive } },
        { ("Mid","Balanced","Erratic", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster } },

        // ---------------- Late Phase, Static Movement ----------------
        { ("Late","Aggressive","Static", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Late","Aggressive","Static", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Late","Aggressive","Static", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Late","Aggressive","Static", "Survival"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },

        { ("Late","Defensive","Static", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Late","Defensive","Static", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Late","Defensive","Static", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Late","Defensive","Static", "Survival"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },

        { ("Late","Efficient","Static", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Late","Efficient","Static", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Late","Efficient","Static", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Late","Efficient","Static", "Survival"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },

        { ("Late","Balanced","Static", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Late","Balanced","Static", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Late","Balanced","Static", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },
        { ("Late","Balanced","Static", "Survival"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive, MARLBehavior.Scatter } },

        // ---------------- Late Phase, Aggressive Movement ----------------
        { ("Late","Aggressive","Aggressive", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive } },
        { ("Late","Aggressive","Aggressive", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive } },
        { ("Late","Aggressive","Aggressive", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Late","Aggressive","Aggressive", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },

        { ("Late","Defensive","Aggressive", "Balanced"), new List<MARLBehavior>{MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Defensive","Aggressive", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Defensive","Aggressive", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Late","Defensive","Aggressive", "Survival"), new List<MARLBehavior>{ MARLBehavior.Aggressive, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },

        { ("Late","Efficient","Aggressive", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Efficient","Aggressive", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Efficient","Aggressive", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Late","Efficient","Aggressive", "Survival"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive } },

        { ("Late","Balanced","Aggressive", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Balanced","Aggressive", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Balanced","Aggressive", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Late","Balanced","Aggressive", "Survival"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Aggressive } },

        // ---------------- Late Phase, Erratic Movement ----------------
        { ("Late","Aggressive","Erratic", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Aggressive","Erratic", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Aggressive","Erratic", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Late","Aggressive","Erratic", "Survival"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },


        { ("Late","Defensive","Erratic", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Defensive","Erratic", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Defensive","Erratic", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Late","Defensive","Erratic", "Survival"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },

        { ("Late","Efficient","Erratic", "Balanced"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Efficient","Erratic", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Efficient","Erratic", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Late","Efficient","Erratic", "Survival"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },

        { ("Late","Balanced","Erratic", "Balanced"),   new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Balanced","Erratic", "Brutality"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
        { ("Late","Balanced","Erratic", "Tactics"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Scatter, MARLBehavior.Aggressive } },
        { ("Late","Balanced","Erratic", "Survival"), new List<MARLBehavior>{ MARLBehavior.CounterOffense, MARLBehavior.Encircle, MARLBehavior.Cluster, MARLBehavior.Scatter } },
    };

    // Action weights per state, now using 4-part key
    public Dictionary<(string phase, string style, string movement, string categoryProfile), Dictionary<MARLBehavior, float>> actionWeights =
        new Dictionary<(string, string, string, string), Dictionary<MARLBehavior, float>>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // --- NEW ---
    // Add a Start method to find the PlayerInventory if not assigned
    void Start()
    {
        if (playerInventory == null)
        {
            // Fallback: try to find the player by tag
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerInventory = playerObj.GetComponent<PlayerInventory>();
            }
            
            if (playerInventory == null)
            {
                //Debug.LogWarning("[MARLManager] PlayerInventory not found! Inventory-based adaptation will be disabled ('Balanced' profile).");
            }
        }
    }

    void Update()
    {
        if (!enableStandalone) 
        {
            if (enableDetailedLogs) Debug.Log("[MARLManager] Update skipped (standalone disabled).");
            return;
        }
        if (!marlEnabled) return;

        marlTimer += Time.deltaTime;
        behaviorTimer += Time.deltaTime;

        if (marlTimer >= marlUpdateInterval)
        {
            marlTimer = 0f;
            float reward = AggregateAndDistributeRewards();
            UpdateActionWeights(lastState, lastAction, reward); // --- MODIFIED ---
            ClearCounters();
        }

        if (behaviorTimer >= behaviorUpdateInterval)
        {
            behaviorTimer = 0f;
            EvaluateMARLState();
        }

        cacheTimer += Time.deltaTime;
        if (cacheTimer >= cacheUpdateInterval)
        {
            cacheTimer = 0f;
            UpdateAgentCache();
            PrecomputeSharedValues();
        }
    }

    #endregion

    #region Agent lifecycle

    public void RegisterAgent(MARLAgent a)
    {
        if (a == null) return;
        if (!agents.Contains(a)) agents.Add(a);
    }

    public void UnregisterAgent(MARLAgent a)
    {
        if (a == null) return;
        agents.Remove(a);
    }
    #endregion

    #region Event reporting
    public void ReportHit(MARLAgent agent) { if (agent != null) hitCount++; }
    public void ReportDeath(MARLAgent agent) { if (agent != null) deathCount++; }
    #endregion

    #region Reward aggregation
    public float AggregateAndDistributeRewards()
    {
        if (agents.Count == 0) return 0f;

        int alive = 0;
        foreach (var a in agents) if (a != null && a.IsAlive) alive++;

        float survivalComponent = 0.05f * alive * marlUpdateInterval;
        float hitComponent = 1f * hitCount;
        float deathComponent = -2f * deathCount;

        float globalReward = survivalComponent + hitComponent + deathComponent;

        foreach (var a in agents)
        {
            if (a != null && a.IsAlive)
                a.ReceiveGlobalReward(globalReward);
        }

        if (enableDetailedLogs) Debug.Log($"[MARLManager] (alive={alive}, hits={hitCount}, deaths={deathCount}) Reward={globalReward:F2}");
        LastGlobalReward = globalReward;
        return globalReward;
    }

    public void ClearCounters()
    {
        hitCount = 0;
        deathCount = 0;
    }
    #endregion

    #region Policy selection

    // --- MODIFIED ---
    /// <summary>
    /// Analyzes the player's inventory by ITEM NAME and returns their dominant build type.
    /// Weights items by their current level.
    /// </summary>
    /// <returns>"Brutality", "Tactics", "Survival", or "Balanced"</returns>
    public string GetInventoryProfile()
    {
        if (playerInventory == null) return "Balanced"; // Default if no inventory found

        // Use ItemCategory enum as keys for tallying
        Dictionary<ItemCategory, int> categoryWeights = new Dictionary<ItemCategory, int>
        {
            { ItemCategory.Brutality, 0 },
            { ItemCategory.Tactics, 0 },
            { ItemCategory.Survival, 0 }
        };

        // --- Tally weights from Weapons ---
        List<Weapon> weapons = playerInventory.GetAllWeapons(); //
        if (weapons != null)
        {
            foreach (var weapon in weapons)
            {
                if (weapon == null || weapon.data == null) continue;
                
                // Check name against our hard-coded lists
                string itemName = weapon.data.name; //
                if (brutalityItems.Contains(itemName))
                {
                    categoryWeights[ItemCategory.Brutality] += weapon.currentLevel; //
                }
                else if (tacticsItems.Contains(itemName))
                {
                    categoryWeights[ItemCategory.Tactics] += weapon.currentLevel; //
                }
                else if (survivalItems.Contains(itemName))
                {
                    categoryWeights[ItemCategory.Survival] += weapon.currentLevel; //
                }
            }
        }

        // --- Tally weights from Passives ---
        List<Passive> passives = playerInventory.GetAllPassives(); //
        if (passives != null)
        {
            foreach (var passive in passives)
            {
                if (passive == null || passive.data == null) continue;

                // Check name against our hard-coded lists
                string itemName = passive.data.name; //
                if (brutalityItems.Contains(itemName))
                {
                    categoryWeights[ItemCategory.Brutality] += passive.currentLevel; //
                }
                else if (tacticsItems.Contains(itemName))
                {
                    categoryWeights[ItemCategory.Tactics] += passive.currentLevel; //
                }
                else if (survivalItems.Contains(itemName))
                {
                    categoryWeights[ItemCategory.Survival] += passive.currentLevel; //
                }
            }
        }
        
        // --- Determine dominant category (same logic as before) ---
        int totalWeight = categoryWeights.Values.Sum();
        if (totalWeight == 0) return "Balanced"; // no items found

        var sorted = categoryWeights.OrderByDescending(kvp => kvp.Value).ToList();
        int maxWeight = sorted[0].Value;
        string dominantCategory = sorted[0].Key.ToString();

        float dominanceRatio = (float)maxWeight / totalWeight;
        float threshold = 0.5f; // weight of item category must exceed 50% to be dominant

        if (dominanceRatio >= threshold)
            return dominantCategory;

        return "Balanced"; // true hybrid build
    }

    // --- MODIFIED ---
    public void EvaluateMARLState()
    {
        var profile = PredictivePlayerModel.Instance?.CurrentProfile; //
        if (profile == null)
        {
            //Debug.Log("[MARLManager] No PPM profile available, defaulting to Aggressive.");
            CurrentBehavior = MARLBehavior.Aggressive;
            return;
        }

        // Get state from PPM
        string phase = string.IsNullOrEmpty(profile.timePhase) ? "Early" : profile.timePhase; //
        string style = string.IsNullOrEmpty(profile.playstyle) ? "Balanced" : profile.playstyle; //
        string movement = PredictivePlayerModel.Instance?.CurrentMovement ?? "Static"; //

        // --- NEW ---
        // Get state from PlayerInventory by checking item names
        string categoryProfile = GetInventoryProfile();

        // --- MODIFIED ---
        CurrentBehavior = ChooseBehavior(phase, style, movement, categoryProfile);

        foreach (var a in agents)
        {
            if (a == null) continue;
            a.SetBehavior(CurrentBehavior);
        }

        // --- log weights for this state ---
        // --- MODIFIED ---
        var key = (phase, style, movement, categoryProfile);
        if (actionWeights.TryGetValue(key, out var weights))
        {
            string weightStr = string.Join(", ",
                System.Linq.Enumerable.Select(weights, kv => $"{kv.Key}:{kv.Value:F2}"));
            // --- MODIFIED ---
            if (enableDetailedLogs) Debug.Log($"[MARLManager] Selected behavior: {CurrentBehavior} (Phase={phase}, Style={style}, Move={movement}, Category={categoryProfile}) | Weights: {weightStr}");
        }
        else
        {
            if (enableDetailedLogs) Debug.Log($"[MARLManager] Selected behavior: {CurrentBehavior} (Phase={phase}, Style={style}, Move={movement}, Category={categoryProfile}) | Weights: [uninitialized]");
        }

        // store one-step memory
        lastState = (phase, style, movement, categoryProfile);
        lastAction = CurrentBehavior;

    }

    public (string phase, string style, string movement, string categoryProfile) LastState => lastState; // FOR COMBINED VERSION

    // Signature changed to accept 4-part state
    public MARLBehavior ChooseBehavior(string phase, string style, string movement, string categoryProfile)
    {
        var key = (phase, style, movement, categoryProfile);

        if (!behaviorMatrix.TryGetValue(key, out var behaviors) || behaviors == null || behaviors.Count == 0)
        {
            // --- NEW: Fallback logic ---
            // If the specific category key (e.g., "Brutality") doesn't exist,
            // try falling back to the "Balanced" key for this state.
            var fallbackKey = (phase, style, movement, "Balanced");
            if (key != fallbackKey && behaviorMatrix.TryGetValue(fallbackKey, out behaviors) && behaviors != null && behaviors.Count > 0)
            {
                //Debug.LogWarning($"[MARLManager] No behaviors for {key}. Using fallback {fallbackKey}.");
                key = fallbackKey; // Use the fallback key for weight lookup
            }
            else
            {
                // If even the "Balanced" fallback fails
                //Debug.LogWarning($"[MARLManager] No behaviors for {key} or fallback {fallbackKey}. Falling back to Aggressive.");
                return MARLBehavior.Aggressive;
            }
        }

        // Initialize equal weights if first time seeing this state
        if (!actionWeights.ContainsKey(key))
        {
            var weights = new Dictionary<MARLBehavior, float>();
            float init = 1f / behaviors.Count;
            foreach (var b in behaviors) weights[b] = init;
            actionWeights[key] = weights;
        }

        var dist = actionWeights[key];

        // Sample action according to weights
        float total = 0f;
        foreach (var w in dist.Values) total += w;

        float roll = Random.value * total;
        float cumulative = 0f;
        foreach (var kv in dist)
        {
            cumulative += kv.Value;
            if (roll <= cumulative)
                return kv.Key;
        }

        return behaviors[0]; // safety fallback
    }

    // --- MODIFIED ---
    // Signature changed to accept 4-part state tuple
    public void UpdateActionWeights((string phase, string style, string movement, string categoryProfile) state, MARLBehavior action, float reward)
    {
        if (string.IsNullOrEmpty(state.phase)) return;

        // --- Ensure state is initialized ---
        if (!actionWeights.ContainsKey(state))
        {
            if (!behaviorMatrix.TryGetValue(state, out var behaviors) || behaviors == null || behaviors.Count == 0)
            {
                var fallbackKey = (state.phase, state.style, state.movement, "Balanced");
                if (!behaviorMatrix.TryGetValue(fallbackKey, out behaviors) || behaviors == null || behaviors.Count == 0)
                {
                    //Debug.LogError($"[MARLManager] Cannot update weights. No behavior list found for state {state} or its fallback.");
                    return;
                }
            }

            var newWeights = new Dictionary<MARLBehavior, float>();
            float init = 1f / behaviors.Count;
            foreach (var b in behaviors) newWeights[b] = init;
            actionWeights[state] = newWeights;
        }

        var stateWeights = actionWeights[state];

        if (!stateWeights.ContainsKey(action))
        {
            //Debug.LogWarning($"[MARLManager] Action {action} not in weight list for {state}. Adding it.");
            stateWeights[action] = 0.01f;
        }

        string before = string.Join(", ", stateWeights.Select(kv => $"{kv.Key}:{kv.Value:F2}"));

        // --- Centered reward update ---
        // Map reward into [-1, 1] where ~10 is neutral
        float centered = Mathf.Clamp(reward / 20f, -1f, 1f);

        // Adaptive "transition" rate: small for neutral, large for extremes
        float transition = 0.15f + 0.35f * Mathf.Abs(centered); // [0.15, 0.5]
        float explorationBias = 0.02f;                          // ensures no action is ever 0

        var keys = stateWeights.Keys.ToList();
        var updatedWeights = new Dictionary<MARLBehavior, float>();

        foreach (var k in keys)
        {
            if (k == action)
            {
                if (centered > 0)
                {
                    // Positive reward → push weight up
                    updatedWeights[k] = (1f - transition) * stateWeights[k] + transition;
                }
                else if (centered < 0)
                {
                    // Negative reward → push weight down
                    updatedWeights[k] = (1f - transition) * stateWeights[k];
                }
                else
                {
                    // Neutral reward → keep stable
                    updatedWeights[k] = stateWeights[k];
                }
            }
            else
            {
                // Non-chosen actions decay slightly if chosen action was positive,
                // or recover slightly if chosen action was negative
                if (centered > 0)
                    updatedWeights[k] = (1f - transition) * stateWeights[k];
                else if (centered < 0)
                    updatedWeights[k] = (1f - transition) * stateWeights[k] + (transition / (keys.Count - 1));
                else
                    updatedWeights[k] = stateWeights[k];
            }
        }

        // Add exploration bias
        foreach (var k in keys)
            updatedWeights[k] += explorationBias;

        // Normalize to sum = 1
        float total = updatedWeights.Values.Sum();
        foreach (var k in keys)
            stateWeights[k] = updatedWeights[k] / total;

        string after = string.Join(", ", stateWeights.Select(kv => $"{kv.Key}:{kv.Value:F2}"));
        //Debug.Log($"[MARLManager] Weights for {state}: BEFORE [{before}] -> AFTER [{after}] (Reward={reward:F2}, Transition={transition:F2})");
    }
    #endregion

    #region TacticalDirector
    // Expose current agents (read-only copy)
    public void ApplyDirectiveBias(GameDirective directive)
    {
        var key = lastState;

        // --- NEW: Validate key before accessing weights ---
        if (string.IsNullOrEmpty(key.phase) ||
            string.IsNullOrEmpty(key.style) ||
            string.IsNullOrEmpty(key.movement) ||
            string.IsNullOrEmpty(key.categoryProfile))
        {
            //Debug.LogWarning($"[MARLManager] Skipping directive bias — incomplete key: ({key.phase}, {key.style}, {key.movement}, {key.categoryProfile})");
            return;
        }

        if (!actionWeights.TryGetValue(key, out var weights))
        {
            //Debug.LogWarning($"[MARLManager] Skipping directive bias — no weights found for key: {key}");
            return;
        }

        // --- Bias logic ---
        if (directive.LootBias == LootBias.DPS)
        {
            BiasWeight(weights, MARLBehavior.Scatter, 0.2f);
            BiasWeight(weights, MARLBehavior.Encircle, 0.1f);
        }
        if (directive.LootBias == LootBias.Survival)
        {
            BiasWeight(weights, MARLBehavior.Aggressive, 0.2f);
            BiasWeight(weights, MARLBehavior.Cluster, 0.1f);
        }

        float total = weights.Values.Sum();
        foreach (var b in weights.Keys.ToList())
            weights[b] /= total;
    }

    private void BiasWeight(Dictionary<MARLBehavior, float> weights, MARLBehavior behavior, float amount)
    {
        if (weights.ContainsKey(behavior))
            weights[behavior] += amount;
    }

    public List<MARLBehavior> GetCandidateBehaviors((string phase, string style, string movement, string categoryProfile) key)
    {
        if (behaviorMatrix.TryGetValue(key, out var list))
            return list;

        var fallbackKey = (key.phase, key.style, key.movement, "Balanced");
        if (key != fallbackKey && behaviorMatrix.TryGetValue(fallbackKey, out var fallbackList))
            return fallbackList;

        return new List<MARLBehavior>();
    }
    public List<MARLAgent> GetAgents()
    {
        return new List<MARLAgent>(agents);
    }

    public void OverrideLastAction(MARLBehavior action)
    {
        lastAction = action;
    }

    #endregion

    #region Optimization
    public void UpdateAgentCache()
    {
        cachedAgents.Clear();
        cachedAgents.AddRange(agents.Where(a => a != null && a.IsAlive));
        // Build spatial grid
        spatialGrid.Clear();
        foreach (var agent in cachedAgents)
        {
            Vector2 pos = agent.transform.position;
            Vector2Int cell = new Vector2Int(Mathf.FloorToInt(pos.x / GRID_CELL_SIZE), Mathf.FloorToInt(pos.y / GRID_CELL_SIZE));
            if (!spatialGrid.ContainsKey(cell)) spatialGrid[cell] = new List<MARLAgent>();
            spatialGrid[cell].Add(agent);
        }
    }

    public void PrecomputeSharedValues()
    {
        // Precompute cluster center (same as before, but cached)
        if (cachedAgents.Count == 0)
        {
            PrecomputedClusterCenter = Vector2.zero;
            return;
        }
        // Find densest cell
        Vector2Int densestCell = spatialGrid.OrderByDescending(kvp => kvp.Value.Count).FirstOrDefault().Key;
        var agentsInCell = spatialGrid.ContainsKey(densestCell) ? spatialGrid[densestCell] : new List<MARLAgent>();
        if (agentsInCell.Count > 0)
        {
            Vector2 sum = Vector2.zero;
            foreach (var agent in agentsInCell) sum += (Vector2)agent.transform.position;
            Vector2 avg = sum / agentsInCell.Count;
            Vector2 playerPos = PredictivePlayerModel.Instance?.CurrentProfile != null ? (Vector2)GameObject.FindGameObjectWithTag("Player").transform.position : Vector2.zero;
            PrecomputedClusterCenter = Vector2.Lerp(avg, playerPos, 0.3f);
        }

        // Precompute separations for Scatter
        PrecomputedSeparations.Clear();
        foreach (var agent in cachedAgents)
        {
            Vector2 separation = Vector2.zero;
            Vector2 agentPos = agent.transform.position;
            Vector2Int cell = new Vector2Int(Mathf.FloorToInt(agentPos.x / GRID_CELL_SIZE), Mathf.FloorToInt(agentPos.y / GRID_CELL_SIZE));
            // Check neighboring cells for nearby agents
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Vector2Int neighborCell = cell + new Vector2Int(x, y);
                    if (spatialGrid.TryGetValue(neighborCell, out var neighbors))
                    {
                        foreach (var neighbor in neighbors)
                        {
                            if (neighbor == agent || !neighbor.IsAlive) continue;
                            Vector2 away = agentPos - (Vector2)neighbor.transform.position;
                            float dist = away.magnitude;
                            if (dist < 3f) separation += away.normalized / Mathf.Max(dist, 0.1f);
                        }
                    }
                }
            }
            PrecomputedSeparations[agent] = separation.normalized;
        }
    }

    // New: Get nearby count using spatial grid
    public int GetNearbyCount(MARLAgent agent, float radius)
    {
        int count = 0;
        Vector2 agentPos = agent.transform.position;
        Vector2Int cell = new Vector2Int(Mathf.FloorToInt(agentPos.x / GRID_CELL_SIZE), Mathf.FloorToInt(agentPos.y / GRID_CELL_SIZE));
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int neighborCell = cell + new Vector2Int(x, y);
                if (spatialGrid.TryGetValue(neighborCell, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor != agent && neighbor.IsAlive && Vector2.Distance(agentPos, neighbor.transform.position) < radius)
                            count++;
                    }
                }
            }
        }
        return count;
    }

    #endregion
}

#region ActionSpace
public enum MARLBehavior
{
    Aggressive,       // rush player
    Encircle,         // circle then collapse
    CounterOffense,   // dodge projectile then punish
    Scatter,          // spread out
    Cluster           // opportunistic blob surge
}
#endregion