using UnityEngine;

//this is a class that can be subclassed by any other class to make the sprites of the class
//automatically sort themselves by the y-axis
[RequireComponent(typeof(SpriteRenderer))]
public abstract class Sortable : MonoBehaviour
{
    public SpriteRenderer sorted;
    public bool sortingActive = true; // allows us to deactivate this on certain objects
    public float minimumDistance = 0.2f; // Minimum distance before the sorting value updates
    public int lastSortOrder = 0;

    protected virtual void Start()
    {
        sorted = GetComponent<SpriteRenderer>();
    }

    protected virtual void LateUpdate()
    {
        int newSortOrder = (int)(-transform.position.y / minimumDistance);
        if (lastSortOrder != newSortOrder)
            sorted.sortingOrder = newSortOrder;
    }
}
