using System.Collections;
using UnityEngine;

public class GameBootstrapper : MonoBehaviour
{
    public static GameBootstrapper Instance;

    [Header("Preload Assets")]
    public GameObject[] enemyPrefabs; // Assign enemy prefabs in Inspector
    public GameObject[] effectPrefabs; // Assign VFX prefabs
    public AudioClip[] audioClips; // Assign audio clips
    public GameObject[] otherPrefabs; //other prefabs to preload

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public IEnumerator PreloadEnemies()
    {
        Debug.Log("[Preload] Enemies...");
        foreach (GameObject prefab in enemyPrefabs)
        {
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab);
                instance.SetActive(false); // Disable to avoid rendering
                yield return null; // Yield to avoid frame drops
            }
        }
        yield return new WaitForSeconds(0.1f);
    }

    public IEnumerator PreloadEffects()
    {
        Debug.Log("[Preload] VFX...");
        foreach (GameObject prefab in effectPrefabs)
        {
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab);
                instance.SetActive(false);
                yield return null;
            }
        }
        yield return new WaitForSeconds(0.1f);
    }

    public IEnumerator PreloadAudio()
    {
        Debug.Log("[Preload] Audio...");
        foreach (AudioClip clip in audioClips)
        {
            if (clip != null)
            {
                // Load into memory (Unity handles caching)
                AudioSource tempSource = gameObject.AddComponent<AudioSource>();
                tempSource.clip = clip;
                tempSource.Play();
                tempSource.Stop();
                Destroy(tempSource);
                yield return null;
            }
        }
        yield return new WaitForSeconds(0.1f);
    }

    public IEnumerator PreloadOthers()
    {
        Debug.Log("[Preload] Others...");
        foreach (GameObject prefab in otherPrefabs)
        {
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab);
                instance.SetActive(false); // Disable to avoid rendering
                yield return null; // Yield to avoid frame drops
            }
        }
        yield return new WaitForSeconds(0.1f);
    }

    // public IEnumerator PreloadMARL()
    // {
    //     Debug.Log("[Preload] MARL/AI...");
    //     // Initialize MDP/MARL if needed
    //     if (MDPManager.Instance == null)
    //     {
    //         // Force initialization
    //         GameObject mdpObj = new GameObject("MDPManager");
    //         mdpObj.AddComponent<MDPManager>();
    //     }
    //     if (MARLManager.Instance == null)
    //     {
    //         GameObject marlObj = new GameObject("MARLManager");
    //         marlObj.AddComponent<MARLManager>();
    //     }
    //     yield return new WaitForSeconds(0.1f);
    // }

    public void EnsureSingletons()
    {
        if (CharacterSelector.instance == null)
        {
            GameObject csObj = new GameObject("CharacterSelector");
            csObj.AddComponent<CharacterSelector>();
            DontDestroyOnLoad(csObj);
            Debug.Log("[Bootstrapper] Created CharacterSelector singleton.");
        }

    }
}