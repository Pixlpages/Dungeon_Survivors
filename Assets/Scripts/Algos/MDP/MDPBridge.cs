using UnityEngine;

public class MDPBridge : MonoBehaviour
{
    public MDPManager mdp;

    void Awake()
    {
        if (!mdp)
            mdp = MDPManager.Instance;
    }

    // Queue an enemy wave for the given state via MDP.
    public void QueueEnemiesForState(MDPManager.GameState state)
    {
        if (mdp != null)
        {
            mdp?.EnqueueAction(state, MDPManager.GameAction.SpawnEnemies);
        }
    }

    // Event trigger wrapper
    public void QueueRandomEvent(MDPManager.GameState state)
    {
        if (mdp != null)
        {
            mdp?.EnqueueAction(state, MDPManager.GameAction.TriggerMobEvent);
        }
    }

    // Loot adjustment wrapper
    public void AdjustLoot(MDPManager.GameState state)
    {
        mdp?.EnqueueAction(state, MDPManager.GameAction.AdjustLoot);
    }
}
