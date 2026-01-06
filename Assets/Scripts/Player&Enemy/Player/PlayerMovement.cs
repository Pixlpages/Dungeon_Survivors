using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Terresquall;

public class PlayerMovement : Sortable, IMovement
{
    [HideInInspector] public Vector2 moveDir;
    [HideInInspector] public Vector2 lastMovedVector;
    public const float DEFAULT_MOVESPEED = 5f;

    private Rigidbody2D rb;
    PlayerStats player;

    public Vector2 MoveDirection => moveDir;
    public Vector2 LookDirection => lastMovedVector; // faces where last moved

    protected override void Start()
    {
        base.Start();
        player = GetComponent<PlayerStats>();
        rb = GetComponent<Rigidbody2D>();
        lastMovedVector = Vector2.right;
    }

    void Update()
    {
        InputManagement();
    }

    void FixedUpdate()
    {
        Move();
    }

    void InputManagement()
    {
        if (GameManager.Instance.isGameOver) return;

        float moveX, moveY;
        if (VirtualJoystick.CountActiveInstances() > 0)
        {
            moveX = VirtualJoystick.GetAxisRaw("Horizontal");
            moveY = VirtualJoystick.GetAxisRaw("Vertical");
        }
        else
        {
            moveX = Input.GetAxisRaw("Horizontal");
            moveY = Input.GetAxisRaw("Vertical");
        }

        moveDir = new Vector2(moveX, moveY).normalized;

        if (moveDir != Vector2.zero)
            lastMovedVector = moveDir;
    }

    void Move()
    {
        if (GameManager.Instance.isGameOver) return;
        rb.velocity = moveDir * DEFAULT_MOVESPEED * player.Stats.moveSpeed;
    }
}
