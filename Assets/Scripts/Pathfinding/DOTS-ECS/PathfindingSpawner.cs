using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections;

namespace DOTS_ECS
{
    public class PathfindingSpawner : MonoBehaviour
    {
        public int EntityCount
        {
            get => entityCount;
            set => entityCount = value;
        }

        public float SpawnInterval
        {
            get => spawnInterval;
            set => spawnInterval = value;
        }

        [Header("Spawn Settings")]
        [SerializeField] private int entityCount = 10;
        [SerializeField] private float spawnInterval = 1f;
        [SerializeField] private KeyCode spawnKey = KeyCode.Space;
        [SerializeField] private KeyCode clearKey = KeyCode.C;

        private EntityManager entityManager;
        private Unity.Mathematics.Random random;
        private DataCollector dataCollector; // NEW: Reference to data collector

        void Start()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);

            // NEW: Find the data collector in the scene
            dataCollector = FindFirstObjectByType<DataCollector>();
            if (dataCollector == null)
            {
                Debug.LogWarning("DataCollector not found! Spawn batch timing will not be tracked.");
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(spawnKey))
            {
                StartCoroutine(SpawnPathfindingRequests());
            }

            if (Input.GetKeyDown(clearKey))
            {
                ClearAllRequests();
            }
        }

        public IEnumerator SpawnPathfindingRequests()
        {
            Debug.Log($"Spawning {entityCount} pathfinding requests...");

            // NEW: Start tracking this spawn batch
            if (dataCollector != null)
            {
                dataCollector.StartSpawnBatch(entityCount);
            }

            // Track the start time for performance measurement
            float batchStartTime = Time.realtimeSinceStartup;

            for (int i = 0; i < entityCount; i++)
            {
                SpawnSingleRequest();
                yield return new WaitForSeconds(spawnInterval);
            }

            // NEW: End tracking this spawn batch
            if (dataCollector != null)
            {
                dataCollector.EndSpawnBatch();
            }

            float batchTotalTime = (Time.realtimeSinceStartup - batchStartTime) * 1000f; // Convert to ms
            Debug.Log($"🎯 Spawn batch completed: {entityCount} entities in {batchTotalTime:F1}ms " +
                     $"({batchTotalTime / entityCount:F1}ms per entity)");
        }

        void SpawnSingleRequest()
        {
            if (GridManager.Instance == null) return;

            var grid = GridManager.Instance;

            int2 start = GenerateRandomWalkablePosition(grid);
            int2 target = GenerateRandomWalkablePosition(grid);

            var entity = entityManager.CreateEntity();
            entityManager.AddComponent<PathfindingRequest>(entity);
            entityManager.AddBuffer<PathResult>(entity);

            entityManager.SetComponentData(entity, new PathfindingRequest
            {
                startPosition = start,
                targetPosition = target,
                isProcessing = false,
                hasResult = false
            });

            Debug.Log($"Pathfinding request: {start} → {target}");
        }

        int2 GenerateRandomWalkablePosition(GridManager grid)
        {
            int attempts = 0;
            int2 position;

            do
            {
                position = new int2(
                    random.NextInt(0, grid.gridSize.x),
                    random.NextInt(0, grid.gridSize.y)
                );
                attempts++;
            }
            while (!grid.IsWalkable(position) && attempts < 100);

            return position;
        }

        public void ClearAllRequests()
        {
            var query = entityManager.CreateEntityQuery(typeof(PathfindingRequest));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            Debug.Log($"Clearing {entities.Length} pathfinding requests...");

            entityManager.DestroyEntity(entities);
            entities.Dispose();
            query.Dispose();
        }
    }
}