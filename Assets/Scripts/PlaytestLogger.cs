using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Diagnostics;
#if UNITY_ANDROID || UNITY_IOS
// no namespace needed; NativeShare is in global scope
#endif

public class PlaytestLogger : MonoBehaviour
{
    public static PlaytestLogger Instance;

    private string csvFilePath;
    private float totalPlaytime = 0f;
    private int retryCount = 0;
    private int highestLevel = 1;  // All-time highest level
    private float sessionStartTime;  // Track when the session started for survival time on quit

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);  // Only persist this object, not the parent
        }
        else
        {
            Destroy(gameObject);  // Destroy duplicates
        }
        InitializeCSV();
        LoadPlaytestData();
        sessionStartTime = Time.time;  // Record session start time
    }

    void Update()
    {
        // Track total playtime continuously, only when the game is running
        if (GameManager.Instance != null && !GameManager.Instance.isGameOver)
        {
            totalPlaytime += Time.deltaTime;
        }
    }

    void InitializeCSV()
    {
        // Create CSV in persistent data path (works across platforms)
        csvFilePath = Path.Combine(Application.persistentDataPath, "playtest_data.csv");
        UnityEngine.Debug.Log("Persistent path: " + Application.persistentDataPath);
        // Create CSV with headers if it doesn't exist
        if (!File.Exists(csvFilePath))
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Timestamp,Total Playtime (seconds),Survival Time (seconds),Retry Count,Level Reached,Mode");  // Added "Mode" column
            File.WriteAllText(csvFilePath, sb.ToString());
            UnityEngine.Debug.Log($"Created playtest CSV at: {csvFilePath}");
        }
    }

    void LoadPlaytestData()
    {
        // Load persistent data from PlayerPrefs
        totalPlaytime = PlayerPrefs.GetFloat("TotalPlaytime", 0f);
        retryCount = PlayerPrefs.GetInt("RetryCount", 0);
        highestLevel = PlayerPrefs.GetInt("HighestLevel", 1);
    }

    void SavePlaytestData()
    {
        // Save persistent data to PlayerPrefs
        PlayerPrefs.SetFloat("TotalPlaytime", totalPlaytime);
        PlayerPrefs.SetInt("RetryCount", retryCount);
        PlayerPrefs.SetInt("HighestLevel", highestLevel);
        PlayerPrefs.Save();
    }

    public void LogGameOver(float survivalTime, int levelReached)
    {
        // Update retry count
        retryCount++;

        // Update highest level if this is a new record (all-time high)
        if (levelReached > highestLevel)
        {
            highestLevel = levelReached;
        }

        // Save data
        SavePlaytestData();

        // Write to CSV (log the level reached in this run and mode)
        WriteToCSV(survivalTime, levelReached);

        UnityEngine.Debug.Log($"Playtest data logged to: {csvFilePath}");
    }

    void WriteToCSV(float survivalTime, int levelReached)
    {
        try
        {
            // Get the mode from PlayerPrefs
            bool isCombined = PlayerPrefs.GetInt("EnableCombinedMode", 1) == 1;
            string mode = isCombined ? "Combined" : "Standalone";

            StringBuilder sb = new StringBuilder();
            
            // Format: Timestamp, Total Playtime, Survival Time, Retry Count, Level Reached, Mode
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.Append(",");
            sb.Append(totalPlaytime.ToString("F2"));
            sb.Append(",");
            sb.Append(survivalTime.ToString("F2"));
            sb.Append(",");
            sb.Append(retryCount);
            sb.Append(",");
            sb.Append(levelReached);
            sb.Append(",");
            sb.Append(mode);  // Added mode
            sb.AppendLine();

            // Append to existing CSV
            File.AppendAllText(csvFilePath, sb.ToString());
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to write to CSV: {e.Message}");
        }
    }

    public void ResetPlaytestData()
    {
        // Reset in-memory data
        totalPlaytime = 0f;
        retryCount = 0;
        highestLevel = 1;
        // Save to PlayerPrefs
        SavePlaytestData();
        // Reset CSV: Delete and recreate with headers
        try
        {
            if (File.Exists(csvFilePath))
            {
                File.Delete(csvFilePath);
                UnityEngine.Debug.Log("Deleted existing CSV file.");
            }
            // Recreate CSV with updated headers
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Timestamp,Total Playtime (seconds),Survival Time (seconds),Retry Count,Level Reached,Mode");  // Updated header with Mode
            File.WriteAllText(csvFilePath, sb.ToString());
            UnityEngine.Debug.Log($"Reset playtest CSV at: {csvFilePath}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to reset CSV: {e.Message}");
        }
    }

    // Public getters for UI display if needed
    public float GetTotalPlaytime() => totalPlaytime;
    public int GetRetryCount() => retryCount;
    public int GetHighestLevel() => highestLevel;  // All-time highest
    public string GetCSVPath() => csvFilePath;

    void OnApplicationQuit()
    {
        // If the game is still running (not game over), log the current run before quitting
        if (GameManager.Instance != null && !GameManager.Instance.isGameOver)
        {
            float survivalTime = Time.time - sessionStartTime;  // Calculate survival time from session start
            
            // Get the highest level from all players (mirroring GameManager.GameOver logic)
            int currentLevel = 1;
            PlayerStats[] players = FindObjectsOfType<PlayerStats>();  // Find players dynamically
            foreach (PlayerStats p in players)
            {
                if (p.level > currentLevel)
                    currentLevel = p.level;
            }
            
            LogGameOver(survivalTime, currentLevel);
            UnityEngine.Debug.Log("Logged incomplete run on quit.");
        }
        
        // Save data when application closes
        SavePlaytestData();
    }

    public void OpenCSVFolder()
    {
        string folderPath = Path.GetDirectoryName(csvFilePath);

    #if UNITY_ANDROID
        // On Android, use NativeShare to share the CSV file directly
        if (System.IO.File.Exists(csvFilePath))
        {
            new NativeShare()
                .AddFile(csvFilePath)
                .SetSubject("Playtest Data")
                .SetText("Here is my playtest CSV log from the game.")
                .Share();
            
            UnityEngine.Debug.Log("Shared CSV file via Android share sheet.");
        }
        else
        {
            UnityEngine.Debug.LogWarning("CSV file not found to share!");
        }

    #elif UNITY_STANDALONE || UNITY_EDITOR
        // On PC or Editor, just open the folder normally
        if (System.IO.Directory.Exists(folderPath))
        {
            try
            {
                System.Diagnostics.Process.Start(folderPath);
                UnityEngine.Debug.Log($"Opened CSV folder: {folderPath}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to open CSV folder: {e.Message}");
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning($"CSV folder does not exist: {folderPath}");
        }
    #else
        // For other platforms (iOS, consoles, etc.)
        UnityEngine.Debug.Log($"CSV path: {csvFilePath}");
    #endif
    }

}
