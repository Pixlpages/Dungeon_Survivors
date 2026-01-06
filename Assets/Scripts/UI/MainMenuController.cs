using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;  // Add this for Stack

public class MainMenuController : MonoBehaviour
{
    // Drag your CreditsPopup panel from the Hierarchy into this slot in the Inspector
    public GameObject creditsPopup;
    public GameObject tutorialPopup;
    public GameObject settingsPopup;
    public GameObject aboutPopup;
    public GameObject mainPopup;

    [Header("Website URL")]
    public string websiteURL1;
    public string websiteURL2;

    private Stack<GameObject> popupHistory = new Stack<GameObject>();  // Track popup history

    void Awake()
    {
        SelectFirstButton(mainPopup);
        popupHistory.Push(mainPopup);  // Main menu as base
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseCurrentPopup();
        }
    }

    private void CloseCurrentPopup()
    {
        if (popupHistory.Count > 1)  // If there's a popup open
        {
            // Hide current popup
            GameObject currentPopup = popupHistory.Pop();
            currentPopup.SetActive(false);

            // Show previous (main menu or another popup)
            GameObject previous = popupHistory.Peek();
            previous.SetActive(true);
            SelectFirstButton(previous);
        }
        else
        {
            // No popup open, do nothing or exit game
            Debug.Log("No popup to close. Press again to exit.");
            // Optional: ExitGame(); if you want double-Escape to quit
        }
    }

    public void ShowSettings()
    {
        if (settingsPopup != null)
        {
            settingsPopup.SetActive(true);
            popupHistory.Push(settingsPopup);
            SelectFirstButton(settingsPopup);
        }
    }

    public void HideSettings()
    {
        if (settingsPopup != null)
        {
            settingsPopup.SetActive(false);
            if (popupHistory.Count > 0 && popupHistory.Peek() == settingsPopup)
            {
                popupHistory.Pop();
            }
        }
    }
    
    public void ShowMainMenu()
    {
        if (mainPopup != null)
        {
            mainPopup.SetActive(true);
            popupHistory.Push(mainPopup);
            SelectFirstButton(mainPopup);
        }
    }

    public void HideMainMenu()
    {
        if (mainPopup != null)
        {
            mainPopup.SetActive(false);
            if (popupHistory.Count > 0 && popupHistory.Peek() == mainPopup)
            {
                popupHistory.Pop();
            }
        }
    }

    public void ShowAboutUs()
    {
        if (aboutPopup != null)
        {
            aboutPopup.SetActive(true);
            popupHistory.Push(aboutPopup);
            SelectFirstButton(aboutPopup);
        }
    }
    
    public void HideAboutUs()
    {
        if (aboutPopup != null)
        {
            aboutPopup.SetActive(false);
            if (popupHistory.Count > 0 && popupHistory.Peek() == aboutPopup)
            {
                popupHistory.Pop();
            }
        }
    }

    public void ShowCredits()
    {
        if (creditsPopup != null)
        {
            creditsPopup.SetActive(true);
            popupHistory.Push(creditsPopup);
            SelectFirstButton(creditsPopup);
        }
    }

    public void HideCredits()
    {
        if (creditsPopup != null)
        {
            creditsPopup.SetActive(false);
            if (popupHistory.Count > 0 && popupHistory.Peek() == creditsPopup)
            {
                popupHistory.Pop();
            }
        }
    }

    public void ShowTutorial()
    {
        if (tutorialPopup != null)
        {
            tutorialPopup.SetActive(true);
            popupHistory.Push(tutorialPopup);
            SelectFirstButton(tutorialPopup);
        }
    }
    
    public void HideTutorial()
    {
        if (tutorialPopup != null)
        {
            tutorialPopup.SetActive(false);
            if (popupHistory.Count > 0 && popupHistory.Peek() == tutorialPopup)
            {
                popupHistory.Pop();
            }
        }
    }

    public void ExitGame()
    {
        UnityEngine.Debug.Log("Exiting game...");
        #if UNITY_EDITOR
            // In the Unity Editor, stop playing the scene
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            // In a built game, quit the application
            Application.Quit();
        #endif
    }

    public void OpenWebsite1()
    {
        if (!string.IsNullOrEmpty(websiteURL1))
        {
            UnityEngine.Debug.Log($"Opening website: {websiteURL1}");
            Application.OpenURL(websiteURL1);
        }
        else
        {
            UnityEngine.Debug.LogWarning("Website URL is not set!");
        }
    }

    public void OpenWebsite2()
    {
        if (!string.IsNullOrEmpty(websiteURL2))  // Fixed
        {
            UnityEngine.Debug.Log($"Opening website: {websiteURL2}");
            Application.OpenURL(websiteURL2);
        }
        else
        {
            UnityEngine.Debug.LogWarning("Website URL is not set!");
        }
    }

    public void OpenCSVFolder()
    {
        if (PlaytestLogger.Instance != null)
        {
            PlaytestLogger.Instance.OpenCSVFolder();
        }
        else
        {
            UnityEngine.Debug.LogWarning("PlaytestLogger instance not found!");
        }
    }

    public void ResetPlaytestData()
    {
        if (PlaytestLogger.Instance != null)
        {
            PlaytestLogger.Instance.ResetPlaytestData();
            UnityEngine.Debug.Log("Playtest data reset via button.");
        }
        else
        {
            UnityEngine.Debug.LogWarning("PlaytestLogger instance not found!");
        }
    }

    public void SelectCharacter(CharacterData data)
    {
        if (CharacterSelector.instance != null)
        {
            CharacterSelector.instance.SelectCharacter(data);
            Debug.Log("Character selected: " + data.name);
        }
        else
        {
            Debug.LogError("CharacterSelector instance not found!");
        }
    }

    public void EnableCombinedMode()
    {
        SetCombinedMode(true);
    }
    public void DisableCombinedMode()
    {
        SetCombinedMode(false);
    }
    private void SetCombinedMode(bool enable)
    {
        // Save to PlayerPrefs for persistence
        PlayerPrefs.SetInt("EnableCombinedMode", enable ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[MainMenu] Combined mode set to {enable} and saved to PlayerPrefs.");
    }

    private void SelectFirstButton(GameObject panel)
    {
        if (panel == null) return;

        // Find the first active Button in this panel
        Button firstButton = panel.GetComponentInChildren<Button>(true);

        if (firstButton != null)
        {
            // Clear previous selection first
            EventSystem.current.SetSelectedGameObject(null);
            // Set the new selection
            EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
        }
        else
        {
            Debug.LogWarning($"No Button found inside {panel.name}");
        }
    }
}
