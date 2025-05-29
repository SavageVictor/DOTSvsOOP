using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class IndividualPathStorage : MonoBehaviour
{
    [Header("Storage Settings")]
    [SerializeField] private bool autoSaveEnabled = true;
    [SerializeField] private float autoSaveInterval = 30f;
    [SerializeField] private int maxStoredPaths = 100;
    [SerializeField] private string saveDirectory = "IndividualPathMaps";

    [Header("Map Symbols")]
    [SerializeField] private char emptySymbol = '·';     // Middle dot - cleaner than period
    [SerializeField] private char startSymbol = 'S';     // Clear start marker
    [SerializeField] private char endSymbol = 'E';       // Clear end marker  
    [SerializeField] private char obstacleSymbol = '█';   // Solid block
    [SerializeField] private char pathSymbol = '●';       // Filled circle for path nodes

    [Header("Directional Path Symbols")]
    [SerializeField] private bool useDirectionalSymbols = true;
    [SerializeField] private char northSymbol = '↑';     // Up
    [SerializeField] private char southSymbol = '↓';     // Down
    [SerializeField] private char eastSymbol = '→';      // Right
    [SerializeField] private char westSymbol = '←';      // Left
    [SerializeField] private char northEastSymbol = '↗'; // Up-Right
    [SerializeField] private char northWestSymbol = '↖'; // Up-Left
    [SerializeField] private char southEastSymbol = '↘'; // Down-Right
    [SerializeField] private char southWestSymbol = '↙'; // Down-Left

    [Header("Export Options")]
    [SerializeField] private bool saveAsIndividualFiles = true;
    [SerializeField] private bool saveAsCollection = true;
    [SerializeField] private bool exportTextMaps = true;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogging = true;
    [SerializeField] private bool logEntityQueries = true;
    [SerializeField] private float debugInterval = 2f;
    [SerializeField] private KeyCode saveKey = KeyCode.F5;
    [SerializeField] private KeyCode exportKey = KeyCode.F6;
    [SerializeField] private KeyCode clearKey = KeyCode.F7;

    private PathMapCollection currentCollection;
    private Dictionary<Entity, bool> processedEntities;
    private EntityQuery completedPathsQuery;
    private EntityQuery allPathfindingQuery; // For debugging
    private EntityManager entityManager;
    private int nextPathId = 0;
    private float lastAutoSaveTime;
    private float lastDebugTime;

    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        InitializeCollection();
        CreateQueries();

        // Create save directory
        string fullPath = Path.Combine(Application.persistentDataPath, saveDirectory);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }
    }

    void Update()
    {
        ProcessCompletedPaths();

        // Debug logging
        if (enableDebugLogging && Time.time - lastDebugTime >= debugInterval)
        {
            DebugEntityCounts();
            lastDebugTime = Time.time;
        }

        // Auto save
        if (autoSaveEnabled && Time.time - lastAutoSaveTime >= autoSaveInterval)
        {
            SaveCurrentCollection();
            lastAutoSaveTime = Time.time;
        }

        // Manual controls
        if (Input.GetKeyDown(saveKey))
            SaveCurrentCollection();

        if (Input.GetKeyDown(exportKey))
            ExportAllMapsAsText();

        if (Input.GetKeyDown(clearKey))
            ClearCollection();
    }

    private void InitializeCollection()
    {
        currentCollection = new PathMapCollection
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            totalMaps = 0,
            gridSize = new int2(20, 20), // Default, will be updated
            pathMaps = new List<IndividualPathMap>(),
            symbolLegend = new Dictionary<char, string>(),
            statistics = new CollectionStatistics()
        };

        processedEntities = new Dictionary<Entity, bool>();

        InitializeSymbolLegend();
    }

    private void InitializeSymbolLegend()
    {
        currentCollection.symbolLegend[emptySymbol] = "Empty space";
        currentCollection.symbolLegend[startSymbol] = "Start position";
        currentCollection.symbolLegend[endSymbol] = "End/Target position";
        currentCollection.symbolLegend[obstacleSymbol] = "Obstacle";

        if (useDirectionalSymbols)
        {
            currentCollection.symbolLegend[northSymbol] = "Path moving North ↑";
            currentCollection.symbolLegend[southSymbol] = "Path moving South ↓";
            currentCollection.symbolLegend[eastSymbol] = "Path moving East →";
            currentCollection.symbolLegend[westSymbol] = "Path moving West ←";
            currentCollection.symbolLegend[northEastSymbol] = "Path moving North-East ↗";
            currentCollection.symbolLegend[northWestSymbol] = "Path moving North-West ↖";
            currentCollection.symbolLegend[southEastSymbol] = "Path moving South-East ↘";
            currentCollection.symbolLegend[southWestSymbol] = "Path moving South-West ↙";
        }
        else
        {
            currentCollection.symbolLegend[pathSymbol] = "Path route";
        }
    }

    private void CreateQueries()
    {
        // Query for completed pathfinding (has PathfindingComplete component enabled)
        completedPathsQuery = entityManager.CreateEntityQuery(
            typeof(PathfindingRequest),
            typeof(PathBuffer),
            typeof(PathfindingComplete)
        );

        // Query for all pathfinding entities (for debugging)
        allPathfindingQuery = entityManager.CreateEntityQuery(
            typeof(PathfindingRequest)
        );

        if (enableDebugLogging)
        {
            Debug.Log("PathStorage: Entity queries created");
        }
    }

    private void ProcessCompletedPaths()
    {
        var entities = completedPathsQuery.ToEntityArray(Allocator.TempJob);

        if (enableDebugLogging && entities.Length > 0)
        {
            Debug.Log($"PathStorage: Found {entities.Length} completed pathfinding entities");
        }

        foreach (var entity in entities)
        {
            // Check if PathfindingComplete component is enabled
            bool isCompleteEnabled = entityManager.IsComponentEnabled<PathfindingComplete>(entity);

            if (enableDebugLogging)
            {
                Debug.Log($"PathStorage: Entity {entity.Index} - Complete component enabled: {isCompleteEnabled}");
            }

            if (!processedEntities.ContainsKey(entity) &&
                currentCollection.totalMaps < maxStoredPaths &&
                isCompleteEnabled)
            {
                if (enableDebugLogging)
                {
                    Debug.Log($"PathStorage: Processing new completed entity {entity.Index}");
                }

                CreateIndividualPathMap(entity);
                processedEntities[entity] = true;
            }
        }

        entities.Dispose();
    }

    private void DebugEntityCounts()
    {
        int totalPathfindingEntities = allPathfindingQuery.CalculateEntityCount();
        int completedPathfindingEntities = completedPathsQuery.CalculateEntityCount();

        Debug.Log($"PathStorage Debug - Total pathfinding entities: {totalPathfindingEntities}, " +
                  $"Completed: {completedPathfindingEntities}, " +
                  $"Processed: {processedEntities.Count}, " +
                  $"Stored maps: {currentCollection.totalMaps}");

        if (logEntityQueries && completedPathfindingEntities > 0)
        {
            var entities = completedPathsQuery.ToEntityArray(Allocator.TempJob);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                bool hasRequest = entityManager.HasComponent<PathfindingRequest>(entity);
                bool hasBuffer = entityManager.HasComponent<PathBuffer>(entity);
                bool hasComplete = entityManager.HasComponent<PathfindingComplete>(entity);
                bool isCompleteEnabled = entityManager.IsComponentEnabled<PathfindingComplete>(entity);
                bool wasProcessed = processedEntities.ContainsKey(entity);

                Debug.Log($"Entity {entity.Index}: Request={hasRequest}, Buffer={hasBuffer}, " +
                          $"Complete={hasComplete}, CompleteEnabled={isCompleteEnabled}, Processed={wasProcessed}");

                if (hasComplete && isCompleteEnabled)
                {
                    var completion = entityManager.GetComponentData<PathfindingComplete>(entity);
                    var request = entityManager.GetComponentData<PathfindingRequest>(entity);
                    var buffer = entityManager.GetBuffer<PathBuffer>(entity);

                    Debug.Log($"  PathFound: {completion.pathFound}, BufferLength: {buffer.Length}, " +
                              $"Start: {request.startPosition}, End: {request.targetPosition}");
                }
            }

            entities.Dispose();
        }
    }

    private void CreateIndividualPathMap(Entity entity)
    {
        var request = entityManager.GetComponentData<PathfindingRequest>(entity);
        var pathBuffer = entityManager.GetBuffer<PathBuffer>(entity);
        var completion = entityManager.GetComponentData<PathfindingComplete>(entity);

        // Update grid size if needed
        if (request.gridSize.x > currentCollection.gridSize.x ||
            request.gridSize.y > currentCollection.gridSize.y)
        {
            currentCollection.gridSize = request.gridSize;
        }

        var pathMap = new IndividualPathMap
        {
            pathId = nextPathId,
            startPosition = request.startPosition,
            endPosition = request.targetPosition,
            pathFound = completion.pathFound,
            pathLength = pathBuffer.Length,
            calculationTimeMs = 0f, // Would need timing system
            mapGrid = new List<string>(),
            pathNodes = new List<int2>(),
            pathResult = completion.pathFound ? "SUCCESS" : "FAILED"
        };

        // Store path nodes
        for (int i = 0; i < pathBuffer.Length; i++)
        {
            pathMap.pathNodes.Add(pathBuffer[i].position);
        }

        // Generate the individual map for this path
        GenerateIndividualMap(pathMap, request.gridSize);

        currentCollection.pathMaps.Add(pathMap);
        currentCollection.totalMaps++;
        nextPathId++;

        UpdateStatistics();

        Debug.Log($"Created individual path map {pathMap.pathId}: {request.startPosition} → {request.targetPosition} " +
                  $"({pathBuffer.Length} nodes, {pathMap.pathResult})");

        // Print the map to console for immediate feedback
        PrintIndividualMap(pathMap);
    }

    private void GenerateIndividualMap(IndividualPathMap pathMap, int2 gridSize)
    {
        // Get obstacle data from grid
        var obstaclePositions = GetObstaclePositions(gridSize);

        // Create path lookup with directional information
        var pathDirections = new Dictionary<int2, char>();

        if (useDirectionalSymbols && pathMap.pathNodes.Count > 1)
        {
            CalculatePathDirections(pathMap.pathNodes, pathDirections);
        }
        else
        {
            // Use simple path symbols
            foreach (var node in pathMap.pathNodes)
            {
                pathDirections[node] = pathSymbol;
            }
        }

        // Generate map from top to bottom (y descending)
        for (int y = gridSize.y - 1; y >= 0; y--)
        {
            string row = "";

            for (int x = 0; x < gridSize.x; x++)
            {
                int2 pos = new int2(x, y);
                char symbol = DetermineSymbolForPosition(pos, pathMap, pathDirections, obstaclePositions);
                row += symbol;
            }

            pathMap.mapGrid.Add(row);
        }
    }

    private void CalculatePathDirections(List<int2> pathNodes, Dictionary<int2, char> pathDirections)
    {
        for (int i = 0; i < pathNodes.Count; i++)
        {
            int2 currentNode = pathNodes[i];
            char directionSymbol = pathSymbol; // Default fallback

            if (i < pathNodes.Count - 1)
            {
                // Calculate direction from current to next node
                int2 nextNode = pathNodes[i + 1];
                int2 direction = nextNode - currentNode;
                directionSymbol = GetDirectionSymbol(direction);
            }
            else if (i > 0)
            {
                // For the last node, use the direction from previous node
                int2 prevNode = pathNodes[i - 1];
                int2 direction = currentNode - prevNode;
                directionSymbol = GetDirectionSymbol(direction);
            }

            pathDirections[currentNode] = directionSymbol;
        }
    }

    private char GetDirectionSymbol(int2 direction)
    {
        // Normalize direction and return appropriate symbol
        if (direction.x == 0 && direction.y == 1) return northSymbol;      // North ↑
        if (direction.x == 0 && direction.y == -1) return southSymbol;     // South ↓
        if (direction.x == 1 && direction.y == 0) return eastSymbol;       // East →
        if (direction.x == -1 && direction.y == 0) return westSymbol;      // West ←
        if (direction.x == 1 && direction.y == 1) return northEastSymbol;  // North-East ↗
        if (direction.x == -1 && direction.y == 1) return northWestSymbol; // North-West ↖
        if (direction.x == 1 && direction.y == -1) return southEastSymbol; // South-East ↘
        if (direction.x == -1 && direction.y == -1) return southWestSymbol;// South-West ↙

        return pathSymbol; // Fallback for any unexpected directions
    }

    private char DetermineSymbolForPosition(int2 pos, IndividualPathMap pathMap,
        Dictionary<int2, char> pathDirections, HashSet<int2> obstaclePositions)
    {
        // Priority: Start > End > Obstacle > Path > Empty

        if (pos.Equals(pathMap.startPosition))
            return startSymbol;

        if (pos.Equals(pathMap.endPosition))
            return endSymbol;

        if (obstaclePositions.Contains(pos))
            return obstacleSymbol;

        if (pathDirections.ContainsKey(pos))
            return pathDirections[pos];

        return emptySymbol;
    }

    private HashSet<int2> GetObstaclePositions(int2 gridSize)
    {
        var obstacles = new HashSet<int2>();

        // Get grid data to find obstacles
        var gridQuery = entityManager.CreateEntityQuery(typeof(GridData));
        if (gridQuery.CalculateEntityCount() > 0)
        {
            var gridData = entityManager.GetComponentData<GridData>(gridQuery.GetSingletonEntity());
            ref var gridNodes = ref gridData.gridBlob.Value.nodes;

            for (int i = 0; i < gridNodes.Length; i++)
            {
                if (!gridNodes[i].isWalkable)
                {
                    obstacles.Add(gridNodes[i].position);
                }
            }
        }

        gridQuery.Dispose();
        return obstacles;
    }

    private void UpdateStatistics()
    {
        var stats = currentCollection.statistics;
        stats.totalPaths = currentCollection.totalMaps;
        stats.successfulPaths = 0;
        stats.failedPaths = 0;

        int shortestPath = int.MaxValue;
        int longestPath = 0;
        int totalLength = 0;
        var startCounts = new Dictionary<int2, int>();
        var endCounts = new Dictionary<int2, int>();

        foreach (var pathMap in currentCollection.pathMaps)
        {
            if (pathMap.pathFound)
            {
                stats.successfulPaths++;
                totalLength += pathMap.pathLength;
                shortestPath = Mathf.Min(shortestPath, pathMap.pathLength);
                longestPath = Mathf.Max(longestPath, pathMap.pathLength);
            }
            else
            {
                stats.failedPaths++;
            }

            // Count start/end positions
            if (!startCounts.ContainsKey(pathMap.startPosition))
                startCounts[pathMap.startPosition] = 0;
            startCounts[pathMap.startPosition]++;

            if (!endCounts.ContainsKey(pathMap.endPosition))
                endCounts[pathMap.endPosition] = 0;
            endCounts[pathMap.endPosition]++;
        }

        stats.successRate = stats.totalPaths > 0 ? (float)stats.successfulPaths / stats.totalPaths : 0f;
        stats.averagePathLength = stats.successfulPaths > 0 ? (float)totalLength / stats.successfulPaths : 0f;
        stats.shortestPath = shortestPath == int.MaxValue ? 0 : shortestPath;
        stats.longestPath = longestPath;

        // Find most used positions
        stats.mostUsedStart = FindMostUsedPosition(startCounts);
        stats.mostUsedEnd = FindMostUsedPosition(endCounts);
    }

    private int2 FindMostUsedPosition(Dictionary<int2, int> counts)
    {
        int maxCount = 0;
        int2 mostUsed = new int2(0, 0);

        foreach (var kvp in counts)
        {
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                mostUsed = kvp.Key;
            }
        }

        return mostUsed;
    }

    private void PrintIndividualMap(IndividualPathMap pathMap)
    {
        Debug.Log($"=== PATH MAP {pathMap.pathId} ({pathMap.pathResult}) ===");
        Debug.Log($"Start: {pathMap.startPosition} → End: {pathMap.endPosition} | Length: {pathMap.pathLength}");

        foreach (var row in pathMap.mapGrid)
        {
            Debug.Log(row);
        }

        Debug.Log($"Legend: {emptySymbol}=Empty, S=Start, E=End, {obstacleSymbol}=Obstacle" +
                  (useDirectionalSymbols ? ", ↑↓←→↗↖↘↙=Path directions" : $", {pathSymbol}=Path"));
        Debug.Log("");
    }

    public void SaveCurrentCollection()
    {
        if (saveAsCollection)
        {
            SaveAsJsonCollection();
        }

        if (saveAsIndividualFiles)
        {
            SaveAsIndividualJsonFiles();
        }

        if (exportTextMaps)
        {
            ExportAllMapsAsText();
        }
    }

    private void SaveAsJsonCollection()
    {
        string fileName = $"PathMapCollection_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        string fullPath = Path.Combine(Application.persistentDataPath, saveDirectory, fileName);

        try
        {
            string jsonData = JsonUtility.ToJson(currentCollection, true);
            File.WriteAllText(fullPath, jsonData);

            Debug.Log($"Path map collection saved: {fullPath}");
            Debug.Log($"Saved {currentCollection.totalMaps} individual path maps");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save collection: {e.Message}");
        }
    }

    private void SaveAsIndividualJsonFiles()
    {
        string folderPath = Path.Combine(Application.persistentDataPath, saveDirectory,
            $"IndividualMaps_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(folderPath);

        foreach (var pathMap in currentCollection.pathMaps)
        {
            string fileName = $"PathMap_{pathMap.pathId:D3}_{pathMap.pathResult}.json";
            string fullPath = Path.Combine(folderPath, fileName);

            try
            {
                string jsonData = JsonUtility.ToJson(pathMap, true);
                File.WriteAllText(fullPath, jsonData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save individual map {pathMap.pathId}: {e.Message}");
            }
        }

        Debug.Log($"Saved {currentCollection.pathMaps.Count} individual JSON files to: {folderPath}");
    }

    public void ExportAllMapsAsText()
    {
        string fileName = $"AllPathMaps_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string fullPath = Path.Combine(Application.persistentDataPath, saveDirectory, fileName);

        try
        {
            var content = new System.Text.StringBuilder();

            // Header
            content.AppendLine($"INDIVIDUAL PATH MAPS COLLECTION");
            content.AppendLine($"Generated: {currentCollection.timestamp}");
            content.AppendLine($"Grid Size: {currentCollection.gridSize.x}x{currentCollection.gridSize.y}");
            content.AppendLine($"Total Maps: {currentCollection.totalMaps}");
            content.AppendLine($"Success Rate: {currentCollection.statistics.successRate:P1}");
            content.AppendLine();

            // Legend
            content.AppendLine("SYMBOL LEGEND:");
            foreach (var kvp in currentCollection.symbolLegend)
            {
                content.AppendLine($"  {kvp.Key} = {kvp.Value}");
            }
            content.AppendLine();
            content.AppendLine(new string('=', 60));
            content.AppendLine();

            // Individual maps
            foreach (var pathMap in currentCollection.pathMaps)
            {
                content.AppendLine($"PATH MAP {pathMap.pathId} - {pathMap.pathResult}");
                content.AppendLine($"Start: ({pathMap.startPosition.x},{pathMap.startPosition.y}) → End: ({pathMap.endPosition.x},{pathMap.endPosition.y})");
                content.AppendLine($"Path Length: {pathMap.pathLength} nodes");
                content.AppendLine();

                foreach (var row in pathMap.mapGrid)
                {
                    content.AppendLine(row);
                }

                content.AppendLine();
                content.AppendLine(new string('-', 40));
                content.AppendLine();
            }

            File.WriteAllText(fullPath, content.ToString());
            Debug.Log($"All path maps exported to: {fullPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to export text maps: {e.Message}");
        }
    }

    public void ClearCollection()
    {
        InitializeCollection();
        processedEntities.Clear();
        nextPathId = 0;
        Debug.Log("Path map collection cleared");
    }

    // Public API
    public PathMapCollection GetCurrentCollection() => currentCollection;
    public int GetMapCount() => currentCollection.totalMaps;
    public float GetSuccessRate() => currentCollection.statistics.successRate;
    public IndividualPathMap GetPathMap(int pathId) =>
        currentCollection.pathMaps.Find(m => m.pathId == pathId);

    void OnDestroy()
    {
        if (autoSaveEnabled && currentCollection.totalMaps > 0)
        {
            SaveCurrentCollection();
        }
    }
}

[System.Serializable]
public class PathMapCollection
{
    [Header("Collection Info")]
    public string timestamp;
    public int totalMaps;
    public int2 gridSize;

    [Header("Individual Path Maps")]
    public List<IndividualPathMap> pathMaps;

    [Header("Symbol Legend")]
    public Dictionary<char, string> symbolLegend;

    [Header("Summary Statistics")]
    public CollectionStatistics statistics;
}

[System.Serializable]
public class IndividualPathMap
{
    [Header("Path Info")]
    public int pathId;
    public int2 startPosition;
    public int2 endPosition;
    public bool pathFound;
    public int pathLength;
    public float calculationTimeMs;

    [Header("Map Visualization")]
    public List<string> mapGrid; // Each string is a row, showing this specific path

    [Header("Path Data")]
    public List<int2> pathNodes;
    public string pathResult; // "SUCCESS" or "FAILED"
}

[System.Serializable]
public class CollectionStatistics
{
    public int totalPaths;
    public int successfulPaths;
    public int failedPaths;
    public float successRate;
    public float averagePathLength;
    public int shortestPath;
    public int longestPath;
    public int2 mostUsedStart;
    public int2 mostUsedEnd;
}