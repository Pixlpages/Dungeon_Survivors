using System.Collections.Generic;

public static class ProjectileManager
{
    private static readonly List<ProjectileTracker> active = new List<ProjectileTracker>();
    public static IReadOnlyList<ProjectileTracker> Active => active;

    public static void Register(ProjectileTracker proj)
    {
        if (!active.Contains(proj))
            active.Add(proj);
    }

    public static void Unregister(ProjectileTracker proj)
    {
        active.Remove(proj);
    }
}