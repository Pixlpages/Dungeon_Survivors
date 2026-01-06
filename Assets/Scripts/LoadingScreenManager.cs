using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LoadingScreenManager : MonoBehaviour
{
    public Slider progressBar;
    public TMP_Text progressText;
    public TMP_Text tipText;
    public CanvasGroup fadeGroup;

    [Header("Tips")]
    public string[] tips = new string[]
    {
        "Collect coins dropped by enemies to level up!",
        "Most weapons have a cooldown before firing again automatically!",
        "Try and survive as long as you can!"
    };

    void Start()
    {
        // Set random tip
        if (tips.Length > 0 && tipText != null)
            tipText.text = tips[Random.Range(0, tips.Length)];

        // Start with full fade
        if (fadeGroup != null)
            fadeGroup.alpha = 1f;

        StartCoroutine(LoadSceneWithPreload());
    }

    IEnumerator LoadSceneWithPreload()
    {
        // Fade out
        if (fadeGroup != null)
            yield return Fade(1f, 0f, 0.5f);

        // Preload assets with progress tracking
        yield return PreloadSystemsWithProgress();

        // Ensure bar is at 100% before loading
        UpdateProgress(1f);
        yield return new WaitForSeconds(0.5f);  // Brief pause at 100%

        // Load target scene synchronously via SceneController (find it in the same scene)
        SceneController sceneController = FindObjectOfType<SceneController>();
        if (sceneController != null)
        {
            sceneController.LoadTargetScene();  // Synchronous load
        }
        else
        {
            Debug.LogError("LoadingScreenManager: SceneController not found in scene!");
        }

        // Fade in (may not be needed since scene unloads immediately, but keep for visual)
        if (fadeGroup != null)
            yield return Fade(0f, 1f, 0.5f);
    }

    void UpdateProgress(float progress)
    {
        if (progressBar != null)
            progressBar.value = progress;

        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            if (fadeGroup != null)
                fadeGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        if (fadeGroup != null)
            fadeGroup.alpha = to;
    }

    IEnumerator PreloadSystemsWithProgress()
    {
        Debug.Log("Preloading systems...");
        if (GameBootstrapper.Instance != null)
        {
            // Define preload steps and their weights
            var preloadSteps = new System.Collections.Generic.List<System.Func<IEnumerator>>
            {
                () => GameBootstrapper.Instance.PreloadEnemies(),
                () => GameBootstrapper.Instance.PreloadEffects(),
                () => GameBootstrapper.Instance.PreloadAudio(),
                () => GameBootstrapper.Instance.PreloadOthers()
            };

            float totalSteps = preloadSteps.Count;
            float currentProgress = 0f;

            // Ensure singletons first (no progress for this)
            GameBootstrapper.Instance.EnsureSingletons();

            // Preload with progress
            for (int i = 0; i < preloadSteps.Count; i++)
            {
                yield return preloadSteps[i]();
                currentProgress = (i + 1) / totalSteps;
                UpdateProgress(currentProgress);
                yield return null;  // Smooth update
            }
        }
        else
        {
            Debug.LogWarning("LoadingScreenManager: GameBootstrapper.Instance is null, skipping preload.");
        }
        Debug.Log("Preload complete.");
    }
}
