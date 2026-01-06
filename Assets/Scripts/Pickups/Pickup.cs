using UnityEngine;

public class Pickup : Sortable
{
    [Header("Timers")]
    public float despawnTimer = 10f;
    public float collectLifespan = 0.5f;

    protected PlayerStats target;
    protected float speed;
    Vector2 initialPosition;
    float initialOffset;
    float currentTimer;
    

    [System.Serializable]
    public struct BobbingAnimation
    {
        public float frequency;
        public Vector2 direction;
    }

    public BobbingAnimation bobbingAnimation = new BobbingAnimation
    {
        frequency = 2f,
        direction = new Vector2(0, 0.3f)
    };

    [Header("Bonuses")]
    public int experience;
    public int health;

    protected override void Start()
    {
        base.Start();
        ResetPickup();
    }

    void OnEnable()
    {
        ResetPickup();
    }

    public void ResetPickup()
    {
        target = null;
        currentTimer = despawnTimer;
        initialPosition = transform.position;
        initialOffset = Random.Range(0, bobbingAnimation.frequency);
    }

    protected virtual void Update()
    {
        if (target)
        {
            // Move toward player
            Vector2 distance = target.transform.position - transform.position;

            if (distance.sqrMagnitude > speed * speed * Time.deltaTime)
            {
                transform.position += (Vector3)distance.normalized * speed * Time.deltaTime;
            }
            else
            {
                ApplyBonuses();
                ReturnToPool();
            }
        }
        else
        {
            // Bobbing
            transform.position = initialPosition +
                bobbingAnimation.direction * Mathf.Sin((Time.time + initialOffset) * bobbingAnimation.frequency);

            // Despawn countdown
            currentTimer -= Time.deltaTime;
            if (currentTimer <= 0f)
                ReturnToPool();
        }
    }

    public virtual bool Collect(PlayerStats target, float speed, float lifespan = 0f)
    {
        if (this.target != null)
            return false;

        this.target = target;
        this.speed = speed;

        if (lifespan > 0)
            collectLifespan = lifespan;

        // Return to pool after time
        Invoke(nameof(ReturnAfterCollected), collectLifespan);

        return true;
    }

    void ReturnAfterCollected()
    {
        ApplyBonuses();
        ReturnToPool();
    }

    void ApplyBonuses()
    {
        if (target)
        {
            if (experience != 0) target.IncreaseExperience(experience);
            if (health != 0) target.RestoreHealth(health);
        }
    }

    void ReturnToPool()
    {
        CancelInvoke();
        PickupPool.Instance.ReturnPickup(this);
    }
}
