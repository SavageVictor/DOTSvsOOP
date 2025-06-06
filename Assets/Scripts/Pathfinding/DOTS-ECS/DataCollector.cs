using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using System.IO;
using System;

namespace DOTS_ECS
{
    public enum ExportFormat
    {
        Minimal,        // Just success/fail, timing
        WithCoordinates, // Include path coordinates as string
        WithMaps        // Include ASCII maps
    }

    [System.Serializable]
    public class SpawnBatchData
    {
        public int batchId;
        public string timestamp;
        public int entityCount;
        public float totalTimeMs;
        public float avgTimePerEntityMs;
        public List<int> pathIds = new List<int>();
        public float successRate;
    }

    [System.Serializable]
    public class PathfindingData
    {
        public string timestamp;
        public int2 gridSize;
        public List<PathRecord> paths;
        public List<SpawnBatchData> spawnBatches;
        public DataStatistics stats;
        public PerformanceStatistics performance;
    }

    [System.Serializable]
    public class PathRecord
    {
        public int id;
        public int batchId;
        public int2 start;
        public int2 end;
        public bool success;
        public int length;
        public float timeMs; // Will be 0 for DOTS, real timing for Mono
        public bool hasRealTiming; // NEW: Flag to indicate if timing is real
        public string coordinates;
        public List<string> map;
    }

    [System.Serializable]
    public class DataStatistics
    {
        public int total;
        public int successful;
        public float successRate;
        public float avgLength;
        public float avgTimeMs;
        public float totalTimeMs;
        // NEW: Throughput metrics
        public float pathsPerSecond;
        public float avgPathsPerSecond;
        public float peakPathsPerSecond;
    }

    [System.Serializable]
    public class PerformanceStatistics
    {
        public int totalSpawnBatches;
        public float totalSpawnTimeMs;
        public float avgSpawnBatchTimeMs;
        public float fastestBatchTimeMs;
        public float slowestBatchTimeMs;
        public int largestBatchSize;
        public int smallestBatchSize;
        // NEW: Throughput over time
        public float overallPathsPerSecond;
        public float dataCollectionDurationSeconds;
    }

    public class DataCollector : MonoBehaviour
    {
        public int MaxPaths
        {
            get => maxPaths;
            set => maxPaths = value;
        }

        public bool AutoExport
        {
            get => autoExport;
            set => autoExport = value;
        }

        public float AutoExportInterval
        {
            get => autoExportInterval;
            set => autoExportInterval = value;
        }

        public ExportFormat ExportFormat
        {
            get => exportFormat;
            set => exportFormat = value;
        }

        public PathfindingData CurrentData => currentData;
        public bool IsCollecting => currentData?.paths?.Count > 0;

        // NEW: Public properties for UI access
        public float CurrentPathsPerSecond => currentPathsPerSecond;
        public float AveragePathsPerSecond => currentData?.stats?.avgPathsPerSecond ?? 0f;
        public float PeakPathsPerSecond => currentData?.stats?.peakPathsPerSecond ?? 0f;

        public void TriggerExport()
        {
            ExportData();
        }

        public void StartSpawnBatch(int entityCount)
        {
            currentSpawnBatch = new SpawnBatchData
            {
                batchId = nextBatchId++,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                entityCount = entityCount,
                pathIds = new List<int>()
            };
            spawnBatchStartTime = Time.realtimeSinceStartup;

            if (enableDebugLogging)
            {
                Debug.Log($"Started spawn batch {currentSpawnBatch.batchId} with {entityCount} entities");
            }
        }

        public void EndSpawnBatch()
        {
            if (currentSpawnBatch != null)
            {
                float elapsedTime = (Time.realtimeSinceStartup - spawnBatchStartTime) * 1000f;
                currentSpawnBatch.totalTimeMs = elapsedTime;
                currentSpawnBatch.avgTimePerEntityMs = elapsedTime / currentSpawnBatch.entityCount;

                int successfulInBatch = 0;
                foreach (var pathId in currentSpawnBatch.pathIds)
                {
                    var path = currentData.paths.Find(p => p.id == pathId);
                    if (path != null && path.success) successfulInBatch++;
                }
                currentSpawnBatch.successRate = currentSpawnBatch.pathIds.Count > 0 ?
                    (float)successfulInBatch / currentSpawnBatch.pathIds.Count : 0f;

                currentData.spawnBatches.Add(currentSpawnBatch);
                UpdatePerformanceStatistics();

                if (enableDebugLogging)
                {
                    Debug.Log($"Completed spawn batch {currentSpawnBatch.batchId}: {elapsedTime:F1}ms total, " +
                             $"{currentSpawnBatch.avgTimePerEntityMs:F1}ms per entity, {currentSpawnBatch.successRate:P1} success");
                }

                currentSpawnBatch = null;
            }
        }

