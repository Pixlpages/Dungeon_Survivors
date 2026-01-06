using System.Collections.Generic;
using UnityEngine;

public class DropRateManager : MonoBehaviour
{
    [System.Serializable]
    public class Drops
    {
        public string name;
        public GameObject itemPrefab;
        [Range(0f, 100f)] public float dropRate;
    }

    public List<Drops> drops;
    public bool active = false;

    void OnDestroy()
    {
        if (!active) return; // prevent drops if manually disabled
        if (!gameObject.scene.isLoaded) return; // prevent drops on scene unload

        float randomNumber = Random.Range(0f, 100f);
        List<Drops> possibleDrops = new List<Drops>();

        // Collect possible drops
        foreach (Drops d in drops)
        {
            if (randomNumber <= d.dropRate)
            {
                possibleDrops.Add(d);
            }
        }

        // If no drops, exit early
        if (possibleDrops.Count == 0)
            return;

        // Choose a random drop from possible options
        Drops chosenDrop = possibleDrops[Random.Range(0, possibleDrops.Count)];

        // Use POOL instead of Instantiate
        Pickup pickup = PickupPool.Instance.GetPickup();

        // Make sure it spawns at the enemy position
        pickup.transform.position = transform.position;

        // Reset its animation + timer
        pickup.ResetPickup();
    }
}
