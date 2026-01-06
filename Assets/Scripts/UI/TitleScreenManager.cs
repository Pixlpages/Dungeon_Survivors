using UnityEngine;
using UnityEngine.UI;

public class TitleScreenManager : MonoBehaviour
{
    [Header("Button References")]
    public Button exitButton;
    public Button websiteButton;
    public Button openCSVFolderButton;  // New button for opening CSV folder

    [Header("Website URL")]
    public string websiteURL = "https://www.example.com";

    void Start()
    {
        if (exitButton != null)
            exitButton.onClick.AddListener(ExitGame);

        if (websiteButton != null)
            websiteButton.onClick.AddListener(OpenWebsite);

        if (openCSVFolderButton != null)
            openCSVFolderButton.onClick.AddListener(OpenCSVFolder);
    }

    void ExitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    void OpenWebsite()
    {
        if (!string.IsNullOrEmpty(websiteURL))
            Application.OpenURL(websiteURL);
        else
            Debug.LogWarning("Website URL is not set!");
    }

    void OpenCSVFolder()
    {
        if (PlaytestLogger.Instance != null)
        {
            PlaytestLogger.Instance.OpenCSVFolder();
        }
        else
        {
            Debug.LogWarning("PlaytestLogger instance not found!");
        }
    }
}