        [Header("Export Settings")]
        [SerializeField] private ExportFormat exportFormat = ExportFormat.WithCoordinates;
        [SerializeField] private int maxPaths = 100;
        [SerializeField] private string saveDirectory = "PathfindingData";

        [Header("Controls")]
        [SerializeField] private KeyCode exportKey = KeyCode.F5;
        [SerializeField] private bool autoExport = true;
        [SerializeField] private float autoExportInterval = 30f;

        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogging = false;
        [SerializeField] private bool enableVerboseLogging = false;

        [Header("Performance Tracking")]
        [SerializeField] private float throughputUpdateInterval = 1.0f; // How often to calculate paths/sec

        private PathfindingData currentData;
        private HashSet<Entity> processedEntities;
        private EntityManager entityManager;
        private int nextId = 0;
        private int nextBatchId = 0;
        private float lastExportTime;

        private SpawnBatchData currentSpawnBatch;
        private float spawnBatchStartTime;

        // NEW: Throughput tracking variables
        private float lastThroughputUpdate;
        private int pathsAtLastUpdate;
        private float currentPathsPerSecond;
        private List<float> throughputHistory = new List<float>();
        private float dataCollectionStartTime;

        void Start()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            InitializeData();
            CreateDirectory();

            // NEW: Initialize throughput tracking
            dataCollectionStartTime = Time.realtimeSinceStartup;
            lastThroughputUpdate = Time.realtimeSinceStartup;
            pathsAtLastUpdate = 0;
        }

        void Update()
        {
            CollectCompletedPaths();

            // NEW: Update throughput calculations
            UpdateThroughputMetrics();

            // Clean up old processed entities periodically
            if (processedEntities.Count > maxPaths * 2)
            {
                CleanupProcessedEntities();
            }

            if (autoExport && Time.time - lastExportTime >= autoExportInterval)
            {
                ExportData();
                lastExportTime = Time.time;
            }

            if (Input.GetKeyDown(exportKey))
            {
                ExportData();
            }
        }

        void InitializeData()
        {
            currentData = new PathfindingData
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                gridSize = GridManager.Instance ? GridManager.Instance.gridSize : new int2(20, 20),
                paths = new List<PathRecord>(),
                spawnBatches = new List<SpawnBatchData>(),
                stats = new DataStatistics(),
                performance = new PerformanceStatistics()
            };

            processedEntities = new HashSet<Entity>();

