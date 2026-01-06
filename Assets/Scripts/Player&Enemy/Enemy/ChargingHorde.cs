using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChargingHorde : EnemyMovement
{
    Vector2 chargeDirection;

    //we calculate the direction where the enemy charges towards first, i.e. where we are when they spawn
    protected override void Start()
    {
        base.Start();
        chargeDirection = (player.transform.position - transform.position).normalized;
    }

    //instead of moving towards the player, we just move towards the direction we are charging towards
    public override void Move()
    {
        transform.position += (Vector3)chargeDirection * stats.Actual.moveSpeed * Time.deltaTime;
    }
}
