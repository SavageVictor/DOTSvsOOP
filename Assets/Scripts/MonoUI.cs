using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DOTS_ECS;

namespace Mono
{
    public class MonoUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Canvas uiCanvas;
        [SerializeField] private Button switchToDOTSButton;

        [Header("Spawner Settings")]
        [SerializeField] private TMP_InputField entityCountInput;
        [SerializeField] private TMP_InputField spawnIntervalInput;
        [SerializeField] private Button spawnButton;
        [SerializeField] private Button clearRequestsButton;

        [Header("Data Collector Settings")]
        [SerializeField] private TMP_InputField maxPathsInput;
        [SerializeField] private TMP_InputField autoExportIntervalInput;
        [SerializeField] private Toggle autoExportToggle;
        [SerializeField] private TMP_Dropdown exportFormatDropdown;
        [SerializeField] private Button exportNowButton;

        [Header("Grid Settings")]
        [SerializeField] private TMP_InputField gridWidthInput;
        [SerializeField] private TMP_InputField gridHeightInput;
        [SerializeField] private Slider obstaclePercentageSlider;
        [SerializeField] private TMP_Text obstaclePercentageText;
        [SerializeField] private Button regenerateGridButton;

        [Header("Display")]
        [SerializeField] private TMP_Text performanceText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text batchStatsText;
        [SerializeField] private TMP_Text throughputText; // NEW: For throughput display

        [Header("Component References")]
        [SerializeField] private PathfindingSpawner pathfindingSpawner;
        [SerializeField] private DataCollectorMono dataCollector;
        [SerializeField] private GridManager gridManager;

        [Header("Scene Settings")]
        [SerializeField] private string dotsSceneName = "DOTSPathfinding";

        private float deltaTime;
        private int frameCount;

        void Start()
        {
            SetupUI();
            LoadCurrentSettings();
        }

        void Update()
        {
            UpdatePerformanceDisplay();
            UpdateStatsDisplay();
            UpdateBatchStatsDisplay();
            UpdateThroughputDisplay(); // NEW: Update throughput display
            HandleInputs();
        }

