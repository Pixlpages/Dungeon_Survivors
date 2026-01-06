using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SideWaveCharge : EnemyMovement
{
    private Vector2 chargeDirection;

    // Calculate the straight-line charge direction based on spawn position (towards opposite side)
    protected override void Start()
    {
        base.Start();
        
        // Determine direction based on spawn position relative to screen edges
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 leftEdge = cam.ViewportToWorldPoint(new Vector3(0, 0.5f, cam.nearClipPlane));
            Vector3 rightEdge = cam.ViewportToWorldPoint(new Vector3(1, 0.5f, cam.nearClipPlane));
            Vector3 topEdge = cam.ViewportToWorldPoint(new Vector3(0.5f, 1, cam.nearClipPlane));
            Vector3 bottomEdge = cam.ViewportToWorldPoint(new Vector3(0.5f, 0, cam.nearClipPlane));

            // Check which side the enemy spawned on and set direction towards opposite
            if (transform.position.x < leftEdge.x + 1f)  // Spawned on left
            {
                chargeDirection = Vector2.right;
            }
            else if (transform.position.x > rightEdge.x - 1f)  // Spawned on right
            {
                chargeDirection = Vector2.left;
            }
            else if (transform.position.y > topEdge.y - 1f)  // Spawned on top
            {
                chargeDirection = Vector2.down;
            }
            else if (transform.position.y < bottomEdge.y + 1f)  // Spawned on bottom
            {
                chargeDirection = Vector2.up;
            }
            else
            {
                // Fallback: move towards center if position is ambiguous
                Vector2 screenCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, cam.nearClipPlane));
                chargeDirection = (screenCenter - (Vector2)transform.position).normalized;
            }
        }
        else
        {
            // No camera, fallback to right
            chargeDirection = Vector2.right;
        }
    }

    // Move in the locked straight-line direction
    public override void Move()
    {
        transform.position += (Vector3)chargeDirection * stats.Actual.moveSpeed * Time.deltaTime;
    }
}
