using UnityEngine;
using UnityEngine.Rendering.Universal;  // For Light2D

[RequireComponent(typeof(Light2D))]
public class LightFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    [Tooltip("Speed of the flicker effect")]
    public float flickerSpeed = 10f;
    [Tooltip("Maximum intensity variation (e.g., 0.5 for ±0.5 around base intensity)")]
    public float flickerAmount = 0.3f;

    private Light2D light2D;
    private float originalIntensity;

    void Start()
    {
        light2D = GetComponent<Light2D>();
        originalIntensity = light2D.intensity;  // Store the original intensity
    }

    void Update()
    {
        // Apply flicker using a sine wave
        float flicker = Mathf.Sin(Time.time * flickerSpeed) * flickerAmount;
        light2D.intensity = originalIntensity + flicker;
    }
}