        void SetupUI()
        {
            // Button events
            switchToDOTSButton.onClick.AddListener(SwitchToDOTS);
            spawnButton.onClick.AddListener(SpawnRequests);
            clearRequestsButton.onClick.AddListener(ClearRequests);
            exportNowButton.onClick.AddListener(ExportNow);
            regenerateGridButton.onClick.AddListener(RegenerateGrid);

            // Input field events
            entityCountInput.onEndEdit.AddListener(OnEntityCountChanged);
            spawnIntervalInput.onEndEdit.AddListener(OnSpawnIntervalChanged);
            maxPathsInput.onEndEdit.AddListener(OnMaxPathsChanged);
            autoExportIntervalInput.onEndEdit.AddListener(OnAutoExportIntervalChanged);
            gridWidthInput.onEndEdit.AddListener(OnGridWidthChanged);
            gridHeightInput.onEndEdit.AddListener(OnGridHeightChanged);

            // Toggle and slider events
            autoExportToggle.onValueChanged.AddListener(OnAutoExportChanged);
            obstaclePercentageSlider.onValueChanged.AddListener(OnObstaclePercentageChanged);
            exportFormatDropdown.onValueChanged.AddListener(OnExportFormatChanged);

            // Setup export format dropdown
            exportFormatDropdown.ClearOptions();
            exportFormatDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Minimal",
                "With Coordinates",
                "With Maps"
            });
        }

        void LoadCurrentSettings()
        {
            if (pathfindingSpawner != null)
            {
                entityCountInput.text = pathfindingSpawner.EntityCount.ToString();
                spawnIntervalInput.text = pathfindingSpawner.SpawnInterval.ToString("F2");
            }

            if (dataCollector != null)
            {
                maxPathsInput.text = dataCollector.MaxPaths.ToString();
                autoExportIntervalInput.text = dataCollector.AutoExportInterval.ToString("F1");
                autoExportToggle.isOn = dataCollector.AutoExport;
                exportFormatDropdown.value = (int)dataCollector.ExportFormat;
            }

            if (gridManager != null)
            {
                gridWidthInput.text = gridManager.gridSize.x.ToString();
                gridHeightInput.text = gridManager.gridSize.y.ToString();
                obstaclePercentageSlider.value = gridManager.obstaclePercentage;
                OnObstaclePercentageChanged(gridManager.obstaclePercentage);
            }
        }

        void HandleInputs()
        {
            // Toggle UI with Tab
            if (Input.GetKeyDown(KeyCode.Tab))
                ToggleUI();
        }

        void UpdatePerformanceDisplay()
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            frameCount++;

            if (frameCount % 30 == 0) // Update every 30 frames
            {
                float fps = 1.0f / deltaTime;
                float ms = deltaTime * 1000.0f;

                // Add throughput to performance display
                float currentThroughput = dataCollector != null ? dataCollector.CurrentPathsPerSecond : 0f;

                performanceText.text = $"MonoBehaviour | FPS: {fps:F1} | Frame: {ms:F1}ms | Throughput: {currentThroughput:F1} paths/sec";
            }
        }

        void UpdateStatsDisplay()
        {
            if (dataCollector != null && dataCollector.CurrentData != null)
            {
                var stats = dataCollector.CurrentData.stats;
                var pathCount = dataCollector.CurrentData.paths.Count;

                statsText.text = $"Paths: {pathCount}/{dataCollector.MaxPaths} | " +
                                $"Success: {stats.successRate:P1} | " +
                                $"Avg Time: {stats.avgTimeMs:F1}ms | " +
                                $"Avg Length: {stats.avgLength:F1}"; // Show both timing and length for Mono
            }
            else
            {
                statsText.text = "No data available";
            }
        }

        void UpdateBatchStatsDisplay()
        {
            if (batchStatsText == null) return;

            if (dataCollector != null && dataCollector.CurrentData != null && dataCollector.CurrentData.performance != null)
            {
                var perf = dataCollector.CurrentData.performance;
                var batchCount = dataCollector.CurrentData.spawnBatches.Count;

                if (batchCount > 0)
                {
                    batchStatsText.text = $"Batches: {batchCount} | " +
                                         $"Total Spawn Time: {perf.totalSpawnTimeMs:F1}ms | " +
                                         $"Avg/Batch: {perf.avgSpawnBatchTimeMs:F1}ms | " +
                                         $"Fastest: {perf.fastestBatchTimeMs:F1}ms | " +
                                         $"Slowest: {perf.slowestBatchTimeMs:F1}ms";
                }
                else
                {
                    batchStatsText.text = "No spawn batches yet";
                }
            }
            else
            {
                batchStatsText.text = "Batch stats unavailable";
            }
        }

        // NEW: Update throughput display
        void UpdateThroughputDisplay()
        {
            if (throughputText == null) return;

            if (dataCollector != null && dataCollector.CurrentData != null)
            {
                var stats = dataCollector.CurrentData.stats;
                var perf = dataCollector.CurrentData.performance;

                // Show current, average, peak, and overall throughput
                throughputText.text = $"Current: {stats.pathsPerSecond:F1} paths/sec | " +
                                     $"Avg: {stats.avgPathsPerSecond:F1} | " +
                                     $"Peak: {stats.peakPathsPerSecond:F1} | " +
                                     $"Overall: {perf.overallPathsPerSecond:F1}";

                // Color coding based on performance (different thresholds for MonoBehaviour)
                if (stats.pathsPerSecond > 10f)
                    throughputText.color = Color.green;
                else if (stats.pathsPerSecond > 5f)
                    throughputText.color = Color.yellow;
                else if (stats.pathsPerSecond > 0f)
                    throughputText.color = Color.white;
                else
                    throughputText.color = Color.gray;
            }
            else
            {
                throughputText.text = "Throughput: Waiting for data...";
                throughputText.color = Color.gray;
            }
        }

        // UI Actions
        void ToggleUI()
        {
            uiCanvas.enabled = !uiCanvas.enabled;
        }

        void SwitchToDOTS()
        {
            SceneManager.LoadScene(dotsSceneName);
        }

        void SpawnRequests()
        {
            if (pathfindingSpawner != null)
            {
                StartCoroutine(pathfindingSpawner.SpawnPathfindingRequests());
            }
        }

        void ClearRequests()
        {
            if (pathfindingSpawner != null)
            {
                pathfindingSpawner.ClearAllRequests();
            }
        }

        void ExportNow()
        {
            if (dataCollector != null)
            {
                dataCollector.TriggerExport();
            }
        }

        void RegenerateGrid()
        {
            if (gridManager != null)
            {
                gridManager.RegenerateGrid();
                Debug.Log("Grid regenerated!");
            }
        }

        // Setting Change Handlers
        void OnEntityCountChanged(string value)
        {
            if (pathfindingSpawner != null && int.TryParse(value, out int count))
            {
                pathfindingSpawner.EntityCount = count;
            }
        }

        void OnSpawnIntervalChanged(string value)
        {
            if (pathfindingSpawner != null && float.TryParse(value, out float interval))
            {
                pathfindingSpawner.SpawnInterval = interval;
            }
        }

        void OnMaxPathsChanged(string value)
        {
            if (dataCollector != null && int.TryParse(value, out int maxPaths))
            {
                dataCollector.MaxPaths = maxPaths;
            }
        }

        void OnAutoExportIntervalChanged(string value)
        {
            if (dataCollector != null && float.TryParse(value, out float interval))
            {
                dataCollector.AutoExportInterval = interval;
            }
        }

        void OnAutoExportChanged(bool value)
        {
            if (dataCollector != null)
            {
                dataCollector.AutoExport = value;
            }
        }

        void OnExportFormatChanged(int value)
        {
            if (dataCollector != null)
            {
                dataCollector.ExportFormat = (ExportFormat)value;
            }
        }

        void OnObstaclePercentageChanged(float value)
        {
            obstaclePercentageText.text = $"Obstacles: {value:P0}";

            if (gridManager != null)
            {
                gridManager.obstaclePercentage = value;
            }
        }

        void OnGridWidthChanged(string value)
        {
            if (gridManager != null && int.TryParse(value, out int width) && width > 0)
            {
                gridManager.gridSize = new Unity.Mathematics.int2(width, gridManager.gridSize.y);
            }
        }

        void OnGridHeightChanged(string value)
        {
            if (gridManager != null && int.TryParse(value, out int height) && height > 0)
            {
                gridManager.gridSize = new Unity.Mathematics.int2(gridManager.gridSize.x, height);
            }
        }
    }
}