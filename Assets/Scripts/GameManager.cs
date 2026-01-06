using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;


//have dependencies and references flow to the gamemanager instead of the other way
//the game manager is independent from other scripts
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameState //define the different states of the game
    {
        Gameplay, Paused, Gameover, LevelUp
    }

    public GameState currentState; //stores the current state of the game
    public GameState previousState; //store the previous state of the game

    [Header("Screens")]
    public GameObject pauseScreen;
    public GameObject resultsScreen;
    public GameObject levelUpScreen;
    //public GameObject selectionBorder;
    public GameObject uiScreen;
    int stackedLevelUps = 0;

    [Header("Damage Text")]
    public Canvas damageTextCanvas; //canvas to draw text from
    public float textFontSize = 20;
    public GameObject damageTextPrefab;
    public TMP_FontAsset textFont;
    public Camera referenceCamera; //camera to convert world coodinates to screen coordinates

    [Header("Stat Displays")]
    public TMP_Text currentHealthDisplay;
    public TMP_Text currentRecoveryDisplay;
    public TMP_Text currentMoveSpeedDisplay;
    public TMP_Text currentMightDisplay;
    public TMP_Text currentProjectileSpeedDisplay;
    public TMP_Text currentMagnetDisplay;

    [Header("Results Screen Displays")]
    public Image chosenCharacterImage;
    public TMP_Text chosenCharacterName;
    public TMP_Text levelReachedDisplay;
    public TMP_Text timeSurvivedDisplay;


    [Header("StopWatch")]
    public float timeLimit; //time limit in seconds
    float stopwatchTime; //current time elapsed
    private float lateGameCurseTimer = 0f; //curse timer
    public TMP_Text stopwatchDisplay;

    //Helpers
    public bool isGameOver { get { return currentState == GameState.Gameover; } }
    public bool choosingUpgrades { get { return currentState == GameState.LevelUp; }}

    //Tracks all players
    PlayerStats[] players;

    public float GetElapsedTime()
    {
        return stopwatchTime;
    }

    //sums up the curse stat of all players and returns the value
    public static float GetCumulativeCurse()
    {
        if (!Instance) return 0f;

        float totalCurse = 0f;
        foreach (PlayerStats p in Instance.players)
            totalCurse += p.Actual.curse; // raw player curse value

        // Add CurseManager bias as additive offset
        if (CurseManager.Instance != null)
            totalCurse += CurseManager.Instance.GetCurseBias();

        return totalCurse; // 0 = no increase, 0.1 = +10%, etc.
    }

    public static float GetCumulativeLevels()
    {
        if (!Instance) return 1f; // baseline multiplier

        float totalLevelOffset = 0f;
        foreach (PlayerStats p in Instance.players)
        {
            // Level 1 = +0%, Level 2 = +1%, etc.
            totalLevelOffset += (p.level - 1) * 0.01f;
        }

        return 1f + totalLevelOffset; // multiplier
    }

    void Awake()
    {
        players = FindObjectsOfType<PlayerStats>();
        SpawnManager.instance?.ClearAllEnemies();

        if (Instance == null) //the usual GameManager Instance checker
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Extra " + this + "Deleted");
            Destroy(gameObject);
        }

        if (referenceCamera == null)
        {
            referenceCamera = Camera.main;
            if (referenceCamera == null)
                Debug.LogWarning("No Camera found at Awake! Will recheck in Start.");
        }

        DisableScreens();
        uiScreen.SetActive(true);
    }

    void Start()
    {
        if (referenceCamera == null)
        {
            referenceCamera = Camera.main;
            if (referenceCamera != null)
                Debug.Log("Camera reference successfully reattached in Start.");
            else
                Debug.LogError("GameManager: No camera found after scene load!");
        }

    }

    void OnDestroy()
{
    Debug.Log($"[GameManager] OnDestroy: Destroyed in scene '{gameObject.scene.name}' at frame {Time.frameCount}, time {Time.time}. Call stack:\n{System.Environment.StackTrace}");
    if (Instance == this)
    {
        Instance = null;
    }
}

    void Update()
    {
        switch (currentState) //switch case for the current game state,
                              //codes under the cases only run when that specific gamestate is currently active, very easy to handle sh*t
        {
            case GameState.Gameplay:
                CheckForPauseAndResume();
                UpdateStopwatch();
                break;

            case GameState.Paused:
                CheckForPauseAndResume();
                break;
            case GameState.LevelUp:
                break;

            default:
                Debug.LogWarning("State No Exist");
                break;
        }

        //MDP Event testing tool
        if (Input.GetKeyDown(KeyCode.Alpha1))
        { 
            MDPManager.Instance.StepAndApply(MDPManager.GameAction.SpawnEnemies);
            Debug.Log("[TestTool] Forced MDP Action: Spawn Enemies");
        }

        if (Input.GetKeyDown(KeyCode.Alpha2)) 
        {           
            MDPManager.Instance.StepAndApply(MDPManager.GameAction.TriggerMobEvent);
            Debug.Log("[TestTool] Forced MDP Action: Trigger Mob Event");
        }
        
        if (Input.GetKeyDown(KeyCode.Alpha3)) 
        {
            MDPManager.Instance.StepAndApply(MDPManager.GameAction.AdjustCurse);
            Debug.Log("[TestTool] Forced MDP Action: Adjust Curse");
        }

        if (Input.GetKeyDown(KeyCode.Alpha4)) 
        {
            MDPManager.Instance.StepAndApply(MDPManager.GameAction.AdjustLoot);
            Debug.Log("[TestTool] Forced MDP Action: AdjustLoot");
        }

        // MARL Behavior Testing Tool (Force Override)
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            MARLManager.Instance.CurrentBehavior = MARLBehavior.Aggressive;
            AssignBehaviorToAgents(MARLBehavior.Aggressive);
            Debug.Log("[TestTool] Forced MARL behavior: Aggressive");
        }

        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            MARLManager.Instance.CurrentBehavior = MARLBehavior.Encircle;
            AssignBehaviorToAgents(MARLBehavior.Encircle);
            Debug.Log("[TestTool] Forced MARL behavior: Encircle");
        }

        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            MARLManager.Instance.CurrentBehavior = MARLBehavior.Scatter;
            AssignBehaviorToAgents(MARLBehavior.Scatter);
            Debug.Log("[TestTool] Forced MARL behavior: Scatter");
        }

        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            MARLManager.Instance.CurrentBehavior = MARLBehavior.Cluster;
            AssignBehaviorToAgents(MARLBehavior.Cluster);
            Debug.Log("[TestTool] Forced MARL behavior: Cluster");
        }

        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            MARLManager.Instance.CurrentBehavior = MARLBehavior.CounterOffense;
            AssignBehaviorToAgents(MARLBehavior.CounterOffense);
            Debug.Log("[TestTool] Forced MARL behavior: CounterOffense");
        }
        
        
    }

    private void AssignBehaviorToAgents(MARLBehavior behavior)
    {
        if (MARLManager.Instance == null) return;
        
        foreach (var agent in MARLManager.Instance.GetAgents())
        {
            if (agent != null && agent.IsAlive)
            {
                agent.SetBehavior(behavior);
            }
        }
    }

    public void ChangeState(GameState newState) //defines the method to change the state of the game
    {
        previousState = currentState;
        currentState = newState;
    }

    public void PauseGame()
    {
        if (currentState != GameState.Paused)
        {
            ChangeState(GameState.Paused);
            Time.timeScale = 0f;
            pauseScreen.SetActive(true);
            Debug.Log("Game is Paused");
        }
    }

    public void ResumeGame()
    {
        if (currentState == GameState.Paused)
        {
            ChangeState(previousState);
            Time.timeScale = 1f;
            pauseScreen.SetActive(false);
            Debug.Log("Game is Resumed");
        }
    }

    public void CheckForPauseAndResume()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton9))
        {
            if (currentState == GameState.Paused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void GameOver()
    {
        timeSurvivedDisplay.text = stopwatchDisplay.text;

        //set the Game Over Variables here
        ChangeState(GameState.Gameover);
        Time.timeScale = 0f;
        DisplayResults();

        // Get the highest level from all players
        int highestLevel = 1;
        foreach (PlayerStats p in players)
        {
            if (p.level > highestLevel)
                highestLevel = p.level;
        }

        if (PlaytestLogger.Instance != null)
        {
            PlaytestLogger.Instance.LogGameOver(stopwatchTime, highestLevel);
        }
    }

    public void DisplayResults()
    {
        resultsScreen.SetActive(true);
        uiScreen.SetActive(false);
    }

    void DisableScreens()
    {
        pauseScreen.SetActive(false);
        resultsScreen.SetActive(false);
        levelUpScreen.SetActive(false);
        //selectionBorder.SetActive(false);
    }

    public void AssignChosenCharacterUI(CharacterData chosenCharacterData)
    {
        chosenCharacterImage.sprite = chosenCharacterData.Icon;
        chosenCharacterName.text = chosenCharacterData.name;
    }

    public void AssignLevelReachedUI(int levelReachedData)
    {
        levelReachedDisplay.text = levelReachedData.ToString();
    }

    void UpdateStopwatch()
    {
        stopwatchTime += Time.deltaTime;
        UpdateStopwatchDisplay();

        // NEW: Late-game curse scaling - increase by 5% every 10 seconds starting at ~8 minutes
        if (stopwatchTime >= 480 && CurseManager.Instance != null)  // 8 minutes = 480
        {
            lateGameCurseTimer += Time.deltaTime;
            if (lateGameCurseTimer >= 10f)  // Every 10 seconds
            {
                lateGameCurseTimer = 0f;  // Reset timer
                
                CurseManager.Instance.AddCurseBias(0.05f);
            }
        }

        //added just for testing purposes, or if we want a time limit
        //idk actually...
        if (stopwatchTime >= timeLimit)
        {
            foreach(PlayerStats p in players)
                p.SendMessage("Kill");
        }
    }

    void UpdateStopwatchDisplay() //calculates and updates the stopwatch timer
    {
        int minutes = Mathf.FloorToInt(stopwatchTime / 60);
        int seconds = Mathf.FloorToInt(stopwatchTime % 60);

        stopwatchDisplay.text = string.Format("{0:00} : {1:00}", minutes, seconds);
    }

    public void StartLevelUp()
    {
        ChangeState(GameState.LevelUp);

        //if the level up screen is already active, then we record it
        if (levelUpScreen.activeSelf)
            stackedLevelUps++;
        else
        {
            Time.timeScale = 0f;
            levelUpScreen.SetActive(true);
            //selectionBorder.SetActive(true);
            foreach (PlayerStats p in players)
                p.SendMessage("RemoveAndApplyUpgrades");
        }
    }

    public void EndLevelUp()
    {
        Time.timeScale = 1f;
        levelUpScreen.SetActive(false);
        //selectionBorder.SetActive(false);
        ChangeState(GameState.Gameplay);

        if (stackedLevelUps > 0)
        {
            stackedLevelUps--;
            StartLevelUp();
        }
    }

    public static void GenerateFloatingText(string text, Transform target, float duration = 1f, float speed = 1f)
    {
        if (!Instance.damageTextCanvas) return;
        if (!Instance.referenceCamera) Instance.referenceCamera = Camera.main;

        Instance.StartCoroutine(Instance.GenerateFloatingTextCoroutine(text, target, duration, speed));
    }

    IEnumerator GenerateFloatingTextCoroutine(string text, Transform target, float duration = 1f, float speed = 50f)
    {
        var tmPro = DamageTextPool.Instance.Get();
        tmPro.text = text;
        tmPro.fontSize = textFontSize;
        tmPro.color = Color.white;

        RectTransform rect = tmPro.GetComponent<RectTransform>();
        rect.position = referenceCamera.WorldToScreenPoint(target.position);

        float t = 0f;
        float yOffset = 0f;
        while (t < duration)
        {
            if (target)
            {
                yOffset += speed * Time.deltaTime;
                rect.position = referenceCamera.WorldToScreenPoint(target.position + new Vector3(0, yOffset));
            }
            tmPro.color = new Color(tmPro.color.r, tmPro.color.g, tmPro.color.b, 1 - t / duration);

            yield return null;
            t += Time.deltaTime;
        }

        DamageTextPool.Instance.Return(tmPro);
    }

}