            // NEW: Reset throughput tracking
            throughputHistory.Clear();
            currentPathsPerSecond = 0f;
        }

        void CreateDirectory()
        {
            string fullPath = Path.Combine(Application.persistentDataPath, saveDirectory);
            Directory.CreateDirectory(fullPath);
        }

        // NEW: Update throughput metrics
        void UpdateThroughputMetrics()
        {
            float currentTime = Time.realtimeSinceStartup;
            float timeSinceLastUpdate = currentTime - lastThroughputUpdate;

            if (timeSinceLastUpdate >= throughputUpdateInterval)
            {
                int currentPathCount = currentData.paths.Count;
                int pathsCompleted = currentPathCount - pathsAtLastUpdate;

                currentPathsPerSecond = pathsCompleted / timeSinceLastUpdate;
                throughputHistory.Add(currentPathsPerSecond);

                // Keep only recent history (last 60 samples = 1 minute at 1-second intervals)
                if (throughputHistory.Count > 60)
                {
                    throughputHistory.RemoveAt(0);
                }

                if (enableVerboseLogging)
                {
                    Debug.Log($"Throughput: {currentPathsPerSecond:F1} paths/sec (completed {pathsCompleted} in {timeSinceLastUpdate:F1}s)");
                }

                lastThroughputUpdate = currentTime;
                pathsAtLastUpdate = currentPathCount;
            }
        }

        void CollectCompletedPaths()
        {
            if (currentData.paths.Count >= maxPaths)
            {
                if (autoExport)
                {
                    ExportData();
                    InitializeData();
                }
                return;
            }

            var query = entityManager.CreateEntityQuery(typeof(PathfindingRequest), typeof(PathResult));
            var entities = query.ToEntityArray(Allocator.TempJob);

            if (enableVerboseLogging)
            {
                Debug.Log($"Found {entities.Length} total pathfinding entities");
            }

            int newPathsFound = 0;
            int truncatedPaths = 0;

            foreach (var entity in entities)
            {
                if (processedEntities.Contains(entity)) continue;

                if (!entityManager.Exists(entity)) continue;
                if (!entityManager.HasComponent<PathfindingRequest>(entity)) continue;
                if (!entityManager.HasComponent<PathResult>(entity)) continue;

                var request = entityManager.GetComponentData<PathfindingRequest>(entity);

                if (!request.hasResult || request.isProcessing) continue;

                var pathBuffer = entityManager.GetBuffer<PathResult>(entity);

                bool pathComplete = false;
                bool pathTruncated = false;

                if (pathBuffer.Length > 0)
                {
                    var lastPosition = pathBuffer[pathBuffer.Length - 1].position;
                    pathComplete = lastPosition.Equals(request.targetPosition);

                    if (!pathComplete && pathBuffer.Length >= 63)
                    {
                        pathTruncated = true;
                        truncatedPaths++;
                    }
                }

                var record = new PathRecord
                {
                    id = nextId++,
                    batchId = currentSpawnBatch?.batchId ?? -1,
                    start = request.startPosition,
                    end = request.targetPosition,
                    success = pathComplete && !pathTruncated,
                    length = pathBuffer.Length,
                    timeMs = 0f, // No individual timing in DOTS
                    hasRealTiming = false // NEW: Flag that this is DOTS (no real timing)
                };

                if (currentSpawnBatch != null)
                {
                    currentSpawnBatch.pathIds.Add(record.id);
                }

                if (exportFormat >= ExportFormat.WithCoordinates && pathBuffer.Length > 0)
                {
                    var coords = new System.Text.StringBuilder();
                    for (int i = 0; i < pathBuffer.Length; i++)
                    {
                        var pos = pathBuffer[i].position;
                        coords.Append($"({pos.x},{pos.y})");
                        if (i < pathBuffer.Length - 1) coords.Append(" → ");
                    }

                    if (pathTruncated)
                    {
                        coords.Append(" [TRUNCATED]");
                    }

                    record.coordinates = coords.ToString();
                }

                if (exportFormat == ExportFormat.WithMaps)
                {
                    record.map = GenerateMap(record, pathBuffer);
                }

                currentData.paths.Add(record);
                processedEntities.Add(entity);
                newPathsFound++;

                if (enableDebugLogging)
                {
                    string status = pathComplete ? "SUCCESS" : (pathTruncated ? "TRUNCATED" : "FAILED");
                    Debug.Log($"Collected path {record.id}: {record.start} → {record.end} ({status}) - Length: {record.length}");
                }

                if (currentData.paths.Count >= maxPaths) break;
            }

            if (newPathsFound > 0 && enableDebugLogging)
            {
                Debug.Log($"Collected {newPathsFound} new paths this frame. Total: {currentData.paths.Count}");
                if (truncatedPaths > 0)
                {
                    Debug.LogWarning($"Found {truncatedPaths} truncated paths due to buffer capacity limits!");
                }
            }

            entities.Dispose();
            query.Dispose();

            UpdateStatistics();
        }

        void CleanupProcessedEntities()
        {
            var query = entityManager.CreateEntityQuery(typeof(PathfindingRequest));
            var entities = query.ToEntityArray(Allocator.TempJob);
            var validEntities = new HashSet<Entity>();

            foreach (var entity in entities)
            {
                validEntities.Add(entity);
            }

            var toRemove = new List<Entity>();
            foreach (var entity in processedEntities)
            {
                if (!validEntities.Contains(entity))
                {
                    toRemove.Add(entity);
                }
            }

            foreach (var entity in toRemove)
            {
                processedEntities.Remove(entity);
            }

            if (enableVerboseLogging && toRemove.Count > 0)
            {
                Debug.Log($"Cleaned up {toRemove.Count} processed entities");
            }

            entities.Dispose();
            query.Dispose();
        }

        List<string> GenerateMap(PathRecord record, DynamicBuffer<PathResult> pathBuffer)
        {
            var map = new List<string>();
            if (GridManager.Instance == null) return map;

            var grid = GridManager.Instance;
            var pathPositions = new HashSet<int2>();

            for (int i = 0; i < pathBuffer.Length; i++)
            {
                pathPositions.Add(pathBuffer[i].position);
            }

            for (int y = grid.gridSize.y - 1; y >= 0; y--)
            {
                var row = new System.Text.StringBuilder();

                for (int x = 0; x < grid.gridSize.x; x++)
                {
                    int2 pos = new int2(x, y);

                    if (pos.Equals(record.start))
                        row.Append('S');
                    else if (pos.Equals(record.end))
                        row.Append('E');
                    else if (!grid.IsWalkable(pos))
                        row.Append('█');
                    else if (pathPositions.Contains(pos))
                        row.Append('●');
                    else
                        row.Append('·');
                }

                map.Add(row.ToString());
            }

            return map;
        }

        void UpdateStatistics()
        {
            var stats = currentData.stats;
            stats.total = currentData.paths.Count;
            stats.successful = 0;

            float totalLength = 0;
            float totalTime = 0;

            foreach (var path in currentData.paths)
            {
                if (path.success)
                {
                    stats.successful++;
                    totalLength += path.length;
                }
                totalTime += path.timeMs;
            }

            stats.successRate = stats.total > 0 ? (float)stats.successful / stats.total : 0f;
            stats.avgLength = stats.successful > 0 ? totalLength / stats.successful : 0f;
            stats.avgTimeMs = stats.total > 0 ? totalTime / stats.total : 0f;
            stats.totalTimeMs = totalTime;

            // NEW: Update throughput statistics
            stats.pathsPerSecond = currentPathsPerSecond;

            if (throughputHistory.Count > 0)
            {
                float sum = 0f;
                float peak = 0f;
                foreach (var throughput in throughputHistory)
                {
                    sum += throughput;
                    if (throughput > peak) peak = throughput;
                }
                stats.avgPathsPerSecond = sum / throughputHistory.Count;
                stats.peakPathsPerSecond = peak;
            }
        }

        void UpdatePerformanceStatistics()
        {
            var perf = currentData.performance;
            perf.totalSpawnBatches = currentData.spawnBatches.Count;

            if (currentData.spawnBatches.Count > 0)
            {
                float totalSpawnTime = 0;
                float fastest = float.MaxValue;
                float slowest = 0;
                int largest = 0;
                int smallest = int.MaxValue;

                foreach (var batch in currentData.spawnBatches)
                {
                    totalSpawnTime += batch.totalTimeMs;
                    if (batch.totalTimeMs < fastest) fastest = batch.totalTimeMs;
                    if (batch.totalTimeMs > slowest) slowest = batch.totalTimeMs;
                    if (batch.entityCount > largest) largest = batch.entityCount;
                    if (batch.entityCount < smallest) smallest = batch.entityCount;
                }

                perf.totalSpawnTimeMs = totalSpawnTime;
                perf.avgSpawnBatchTimeMs = totalSpawnTime / currentData.spawnBatches.Count;
                perf.fastestBatchTimeMs = fastest;
                perf.slowestBatchTimeMs = slowest;
                perf.largestBatchSize = largest;
                perf.smallestBatchSize = smallest;
            }

            // NEW: Calculate overall throughput
            float totalDataTime = Time.realtimeSinceStartup - dataCollectionStartTime;
            perf.dataCollectionDurationSeconds = totalDataTime;
            perf.overallPathsPerSecond = totalDataTime > 0 ? currentData.paths.Count / totalDataTime : 0f;
        }

        void ExportData()
        {
            if (currentData.paths.Count == 0) return;

            string fileName = $"Pathfinding_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string fullPath = Path.Combine(Application.persistentDataPath, saveDirectory, fileName);

            try
            {
                string json = JsonUtility.ToJson(currentData, true);
                File.WriteAllText(fullPath, json);

                Debug.Log($"Exported {currentData.paths.Count} paths to: {fullPath}");
                Debug.Log($"Success rate: {currentData.stats.successRate:P1}, Avg time: {currentData.stats.avgTimeMs:F1}ms, Total time: {currentData.stats.totalTimeMs:F1}ms");

                // NEW: Throughput logging
                Debug.Log($"Throughput: Current: {currentData.stats.pathsPerSecond:F1} paths/sec, " +
                         $"Average: {currentData.stats.avgPathsPerSecond:F1} paths/sec, " +
                         $"Peak: {currentData.stats.peakPathsPerSecond:F1} paths/sec, " +
                         $"Overall: {currentData.performance.overallPathsPerSecond:F1} paths/sec");

                if (currentData.performance.totalSpawnBatches > 0)
                {
                    Debug.Log($"Performance: {currentData.performance.totalSpawnBatches} batches, " +
                             $"Total spawn time: {currentData.performance.totalSpawnTimeMs:F1}ms, " +
                             $"Avg per batch: {currentData.performance.avgSpawnBatchTimeMs:F1}ms");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Export failed: {e.Message}");
            }
        }
    }
}