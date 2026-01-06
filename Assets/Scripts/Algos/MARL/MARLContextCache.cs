using UnityEngine;
using System.Collections.Generic;

public class MARLContextCache : MonoBehaviour
{
    public static MARLContextCache Instance { get; private set; }

    public static Vector2 PlayerPosition { get; private set; }
    public static Vector2 PlayerFacing { get; private set; }
    public static Vector2 ClusterCenter { get; private set; }

    private List<MARLAgent> agents = new();
    private float lastUpdateTime;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (Time.time - lastUpdateTime < 0.2f) return; // update every 0.2s
        lastUpdateTime = Time.time;

        agents.Clear();
        agents.AddRange(FindObjectsOfType<MARLAgent>());

        PlayerMovement pm = FindObjectOfType<PlayerMovement>();
        PlayerPosition = pm ? (Vector2)pm.transform.position : Vector2.zero;
        PlayerFacing = pm ? pm.LookDirection.normalized : Vector2.right;

        ClusterCenter = ComputeClusterCenter();
    }

    private Vector2 ComputeClusterCenter()
    {
        int cellSize = 5;
        Dictionary<Vector2Int, List<MARLAgent>> grid = new();

        foreach (var agent in agents)
        {
            if (!agent.IsAlive) continue;
            Vector2 pos = agent.transform.position;
            Vector2Int cell = new Vector2Int(Mathf.FloorToInt(pos.x / cellSize), Mathf.FloorToInt(pos.y / cellSize));
            if (!grid.ContainsKey(cell)) grid[cell] = new List<MARLAgent>();
            grid[cell].Add(agent);
        }

        Vector2Int densestCell = default;
        int maxCount = 0;
        foreach (var kvp in grid)
        {
            if (kvp.Value.Count > maxCount)
            {
                maxCount = kvp.Value.Count;
                densestCell = kvp.Key;
            }
        }

        if (grid.TryGetValue(densestCell, out var agentsInCell) && agentsInCell.Count > 0)
        {
            Vector2 sum = Vector2.zero;
            foreach (var agent in agentsInCell)
                sum += (Vector2)agent.transform.position;

            Vector2 avg = sum / agentsInCell.Count;
            return Vector2.Lerp(avg, PlayerPosition, 0.3f);
        }

        return PlayerPosition;
    }

    public int CountNearbyAgents(Vector2 center, float radius, MARLAgent self)
    {
        int count = 0;
        float sqrRadius = radius * radius;
        foreach (var agent in agents)
        {
            if (agent == null || !agent.IsAlive || agent == self) continue;
            if ((agent.transform.position - (Vector3)center).sqrMagnitude < sqrRadius)
            {
                count++;
                if (count > 2) break;
            }
        }
        return count;
    }

    public Vector2 ComputeSeparationVector(Vector2 center, float radius, MARLAgent self)
    {
        Vector2 separation = Vector2.zero;
        foreach (var agent in agents)
        {
            if (agent == null || !agent.IsAlive || agent == self) continue;
            Vector2 away = center - (Vector2)agent.transform.position;
            float dist = away.magnitude;
            if (dist < radius)
                separation += away.normalized / Mathf.Max(dist, 0.1f);
        }
        return separation.normalized;
    }
}