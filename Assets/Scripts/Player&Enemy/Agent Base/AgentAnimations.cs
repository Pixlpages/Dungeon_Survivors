using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentAnimations : MonoBehaviour
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private IMovement movement;

    private Vector3 baseScale;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        movement = GetComponent<IMovement>();

        baseScale = transform.localScale;
    }

    private void Update()
    {
        if (movement == null) return;

        RotateToPointer(movement.LookDirection);
        PlayAnimation(movement.MoveDirection);
    }

    public void RotateToPointer(Vector2 lookDirection)
    {
        if (lookDirection == Vector2.zero) return;

        // Just flip the sprite
        spriteRenderer.flipX = (lookDirection.x < 0);
    }

    public void PlayAnimation(Vector2 movementInput)
    {
        animator.SetBool("Running", movementInput.magnitude > 0.01f);
    }

    public void PlayHitAnimation() => animator.SetTrigger("GetHit");
    public void PlayDeathAnimation() => animator.SetTrigger("Die");

    public void SetAnimatorController(RuntimeAnimatorController c) //something something, to assign an animator to player
    {
        if (!animator)
            animator = GetComponent<Animator>();

        animator.runtimeAnimatorController = c;
    }
}
