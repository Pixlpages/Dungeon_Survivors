using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameDirective
{
    public TargetState TargetState;
    public LootBias LootBias;
    public WaveType WaveType;
    public float CurseAdjustment;
    public MDPManager.GameAction LastAction;

    public GameDirective(TargetState target, LootBias loot, WaveType wave, float curse, MDPManager.GameAction action)
    {
        TargetState = target;
        LootBias = loot;
        WaveType = wave;
        CurseAdjustment = curse;
        LastAction = action;
    }
}