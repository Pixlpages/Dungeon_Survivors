using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : Sortable, IMovement
{
    protected Transform player;
    public Transform Player => player;
    protected EnemyStats stats;
    protected Rigidbody2D rb;

    protected Vector2 knockbackVelocity;
    protected float knockbackDuration;

    [Header("Out of Frame Options")]
    float outOfFrameTimer = 0f;
    public float outOfFrameDelay = 3f;
    public enum OutOfFrameAction { none, respawnAtEdge, despawn }
    public OutOfFrameAction outOfFrameAction = OutOfFrameAction.respawnAtEdge;

    [System.Flags]
    public enum KnockbackVariance { duration = 1, velocity = 2}
    public KnockbackVariance knockbackVariance = KnockbackVariance.velocity;


    protected bool spawnedOutOfFrame = false;

    public Vector2 MoveDirection { get; private set; }
    public Vector2 LookDirection { get; private set; }


    protected override void Start()
    {
        base.Start();
        rb = GetComponent<Rigidbody2D>();
        spawnedOutOfFrame = !SpawnManager.IsWithinBoundaries(transform);
        stats = GetComponent<EnemyStats>();


        //Picks a random player on the screen
        PlayerMovement[] allPlayers = FindObjectsOfType<PlayerMovement>();
        player = allPlayers[Random.Range(0, allPlayers.Length)].transform;
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        //if currently being knocked back, then process it
        if (knockbackDuration > 0)
        {
            transform.position += (Vector3)knockbackVelocity * Time.deltaTime;
            knockbackDuration -= Time.deltaTime;

            MoveDirection = knockbackVelocity.normalized;
            LookDirection = MoveDirection;
        }
        else
        {
            Vector2 dir = (player.transform.position - transform.position).normalized;
            MoveDirection = dir;
            LookDirection = dir;

            Move();
            HandleOutOfFrameAction();
        }
    }

    //if the enemy falls outside of the frame
    protected virtual void HandleOutOfFrameAction()
    {
        if (!SpawnManager.IsWithinBoundaries(transform))
        {
            outOfFrameTimer += Time.deltaTime;
            if (outOfFrameTimer >= outOfFrameDelay)
            {
                switch (outOfFrameAction)
                {
                    case OutOfFrameAction.none:
                    default:
                        break;
                    case OutOfFrameAction.respawnAtEdge:
                        transform.position = SpawnManager.GeneratePosition();
                        break;
                    case OutOfFrameAction.despawn:
                        if (!spawnedOutOfFrame)
                        {
                            Destroy(gameObject);
                        }
                        break;
                }
                outOfFrameTimer = 0f;  // Reset timer after action
            }
        }
        else
        {
            outOfFrameTimer = 0f; //reset if back in boundaries
            spawnedOutOfFrame = false;
        }
    }

    //meant to be called from other scripts to create knockback
    public virtual void Knockback(Vector2 velocity, float duration)
    {
        //ignore the knockback if duration is greater than 0
        if (knockbackDuration > 0)
        {
            return;
        }

        //ignore knockback if type is set to none
        if (knockbackVariance == 0) return;

        //only change the factor if the multiplier is not 0 or 1
        float pow = 1;
        bool reducesVelocity = (knockbackVariance & KnockbackVariance.velocity) > 0,
             reducesDuration = (knockbackVariance & KnockbackVariance.duration) > 0;

        if (reducesVelocity && reducesDuration)
            pow = 0.5f;

        //check which knockback values to affect
        knockbackVelocity = velocity * (reducesVelocity ? Mathf.Pow(stats.Actual.knockbackMultiplier, pow) : 1);
        knockbackDuration = duration * (reducesDuration ? Mathf.Pow(stats.Actual.knockbackMultiplier, pow) : 1);

    }

    private Vector2? externalTarget = null;
    private float externalTargetExpiry = 0f; // Time.time when externalTarget becomes invalid

    /// <summary>
    /// Called by MARLAgent to nudge this enemy to a specific world position for a short time.
    /// The target will expire automatically after duration seconds so it cannot become stale.
    //</summary>
    public void SetExternalTarget(Vector2 target, float duration = 0.25f)
    {
        externalTarget = target;
        externalTargetExpiry = Time.time + duration;
    }

    // Replace the existing Move() implementation with this (keep it virtual so derived types can override)
    public virtual void Move()
    {
        if (player == null || stats == null)
            return;

        Vector2 targetPos;
        // use external target only if still valid
        if (externalTarget.HasValue && Time.time <= externalTargetExpiry)
            targetPos = externalTarget.Value;
        else
            targetPos = (Vector2)player.transform.position;

        if (rb)
        {
            rb.MovePosition(Vector2.MoveTowards(
                rb.position,
                targetPos,
                stats.Actual.moveSpeed * Time.deltaTime)
            );
        }
        else
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                targetPos,
                stats.Actual.moveSpeed * Time.deltaTime
            );
        }

        // Update MoveDirection / LookDirection so animations etc. stay correct
        Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
        MoveDirection = dir;
        LookDirection = dir;
    }

    public void ResetMovement()
    {
        // Reset pathfinding, velocity, or targets
        rb.velocity = Vector2.zero;
        SetExternalTarget(transform.position, 0);
    }
    
}