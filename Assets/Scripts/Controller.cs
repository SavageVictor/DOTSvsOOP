//using System.Collections;
//using UnityEngine;
//using Unity.Collections;
//using Unity.Jobs;
//using Unity.Mathematics;

//public class Controller : MonoBehaviour
//{

//    [Header("Grid Settings")]
//    public int2 gridSize = new int2(100, 100);
//    public bool usePreGeneratedGrid = true;
//    public bool includeObstacles = false;

//    [Header("Test Settings")]
//    public int simultaneousPathfinds = 5;
//    public float testInterval = 1f;
//    public bool runContinuousTests = true;

//    [Header("Path Points")]
//    public int2 startPosition = new int2(0, 0);
//    public int2 endPosition = new int2(99, 99);

//    private NativeArray<PathNode> preGeneratedGrid;
//    private Coroutine testCoroutine;

//    private void Start()
//    {
//        // Предварительно генерируем сетку если нужно
//        if (usePreGeneratedGrid)
//        {
//            GenerateGrid();
//        }

//        // Запускаем периодическое тестирование
//        if (runContinuousTests)
//        {
//            StartContinuousTests();
//        }
//    }

//    private void GenerateGrid()
//    {
//        if (preGeneratedGrid.IsCreated)
//        {
//            preGeneratedGrid.Dispose();
//        }

//        if (includeObstacles)
//        {
//            // Создаем несколько препятствий для тестирования
//            NativeArray<int2> obstacles = new NativeArray<int2>(3, Allocator.Temp);
//            obstacles[0] = new int2(50, 50);
//            obstacles[1] = new int2(25, 75);
//            obstacles[2] = new int2(75, 25);

//            preGeneratedGrid = GridGenerator.GenerateGridWithObstacles(gridSize, endPosition, obstacles, Allocator.Persistent);
//            obstacles.Dispose();
//        }
//        else
//        {
//            preGeneratedGrid = GridGenerator.GenerateGrid(gridSize, endPosition, Allocator.Persistent);
//        }

//        Debug.Log($"Grid generated: {gridSize.x}x{gridSize.y} = {gridSize.x * gridSize.y} nodes");
//    }

//    private void RunPathfindingTest()
//    {
//        float startTime = Time.realtimeSinceStartup;

//        NativeArray<JobHandle> jobHandleArray = new NativeArray<JobHandle>(simultaneousPathfinds, Allocator.TempJob);
//        NativeArray<NativeList<int2>> pathResults = new NativeArray<NativeList<int2>>(simultaneousPathfinds, Allocator.TempJob);

//        // Создаем и запускаем задачи
//        for (int i = 0; i < simultaneousPathfinds; i++)
//        {
//            pathResults[i] = new NativeList<int2>(Allocator.TempJob);

//            PathfindingJob pathfindingJob = new PathfindingJob
//            {
//                startPosition = startPosition,
//                endPosition = endPosition,
//                gridSize = gridSize,
//                usePreGeneratedGrid = usePreGeneratedGrid,
//                preGeneratedGrid = preGeneratedGrid,
//                resultPath = pathResults[i]
//            };

//            jobHandleArray[i] = pathfindingJob.Schedule();
//        }

//        // Ждем завершения всех задач
//        JobHandle.CompleteAll(jobHandleArray);

//        float executionTime = (Time.realtimeSinceStartup - startTime) * 1000f;

//        // Выводим результаты
//        Debug.Log($"Pathfinding completed in {executionTime:F2}ms for {simultaneousPathfinds} simultaneous searches");

//        // Проверяем первый найденный путь
//        if (pathResults[0].Length > 0)
//        {
//            Debug.Log($"Path found with {pathResults[0].Length} nodes");
//        }
//        else
//        {
//            Debug.Log("No path found!");
//        }

//        // Освобождаем ресурсы
//        for (int i = 0; i < simultaneousPathfinds; i++)
//        {
//            if (pathResults[i].IsCreated)
//            {
//                pathResults[i].Dispose();
//            }
//        }

//        jobHandleArray.Dispose();
//        pathResults.Dispose();
//    }

//    private void OnDestroy()
//    {
//        if (preGeneratedGrid.IsCreated)
//        {
//            preGeneratedGrid.Dispose();
//        }
//    }

//    // Публичные методы для тестирования
//    public void SetGridSize(int width, int height)
//    {
//        gridSize = new int2(width, height);
//        if (usePreGeneratedGrid)
//        {
//            GenerateGrid();
//        }
//    }

//    public void SetTestParameters(int pathfindCount, int2 start, int2 end)
//    {
//        simultaneousPathfinds = pathfindCount;
//        startPosition = start;
//        endPosition = end;
//    }

//    public void RegenerateGrid()
//    {
//        GenerateGrid();
//    }

//    public void StartContinuousTests()
//    {
//        if (testCoroutine != null)
//        {
//            StopCoroutine(testCoroutine);
//        }
//        testCoroutine = StartCoroutine(ContinuousTestCoroutine());
//    }

//    public void StopContinuousTests()
//    {
//        if (testCoroutine != null)
//        {
//            StopCoroutine(testCoroutine);
//            testCoroutine = null;
//        }
//    }

//    public void RunSingleTest()
//    {
//        RunPathfindingTest();
//    }

//    private IEnumerator ContinuousTestCoroutine()
//    {
//        while (true)
//        {
//            RunPathfindingTest();
//            yield return new WaitForSeconds(testInterval);
//        }
//    }
//}