using UnityEngine;

public class CurseManager : MonoBehaviour
{
    public static CurseManager Instance;

    [Header("Curse Settings")]
    [Tooltip("Each step is +10% difficulty.")]

    public bool enableDetailedLogs = true;
    public float curseStep = 0.1f;

    private float curseBias = 0f; // MDP-adjustable curse (can increase/decrease)
    private float lateGameCurseBias = 0f; // Late-game scaling curse (only increases)

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Convenience wrappers (backward compatibility, readability)
    public void IncreaseCurse()
    {
        AdjustCurse(+1);
    }

    public void DecreaseCurse()
    {
        AdjustCurse(-1);
    }

    // Primary entry point for MDP severity-based adjustments (affects curseBias only)
    public void AdjustCurse(int steps)
    {
        if (steps == 0) return;

        float newBias = curseBias + steps * curseStep;
        curseBias = Mathf.Max(0f, newBias);  // MDP curse can go down to 0

        ApplyCurseEffects();
        if (enableDetailedLogs) Debug.Log($"[CurseManager] MDP curse adjusted by {steps}, MDP bias: {curseBias:P0}, Total bias: {GetCurseBias():P0}");
    }

    // Add to late-game curse bias (only increases, unaffected by MDP)
    public void AddCurseBias(float amount)
    {
        lateGameCurseBias += amount;
        lateGameCurseBias = Mathf.Max(0f, lateGameCurseBias);  // Ensure non-negative
        ApplyCurseEffects();
        if (enableDetailedLogs) Debug.Log($"[CurseManager] Late-game curse added by {amount:P0}, Late-game bias: {lateGameCurseBias:P0}, Total bias: {GetCurseBias():P0}");
    }

    // Total curse bias (MDP + late-game)
    public float GetCurseBias() => curseBias + lateGameCurseBias;

    private void ApplyCurseEffects()
    {
        if (SpawnManager.instance)
            SpawnManager.instance.boostedByCurse = true;

        if (enableDetailedLogs) Debug.Log($"[CurseManager] Total curse bias: {GetCurseBias():P0}");
    }

    public float GetCurseMultiplier()
    {
        // 0 = no increase, 0.1 = +10%
        return 1f + GetCurseBias();
    }
}
