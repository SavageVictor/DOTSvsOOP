using System;
using System.Collections.Generic;
using UnityEngine;

namespace PerformanceComparison
{
    // Core interfaces for pathfinding implementations
    public interface IPathfinder
    {
        string ImplementationName { get; }
        List<Vector2Int> FindPath(IGrid grid, Vector2Int start, Vector2Int goal);
        void Initialize();
        void Cleanup();
    }

    public interface IGrid
    {
        int Width { get; }
        int Height { get; }
        bool IsWalkable(int x, int y);
        bool IsWalkable(Vector2Int position);
        float GetCost(Vector2Int from, Vector2Int to);
        Vector2Int[] GetNeighbors(Vector2Int position);
    }

    // Core interfaces for physics implementations
    public interface IPhysicsSystem
    {
        string ImplementationName { get; }
        void Initialize();
        void Cleanup();
        void Simulate(float deltaTime);
        IPhysicsBody CreateBody(Vector3 position, Vector3 velocity, float mass);
        void RemoveBody(IPhysicsBody body);
        int ActiveBodyCount { get; }
    }

    public interface IPhysicsBody
    {
        Vector3 Position { get; set; }
        Vector3 Velocity { get; set; }
        float Mass { get; set; }
        bool IsActive { get; set; }
    }

    // Performance measurement interfaces
    public interface IPerformanceProfiler
    {
        void StartProfiling(string testName);
        void StopProfiling();
        PerformanceMetrics GetResults();
        void Reset();
    }

    public interface ITestScenario
    {
        string Name { get; }
        string Description { get; }
        TestParameters Parameters { get; }

        void Setup();
        void Cleanup();
        TestResults RunTest(IImplementationProvider provider);
    }

    public interface IImplementationProvider
    {
        IPathfinder GetPathfinder();
        IPhysicsSystem GetPhysicsSystem();
        IPerformanceProfiler GetProfiler();
        string ProviderName { get; }
    }

    // Data structures for test management
    [Serializable]
    public struct TestParameters
    {
        public int agentCount;
        public int gridSize;
        public float obstacleRatio;
        public int iterationCount;
        public bool enableParallelization;
        public bool enableBurstCompilation;
    }

    [Serializable]
    public struct PerformanceMetrics
    {
        public float executionTimeMs;
        public float memoryUsageMB;
        public float cpuUsagePercent;
        public int gcAllocations;
        public float frameTime;
        public int successfulPaths;
        public int failedPaths;
    }

    [Serializable]
    public struct TestResults
    {
        public string testName;
        public string implementationName;
        public TestParameters parameters;
        public PerformanceMetrics metrics;
        public DateTime timestamp;
        public bool successful;
        public string errorMessage;
    }

    // Abstract base classes for common functionality
    public abstract class BaseTestScenario : ITestScenario
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public virtual TestParameters Parameters { get; protected set; }

        protected IPerformanceProfiler profiler;
        protected bool isSetup = false;

        public virtual void Setup()
        {
            if (isSetup) return;
            OnSetup();
            isSetup = true;
        }

        public virtual void Cleanup()
        {
            if (!isSetup) return;
            OnCleanup();
            isSetup = false;
        }

        public virtual TestResults RunTest(IImplementationProvider provider)
        {
            if (!isSetup)
                throw new InvalidOperationException("Test scenario must be set up before running");

            profiler = provider.GetProfiler();
            profiler.Reset();

            var result = new TestResults
            {
                testName = Name,
                implementationName = provider.ProviderName,
                parameters = Parameters,
                timestamp = DateTime.Now,
                successful = false
            };

            try
            {
                profiler.StartProfiling(Name);

                var testResult = ExecuteTest(provider);
                result.successful = testResult;

                profiler.StopProfiling();
                result.metrics = profiler.GetResults();
            }
            catch (Exception ex)
            {
                result.errorMessage = ex.Message;
                Debug.LogError($"Test {Name} failed: {ex.Message}");
            }

            return result;
        }

        protected abstract void OnSetup();
        protected abstract void OnCleanup();
        protected abstract bool ExecuteTest(IImplementationProvider provider);
    }

    // Factory interface for creating implementations
    public interface IImplementationFactory
    {
        IImplementationProvider CreateMonoBehaviourProvider();
        IImplementationProvider CreateDOTSProvider();
        IImplementationProvider[] GetAllProviders();
    }

    // Test runner interface
    public interface ITestRunner
    {
        void RegisterScenario(ITestScenario scenario);
        void RegisterImplementation(IImplementationProvider provider);
        TestResults[] RunAllTests();
        TestResults[] RunScenario(string scenarioName);
        TestResults[] RunImplementation(string implementationName);
        void ExportResults(string filePath);
    }

    // Comparison and analysis interfaces
    public interface IPerformanceComparer
    {
        ComparisonResults Compare(TestResults[] monoResults, TestResults[] dotsResults);
        void GenerateReport(ComparisonResults results, string outputPath);
    }

    [Serializable]
    public struct ComparisonResults
    {
        public string comparisonName;
        public DateTime timestamp;
        public PerformanceComparison[] comparisons;
        public Summary summary;
    }

    [Serializable]
    public struct PerformanceComparison
    {
        public string testName;
        public string metricName;
        public float monoValue;
        public float dotsValue;
        public float improvementRatio;
        public float improvementPercent;
        public bool dotsIsBetter;
    }

    [Serializable]
    public struct Summary
    {
        public float averageSpeedupRatio;
        public float averageMemoryImprovement;
        public int totalTestsRun;
        public int dotsWinCount;
        public int monoWinCount;
        public string recommendation;
    }

    // Configuration interface
    public interface ITestConfiguration
    {
        TestParameters GetDefaultParameters();
        TestParameters GetStressTestParameters();
        TestParameters GetMobileOptimizedParameters();
        void SaveConfiguration(string filePath);
        void LoadConfiguration(string filePath);
    }
}