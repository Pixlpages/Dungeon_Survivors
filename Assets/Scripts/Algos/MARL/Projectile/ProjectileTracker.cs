using UnityEngine;

public class ProjectileTracker : MonoBehaviour
{
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        ProjectileManager.Register(this);
    }

    void OnDisable()
    {
        ProjectileManager.Unregister(this);
    }

    public Vector2 Position => transform.position;
    public Vector2 Velocity => rb != null ? rb.velocity : Vector2.zero;
}