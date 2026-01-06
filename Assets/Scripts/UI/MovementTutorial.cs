using System.Collections;
using UnityEngine;
using TMPro;

[System.Serializable]
public struct TutorialStep
{
    [Tooltip("Text to display for this step")]
    public string message;
    
    [Tooltip("How long this step stays fully visible")]
    public float displayDuration;
    
    [Tooltip("How long this step fades out")]
    public float fadeOutDuration;
    
    [Tooltip("Specific TextMeshProUGUI for this step")]
    public TextMeshProUGUI textObject;
    
    [Tooltip("CanvasGroup for fading this step (including children). If none, one will be added.")]
    public CanvasGroup canvasGroup;
}

public class MovementTutorial : MonoBehaviour
{
    [Header("Tutorial Steps")]
    [Tooltip("List of sequential tutorial messages. Each uses its own text object and CanvasGroup.")]
    public TutorialStep[] tutorialSteps;
    
    [Header("Settings")]
    [Tooltip("Optional: Disable after first movement (applies to all steps)")]
    public bool hideOnFirstMove = true;
    
    private bool hasShown = false;
    private PlayerMovement playerMovement;
    private int currentStepIndex = 0;
    private CanvasGroup currentCanvasGroup;  // Tracks the active CanvasGroup
    
    void Start()
    {
        // Get player movement reference
        playerMovement = FindObjectOfType<PlayerMovement>();
        Debug.Log("PlayerMovement found: " + (playerMovement != null));
        
        // Ensure all CanvasGroups exist and start hidden (use for loop to modify structs)
        for (int i = 0; i < tutorialSteps.Length; i++)
        {
            if (tutorialSteps[i].textObject != null)
            {
                if (tutorialSteps[i].canvasGroup == null)
                {
                    tutorialSteps[i].canvasGroup = tutorialSteps[i].textObject.GetComponent<CanvasGroup>();
                    if (tutorialSteps[i].canvasGroup == null)
                    {
                        tutorialSteps[i].canvasGroup = tutorialSteps[i].textObject.gameObject.AddComponent<CanvasGroup>();
                    }
                }
                tutorialSteps[i].canvasGroup.alpha = 0f;  // Start hidden
            }
        }
        
        // Backward compatibility: If no steps defined, log error
        if (tutorialSteps == null || tutorialSteps.Length == 0)
        {
            Debug.LogError("No tutorial steps defined! Please add at least one step in the Inspector.");
            return;
        }
        
        // Start the tutorial sequence
        if (!hasShown)
        {
            hasShown = true;
            StartCoroutine(ShowTutorialSequence());
        }
    }
    
    void Update()
    {
        // Check if player has moved (hide current step immediately if enabled)
        if (hideOnFirstMove && playerMovement != null && currentCanvasGroup != null && currentCanvasGroup.alpha > 0)
        {
            if (playerMovement.moveDir != Vector2.zero)
            {
                // Player moved - fade out current step immediately and skip to next or end
                StopAllCoroutines();
                StartCoroutine(FadeOutAndAdvance(currentCanvasGroup, 0.5f));
            }
        }
    }
    
    IEnumerator ShowTutorialSequence()
    {
        for (currentStepIndex = 0; currentStepIndex < tutorialSteps.Length; currentStepIndex++)
        {
            TutorialStep step = tutorialSteps[currentStepIndex];
            
            if (step.textObject == null || step.canvasGroup == null)
            {
                Debug.LogWarning($"Tutorial step {currentStepIndex + 1} is missing textObject or canvasGroup! Skipping.");
                continue;
            }
            
            currentCanvasGroup = step.canvasGroup;
            
            // Hide previous CanvasGroup if different
            if (currentStepIndex > 0 && tutorialSteps[currentStepIndex - 1].canvasGroup != currentCanvasGroup)
            {
                tutorialSteps[currentStepIndex - 1].canvasGroup.alpha = 0f;
            }
            
            // Set text and fade in
            step.textObject.text = step.message;
            Debug.Log($"Showing tutorial step {currentStepIndex + 1}: {step.message}");
            yield return StartCoroutine(FadeIn(currentCanvasGroup, 0.5f));  // Quick fade in
            
            // Wait for display duration
            yield return new WaitForSeconds(step.displayDuration);
            
            // Fade out this step
            yield return StartCoroutine(FadeOut(currentCanvasGroup, step.fadeOutDuration));
            
            // Brief pause between steps
            yield return new WaitForSeconds(0.5f);
        }
        
        // All steps complete - destroy the tutorial
        Debug.Log("Tutorial sequence complete.");
        Destroy(gameObject);
    }
    
    IEnumerator FadeIn(CanvasGroup canvasGroup, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
    }
    
    IEnumerator FadeOut(CanvasGroup canvasGroup, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            yield return null;
        }
        
        canvasGroup.alpha = 0f;
    }
    
    // Fade out quickly and advance to next step (for hideOnFirstMove)
    IEnumerator FadeOutAndAdvance(CanvasGroup canvasGroup, float duration)
    {
        yield return StartCoroutine(FadeOut(canvasGroup, duration));
        
        // Skip to next step or end
        currentStepIndex++;
        if (currentStepIndex < tutorialSteps.Length)
        {
            StartCoroutine(ShowTutorialSequence());  // Restart sequence from next step
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
