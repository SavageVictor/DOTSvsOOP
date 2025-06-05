using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace DOTS_ECS
{
    public class DOTSUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Canvas uiCanvas;
        [SerializeField] private Button switchToMonoButton;

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

        [Header("Component References")]
        [SerializeField] private PathfindingSpawner pathfindingSpawner;
        [SerializeField] private DataCollector dataCollector;
        [SerializeField] private GridManager gridManager;

        [Header("Scene Settings")]
        [SerializeField] private string monoSceneName = "MonoPathfinding";

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
            HandleInputs();
        }

        void SetupUI()
        {
            // Button events
            switchToMonoButton.onClick.AddListener(SwitchToMono);
            spawnButton.onClick.AddListener(SpawnRequests);
            clearRequestsButton.onClick.AddListener(ClearRequests);
            exportNowButton.onClick.AddListener(ExportNow);
            regenerateGridButton.onClick.AddListener(RegenerateGrid);

            // Input field events
            entityCountInput.onEndEdit.AddListener(OnEntityCountChanged);
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

                performanceText.text = $"DOTS ECS | FPS: {fps:F1} | Frame: {ms:F1}ms";
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
                                $"Avg Time: {stats.avgTimeMs:F1}ms";
            }
            else
            {
                statsText.text = "No data available";
            }
        }

        // UI Actions
        void ToggleUI()
        {
            uiCanvas.enabled = !uiCanvas.enabled;
        }

        void SwitchToMono()
        {
            SceneManager.LoadScene(monoSceneName);
        }

        void SpawnRequests()
        {
            if (pathfindingSpawner != null)
            {
                pathfindingSpawner.SpawnAllInstantly();
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