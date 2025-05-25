
using System.Collections.Generic;
using System;
using DOTS;
using PerformanceComparison;
using UnityEngine;
using UnityEditor;

public interface IImplementationProvider
{
    IPathfinder GetPathfinder();
    //IPhysicsSystem GetPhysicsSystem();
    //IPerformanceProfiler GetProfiler();
    string ProviderName { get; }
}

public class MonoBehaviourImplementationProvider : IImplementationProvider
{
    public string ProviderName => "MonoBehaviour";

    public IPathfinder GetPathfinder() => new MonoBehaviourPathfinder();
    //public IPhysicsSystem GetPhysicsSystem() => MonoBehaviourPhysicsSystem();
    //public IPerformanceProfiler GetProfiler() => new UnityProfilerWrapper();
}

public class MonoBehaviourPathfinder : IPathfinder
{
    public string ImplementationName => "MonoBehaviour A*";

    public List<Vector2Int> FindPath(IGrid grid, Vector2Int start, Vector2Int goal)
    {
        // MonoBehaviour-specific A* implementation
        throw new NotImplementedException();
    }

    public void Initialize() { }
    public void Cleanup() { }
}

public class DOTSImplementationProvider : IImplementationProvider
{
    public string ProviderName => "DOTS";

    public IPathfinder GetPathfinder() => new DOTSPathfinder();
    //public IPhysicsSystem GetPhysicsSystem() => new DOTSPhysicsSystem();
    //public IPerformanceProfiler GetProfiler() => new DOTSProfilerWrapper();
}
