using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MovementArrow : MonoBehaviour
{
    [Header("References")]
    public Image arrowImage;
    
    [Header("Settings")]
    [Tooltip("Distance from player")]
    public float distance = 1.5f;
    
    [Tooltip("Arrow outline color")]
    public Color outlineColor = Color.cyan;
    
    [Tooltip("How quickly the arrow rotates to face movement direction")]
    public float rotationSpeed = 8f;
    
    private PlayerMovement playerMovement;
    private RectTransform rectTransform;
    private Vector2 targetDirection = Vector2.right;
    
    void Start()
    {
        // Get references
        playerMovement = FindObjectOfType<PlayerMovement>();
        Debug.Log("PlayerMovement found: " + (playerMovement != null));
        
        rectTransform = GetComponent<RectTransform>();
        
        // Setup arrow image if not assigned
        if (!arrowImage)
            arrowImage = GetComponent<Image>();
        
        // Create arrow if image is missing
        if (!arrowImage)
        {
            arrowImage = gameObject.AddComponent<Image>();
            arrowImage.sprite = Resources.Load<Sprite>("UI/Arrow") ?? CreateArrowSprite();
        }
        
        // Set arrow color to white
        arrowImage.color = Color.white;
    }
    
    void Update()
    {
        if (!playerMovement)
            return;
        
        // Update target direction based on player's last moved vector
        if (playerMovement.LookDirection != Vector2.zero)
            targetDirection = playerMovement.LookDirection;
        
        // Rotate arrow to face target direction
        float targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
        float currentAngle = rectTransform.eulerAngles.z;
        float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * rotationSpeed);
        rectTransform.eulerAngles = new Vector3(0, 0, newAngle);
        
        // Position arrow around player
        Transform playerTransform = playerMovement.transform;
        Vector3 arrowWorldPos = (Vector3)playerTransform.position + (Vector3)targetDirection * distance;
        rectTransform.position = arrowWorldPos;
    }
    
    // Fallback: Create a simple arrow sprite if none exists
    Sprite CreateArrowSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;
        
        // Draw arrow pointing right
        int centerX = size / 2;
        int centerY = size / 2;
        
        // Arrow head
        for (int x = centerX - 5; x < centerX + 10; x++)
        {
            for (int y = centerY - 8; y < centerY + 8; y++)
            {
                int idx = y * size + x;
                if (idx >= 0 && idx < pixels.Length)
                    pixels[idx] = Color.white;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f);
    }
}