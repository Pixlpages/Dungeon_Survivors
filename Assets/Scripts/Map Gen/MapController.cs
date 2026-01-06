using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapController : MonoBehaviour
{
    public List<GameObject> terrainChunks;
    PlayerStats player;
    public float checkerRadius;
    public LayerMask terrainMask;
    public GameObject currentChunk;
    Vector3 playerLastPosition;

    [Header("Optimization")]
    public List<GameObject> spawnedChunks;
    GameObject latestChunk;
    public float maxOpDist; //must be greater than the length and width of the tilemap
    float opDist;
    float optimizerCooldown;
    public float optimizerCooldownDuration;


    public void Awake()
    {
        player = FindObjectOfType<PlayerStats>();
    }

    void Start()
    {
        playerLastPosition = player.transform.position;
    }

    void Update()
    {
        ChunkChecker();
        ChunkOptimizer();
    }

    void SpawnChunk(Vector3 spawnPosition)
    {
        int rand = Random.Range(0, terrainChunks.Count);
        latestChunk = Instantiate(terrainChunks[rand], spawnPosition, Quaternion.identity);
        spawnedChunks.Add(latestChunk);
    }

    void ChunkChecker()
    {
        if (!currentChunk)
        {
            return;
        }

        Vector3 moveDir = player.transform.position - playerLastPosition;
        playerLastPosition = player.transform.position;

        string directionName = GetDirectionName(moveDir);

        CheckAndSpawnChunk(directionName);

        if (directionName.Contains("Up"))
        {
            CheckAndSpawnChunk("Up");
        }
        if (directionName.Contains("Down"))
        {
            CheckAndSpawnChunk("Down");
        }
        if (directionName.Contains("Left"))
        {
            CheckAndSpawnChunk("Left");
        }
        if (directionName.Contains("Right"))
        {
            CheckAndSpawnChunk("Right");
        }
    }

    void CheckAndSpawnChunk(string direction)
    {
        Transform spawnPoint = currentChunk.transform.Find(direction);
        if (spawnPoint == null) return;

        Collider2D hit = Physics2D.OverlapCircle(spawnPoint.position, checkerRadius, terrainMask);

        if (hit == null)
        {
            // only spawn if we haven’t already
            bool alreadySpawned = spawnedChunks.Exists(c =>
                Vector3.Distance(c.transform.position, spawnPoint.position) < 0.1f);

            if (!alreadySpawned)
            {
                SpawnChunk(spawnPoint.position);
            }
        }
    }


    string GetDirectionName(Vector3 direction)
    {
        direction = direction.normalized;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            //moving horizontally more than vertically
            if (direction.y > 0.5f)
            {
                //also moving upwards
                return direction.x > 0 ? "Right Up" : "Left Up";
            }

            else if (direction.y < -0.5f)
            {
                return direction.x > 0 ? "Right Down" : "Left Down";
            }
            else
            {
                //moving straight horizontally
                return direction.x > 0 ? "Right" : "Left";
            }
        }
        else
        {
            //moving vertically more than horizontally
            if (direction.x > 0.5f)
            {
                //also moving right
                return direction.y > 0 ? "Right Up" : "Right Down";
            }
            else if (direction.x < -0.5f)
            {
                //also moving left
                return direction.x > 0 ? "Left Up" : "Left Down";
            }
            else
            {
                //moving straigth vertically
                return direction.x > 0 ? "Up" : "Down";
            }
        }
    }

    void ChunkOptimizer()
    {
        optimizerCooldown -= Time.deltaTime;

        if (optimizerCooldown <= 0f)
            optimizerCooldown = optimizerCooldownDuration;

        foreach (GameObject chunk in spawnedChunks)
            {
                opDist = Vector3.Distance(player.transform.position, chunk.transform.position);
                if (opDist > maxOpDist)
                {
                    chunk.SetActive(false);
                }
                else
                {
                    chunk.SetActive(true);
                }
            }
    }
}
