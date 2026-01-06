using UnityEngine;

public class SpriteRotator : MonoBehaviour
{
    public float rotationSpeed = 100f;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (spriteRenderer != null)
        {
            // Apply rotation only to the sprite's local transform matrix
            // without affecting the GameObject's position or children.
            spriteRenderer.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }
    }
}
