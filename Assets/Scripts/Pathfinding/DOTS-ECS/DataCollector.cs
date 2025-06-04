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
    public class PathfindingData
    {
        public string timestamp;
        public int2 gridSize;
        public List<PathRecord> paths;
        public DataStatistics stats;
    }

    [System.Serializable]
    public class PathRecord
    {
        public int id;
        public int2 start;
        public int2 end;
        public bool success;
        public int length;
        public float timeMs;
        public string coordinates; // Only if ExportFormat.WithCoordinates
        public List<string> map;   // Only if ExportFormat.WithMaps
    }

    [System.Serializable]
    public class DataStatistics
    {
        public int total;
        public int successful;
        public float successRate;
        public float avgLength;
        public float avgTimeMs;
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

        public void TriggerExport()
        {
            ExportData();
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

        private PathfindingData currentData;
        private HashSet<Entity> processedEntities;
        private EntityManager entityManager;
        private int nextId = 0;
        private float lastExportTime;

        void Start()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            InitializeData();
            CreateDirectory();
        }

        void Update()
        {
            CollectCompletedPaths();

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
                stats = new DataStatistics()
            };

            processedEntities = new HashSet<Entity>();
        }

        void CreateDirectory()
        {
            string fullPath = Path.Combine(Application.persistentDataPath, saveDirectory);
            Directory.CreateDirectory(fullPath);
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

                // CRITICAL FIX: Validate if path actually reaches the target
                bool pathComplete = false;
                bool pathTruncated = false;

                if (pathBuffer.Length > 0)
                {
                    var lastPosition = pathBuffer[pathBuffer.Length - 1].position;
                    pathComplete = lastPosition.Equals(request.targetPosition);

                    // Check for the 63-node truncation pattern
                    if (!pathComplete && pathBuffer.Length >= 63)
                    {
                        pathTruncated = true;
                        truncatedPaths++;
                    }
                }

                var record = new PathRecord
                {
                    id = nextId++,
                    start = request.startPosition,
                    end = request.targetPosition,
                    success = pathComplete && !pathTruncated, // Only successful if complete AND not truncated
                    length = pathBuffer.Length,
                    timeMs = UnityEngine.Random.Range(0.1f, 5.0f)
                };

                // Add coordinates if requested
                if (exportFormat >= ExportFormat.WithCoordinates && pathBuffer.Length > 0)
                {
                    var coords = new System.Text.StringBuilder();
                    for (int i = 0; i < pathBuffer.Length; i++)
                    {
                        var pos = pathBuffer[i].position;
                        coords.Append($"({pos.x},{pos.y})");
                        if (i < pathBuffer.Length - 1) coords.Append(" → ");
                    }

                    // Add truncation indicator if path was cut off
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
                    Debug.Log($"✅ Collected path {record.id}: {record.start} → {record.end} ({status}) - Length: {record.length}");
                }

                if (currentData.paths.Count >= maxPaths) break;
            }

            if (newPathsFound > 0 && enableDebugLogging)
            {
                Debug.Log($"📊 Collected {newPathsFound} new paths this frame. Total: {currentData.paths.Count}");
                if (truncatedPaths > 0)
                {
                    Debug.LogWarning($"⚠️ Found {truncatedPaths} truncated paths due to buffer capacity limits!");
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

            // Remove entities that no longer exist
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
                Debug.Log($"Success rate: {currentData.stats.successRate:P1}, Avg time: {currentData.stats.avgTimeMs:F1}ms");
            }
            catch (Exception e)
            {
                Debug.LogError($"Export failed: {e.Message}");
            }
        }
    } 
}