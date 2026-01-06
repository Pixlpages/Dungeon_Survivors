using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DamageTextPool : MonoBehaviour
{
    public static DamageTextPool Instance;

    [Header("Pool Settings")]
    public int initialPoolSize = 30; // You can tweak this based on your game’s density
    private Queue<TMP_Text> pool = new Queue<TMP_Text>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Prewarm the pool
        if (GameManager.Instance && GameManager.Instance.damageTextPrefab)
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreateNewDamageText();
            }
        }
        else
        {
            Debug.LogWarning("DamageTextPool: GameManager or damageTextPrefab not found.");
        }
    }

    private TMP_Text CreateNewDamageText()
    {
        GameObject obj = Instantiate(
            GameManager.Instance.damageTextPrefab, 
            GameManager.Instance.damageTextCanvas.transform
        );

        obj.SetActive(false);

        TMP_Text text = obj.GetComponent<TMP_Text>();
        if (text == null)
        {
            Debug.LogError("DamageTextPool: Prefab missing TMP_Text component!");
            return null;
        }

        // Reset important rect fields
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        pool.Enqueue(text);
        return text;
    }

    public void Return(TMP_Text text)
    {
        text.gameObject.SetActive(false);

        // Reset position before pooling again
        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;

        pool.Enqueue(text);
    }


    public TMP_Text Get()
    {
        TMP_Text text;
        if (pool.Count > 0)
        {
            text = pool.Dequeue();
        }
        else
        {
            text = CreateNewDamageText();
        }

        text.gameObject.SetActive(true);
        return text;
    }

    public void Clear()
    {
        foreach (var text in pool)
        {
            if (text != null)
                Destroy(text.gameObject);
        }
        pool.Clear();
    }
}
