using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WaterDetector : MonoBehaviour
{
    [Header("Visual Offset")]
    public float waterSpriteYOffset = -0.05f;

    private Transform wadeChild;
    public bool IsInWater => isInWater;  // Public property for external access (e.g., sorting)
    [HideInInspector] public Vector3 originalPosition;  // For sorting reference
    private bool isInWater = false;

    void Awake()
    {

        // Find "Wade" child under this object
        Transform found = transform.Find("Wade");
        if (found != null)
        {
            wadeChild = found;
            wadeChild.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"[{name}] Wade child not found!");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Water"))
            return;

        EnterWater();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Water"))
            return;

        ExitWater();
    }

    void EnterWater()
    {
        if (isInWater) return;
        isInWater = true;

        // Enable Wade visual
        if (wadeChild != null)
            wadeChild.gameObject.SetActive(true);
    }

    void ExitWater()
    {
        if (!isInWater) return;
        isInWater = false;

        // Disable Wade visual
        if (wadeChild != null)
            wadeChild.gameObject.SetActive(false);
    }
}