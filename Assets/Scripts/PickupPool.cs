using System.Collections.Generic;
using UnityEngine;

public class PickupPool : MonoBehaviour
{
    public static PickupPool Instance;

    [Header("Pool Settings")]
    public Pickup pickupPrefab;
    public int initialPoolSize = 50;

    private Queue<Pickup> pool = new Queue<Pickup>();

    void Awake()
    {
        Instance = this;
        InitializePool();
    }

    void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
            CreateNewPickup();
    }

    Pickup CreateNewPickup()
    {
        Pickup newPickup = Instantiate(pickupPrefab, transform);
        newPickup.gameObject.SetActive(false);
        pool.Enqueue(newPickup);
        return newPickup;
    }

    public Pickup GetPickup()
    {
        if (pool.Count == 0)
            CreateNewPickup();

        Pickup pickup = pool.Dequeue();
        pickup.gameObject.SetActive(true);
        return pickup;
    }

    public void ReturnPickup(Pickup pickup)
    {
        pickup.gameObject.SetActive(false);
        pool.Enqueue(pickup);
    }
}
