//using UnityEngine;
//using Unity.Mathematics;

//[CreateAssetMenu(fileName = "PathfindingConfig", menuName = "Pathfinding/Config")]
//public class PathfindingConfig : ScriptableObject
//{
//    [Header("Grid Configuration")]
//    [Tooltip("Size of each individual grid (width x height)")]
//    public int2 gridSize = new int2(100, 100);

//    [Tooltip("Number of separate grids to create and test")]
//    [Range(1, 10)]
//    public int numberOfGrids = 1;

//    [Tooltip("Use pre-generated grids for better performance")]
//    public bool usePreGeneratedGrids = true;

//    [Header("Obstacle Configuration")]
//    [Tooltip("Include obstacles in the grids")]
//    public bool includeObstacles = false;

//    [Tooltip("Density of obstacles (0.0 = no obstacles, 1.0 = maximum obstacles)")]
//    [Range(0f, 1f)]
//    public float obstacleDensity = 0.1f;

//    [Tooltip("Minimum distance between obstacles")]
//    [Range(1, 10)]
//    public int minObstacleDistance = 3;

//    [Tooltip("Use random obstacle placement")]
//    public bool randomObstacles = true;

//    [Tooltip("Fixed obstacle positions (used when randomObstacles is false)")]
//    public int2[] fixedObstacles = new int2[]
//    {
//        new int2(50, 50),
//        new int2(25, 75),
//        new int2(75, 25)
//    };

//    [Header("Pathfinding Test Configuration")]
//    [Tooltip("Number of simultaneous pathfinding operations per grid")]
//    [Range(1, 100)]
//    public int simultaneousPathfinds = 5;

//    [Tooltip("Number of parallel actors/threads for pathfinding")]
//    [Range(1, 16)]
//    public int parallelActors = 4;

//    [Tooltip("Interval between continuous tests (in seconds)")]
//    [Range(0.1f, 10f)]
//    public float testInterval = 1f;

//    [Tooltip("Run tests continuously")]
//    public bool runContinuousTests = true;

//    [Header("Path Points Configuration")]
//    [Tooltip("Starting positions for pathfinding (one per grid if multiple grids)")]
//    public int2[] startPositions = new int2[] { new int2(0, 0) };

//    [Tooltip("End positions for pathfinding (one per grid if multiple grids)")]
//    public int2[] endPositions = new int2[] { new int2(99, 99) };

//    [Tooltip("Use random start/end positions for each test")]
//    public bool useRandomStartEnd = false;

//    [Tooltip("Margin from grid edges when using random positions")]
//    [Range(0, 20)]
//    public int randomPositionMargin = 5;

//    [Header("Performance Settings")]
//    [Tooltip("Maximum execution time per frame (ms) before yielding")]
//    [Range(1f, 33f)]
//    public float maxFrameTime = 16f;

//    [Tooltip("Enable detailed performance logging")]
//    public bool enablePerformanceLogging = true;

//    [Tooltip("Log results to file")]
//    public bool logToFile = false;

//    [Header("Memory Settings")]
//    [Tooltip("Dispose grids after each test (saves memory but reduces performance)")]
//    public bool disposeAfterTest = false;

//    [Tooltip("Force garbage collection after tests")]
//    public bool forceGCAfterTest = false;

//    // Validation methods
//    private void OnValidate()
//    {
//        // Ensure we have enough start/end positions for the number of grids
//        if (startPositions.Length < numberOfGrids)
//        {
//            System.Array.Resize(ref startPositions, numberOfGrids);
//            for (int i = startPositions.Length - numberOfGrids; i < numberOfGrids; i++)
//            {
//                startPositions[i] = new int2(0, 0);
//            }
//        }

//        if (endPositions.Length < numberOfGrids)
//        {
//            System.Array.Resize(ref endPositions, numberOfGrids);
//            for (int i = endPositions.Length - numberOfGrids; i < numberOfGrids; i++)
//            {
//                endPositions[i] = new int2(gridSize.x - 1, gridSize.y - 1);
//            }
//        }

//        // Validate positions are within grid bounds
//        for (int i = 0; i < startPositions.Length && i < numberOfGrids; i++)
//        {
//            startPositions[i] = ClampToGrid(startPositions[i]);
//        }

//        for (int i = 0; i < endPositions.Length && i < numberOfGrids; i++)
//        {
//            endPositions[i] = ClampToGrid(endPositions[i]);
//        }
//    }

//    private int2 ClampToGrid(int2 position)
//    {
//        return new int2(
//            Mathf.Clamp(position.x, 0, gridSize.x - 1),
//            Mathf.Clamp(position.y, 0, gridSize.y - 1)
//        );
//    }

//    // Helper methods for runtime use
//    public int2 GetStartPosition(int gridIndex)
//    {
//        if (useRandomStartEnd)
//        {
//            return GetRandomPosition();
//        }

//        int index = Mathf.Min(gridIndex, startPositions.Length - 1);
//        return startPositions[index];
//    }

//    public int2 GetEndPosition(int gridIndex)
//    {
//        if (useRandomStartEnd)
//        {
//            return GetRandomPosition();
//        }

//        int index = Mathf.Min(gridIndex, endPositions.Length - 1);
//        return endPositions[index];
//    }

//    public int2 GetRandomPosition()
//    {
//        return new int2(
//            UnityEngine.Random.Range(randomPositionMargin, gridSize.x - randomPositionMargin),
//            UnityEngine.Random.Range(randomPositionMargin, gridSize.y - randomPositionMargin)
//        );
//    }

//    public int GetEstimatedObstacleCount()
//    {
//        if (!includeObstacles) return 0;

//        int totalCells = gridSize.x * gridSize.y;
//        return Mathf.RoundToInt(totalCells * obstacleDensity);
//    }

//    public int GetTotalPathfindingOperations()
//    {
//        return numberOfGrids * simultaneousPathfinds;
//    }

//    // Create a copy of this config for runtime modifications
//    public PathfindingConfig CreateRuntimeCopy()
//    {
//        PathfindingConfig copy = CreateInstance<PathfindingConfig>();

//        copy.gridSize = this.gridSize;
//        copy.numberOfGrids = this.numberOfGrids;
//        copy.usePreGeneratedGrids = this.usePreGeneratedGrids;

//        copy.includeObstacles = this.includeObstacles;
//        copy.obstacleDensity = this.obstacleDensity;
//        copy.minObstacleDistance = this.minObstacleDistance;
//        copy.randomObstacles = this.randomObstacles;
//        copy.fixedObstacles = (int2[])this.fixedObstacles.Clone();

//        copy.simultaneousPathfinds = this.simultaneousPathfinds;
//        copy.parallelActors = this.parallelActors;
//        copy.testInterval = this.testInterval;
//        copy.runContinuousTests = this.runContinuousTests;

//        copy.startPositions = (int2[])this.startPositions.Clone();
//        copy.endPositions = (int2[])this.endPositions.Clone();
//        copy.useRandomStartEnd = this.useRandomStartEnd;
//        copy.randomPositionMargin = this.randomPositionMargin;

//        copy.maxFrameTime = this.maxFrameTime;
//        copy.enablePerformanceLogging = this.enablePerformanceLogging;
//        copy.logToFile = this.logToFile;

//        copy.disposeAfterTest = this.disposeAfterTest;
//        copy.forceGCAfterTest = this.forceGCAfterTest;

//        return copy;
//    }
//}