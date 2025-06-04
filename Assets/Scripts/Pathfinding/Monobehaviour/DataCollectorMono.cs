using DOTS_ECS;
using System.Collections.Generic;
using System;
using Unity.Mathematics;
using UnityEngine;

namespace Mono
{
    public class DataCollectorMono : MonoBehaviour
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
        private HashSet<int> processedRequestIds = new HashSet<int>();
        private float lastExportTime;

        void Start()
        {
            InitializeData();
            CreateDirectory();
        }

        void Update()
        {
            CollectCompletedPaths();

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

            processedRequestIds.Clear();
        }

        void CreateDirectory()
        {
            string fullPath = System.IO.Path.Combine(Application.persistentDataPath, saveDirectory);
            System.IO.Directory.CreateDirectory(fullPath);
        }

        void CollectCompletedPaths()
        {
            if (PathfindingSystem.Instance == null) return;

            if (currentData.paths.Count >= maxPaths)
            {
                if (autoExport)
                {
                    ExportData();
                    InitializeData();
                }
                return;
            }

            var completedRequests = PathfindingSystem.Instance.GetAllCompletedRequests();
            int newPathsFound = 0;
            int truncatedPaths = 0;

            foreach (var request in completedRequests)
            {
                if (processedRequestIds.Contains(request.id)) continue;
                if (currentData.paths.Count >= maxPaths) break;

                bool pathComplete = request.success && !request.wasTruncated;
                bool pathTruncated = request.wasTruncated;

                if (pathTruncated)
                {
                    truncatedPaths++;
                }

                var record = new PathRecord
                {
                    id = request.id,
                    start = request.startPosition,
                    end = request.targetPosition,
                    success = pathComplete,
                    length = request.pathPositions.Count,
                    timeMs = request.processingTime
                };

                // Add coordinates if requested
                if (exportFormat >= ExportFormat.WithCoordinates && request.pathPositions.Count > 0)
                {
                    var coords = new System.Text.StringBuilder();
                    for (int i = 0; i < request.pathPositions.Count; i++)
                    {
                        var pos = request.pathPositions[i];
                        coords.Append($"({pos.x},{pos.y})");
                        if (i < request.pathPositions.Count - 1) coords.Append(" → ");
                    }

                    if (pathTruncated)
                    {
                        coords.Append(" [TRUNCATED]");
                    }

                    record.coordinates = coords.ToString();
                }

                if (exportFormat == ExportFormat.WithMaps)
                {
                    record.map = GenerateMap(record, request.pathPositions);
                }

                currentData.paths.Add(record);
                processedRequestIds.Add(request.id);
                newPathsFound++;

                if (enableDebugLogging)
                {
                    string status = pathComplete ? "SUCCESS" : (pathTruncated ? "TRUNCATED" : "FAILED");
                    Debug.Log($"✅ Collected path {record.id}: {record.start} → {record.end} ({status}) - Length: {record.length}");
                }
            }

            if (newPathsFound > 0 && enableDebugLogging)
            {
                Debug.Log($"📊 Collected {newPathsFound} new paths this frame. Total: {currentData.paths.Count}");
                if (truncatedPaths > 0)
                {
                    Debug.LogWarning($"⚠️ Found {truncatedPaths} truncated paths due to buffer capacity limits!");
                }
            }

            UpdateStatistics();
        }

        List<string> GenerateMap(PathRecord record, List<int2> pathPositions)
        {
            var map = new List<string>();
            if (GridManager.Instance == null) return map;

            var grid = GridManager.Instance;
            var pathPositionSet = new HashSet<int2>(pathPositions);

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
                    else if (pathPositionSet.Contains(pos))
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
            string fullPath = System.IO.Path.Combine(Application.persistentDataPath, saveDirectory, fileName);

            try
            {
                string json = JsonUtility.ToJson(currentData, true);
                System.IO.File.WriteAllText(fullPath, json);

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