using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SpawnData : ScriptableObject
{
    [Tooltip("A list of all possible gameobjects that can be spawned")]
    public GameObject[] possibleSpawnPrefabs = new GameObject[1];

    [Tooltip("Time between each spawn (in seconds). Will take a number between X and Y")]
    public Vector2 spawnInterval = new Vector2(2, 3);

    [Tooltip("How many enemies are spawned per interval?")]
    public Vector2Int spawnsPerTick = new Vector2Int(1, 1);

    [Tooltip("How long (in seconds) this will spawn enemies for")]
    [Min(0.1f)] public float duration = 60;


    //returns an array of prefabs that we should spawn
    //takes an optional parameter of how many enemies are on the screen at the moment
    public virtual GameObject[] GetSpawns(int totalEnemies = 0)
    {
        //determine how many enemies to spawn
        int count = Random.Range(spawnsPerTick.x, spawnsPerTick.y);

        //Generate the result
        GameObject[] result = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            //randomly picks one of the possible spawns and inserts it
            //into the result array
            result[i] = possibleSpawnPrefabs[Random.Range(0, possibleSpawnPrefabs.Length)];
        }

        return result;
    }

    //get a random spawn interval between the min and max values
    public virtual float GetSpawnInterval()
    {
        return Random.Range(spawnInterval.x, spawnInterval.y);
    }
}
