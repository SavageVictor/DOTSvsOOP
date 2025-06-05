using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace DOTS_ECS
{
    public class PathfindingSpawner : MonoBehaviour
    {
        public int EntityCount
        {
            get => entityCount;
            set => entityCount = value;
        }


        [Header("Spawn Settings")]
        [SerializeField] private int entityCount = 1000;

        [Header("Controls")]
        [SerializeField] private KeyCode spawnKey = KeyCode.Space;
        [SerializeField] private KeyCode clearKey = KeyCode.C;



        private EntityManager entityManager;
        private Unity.Mathematics.Random random;

        void Start()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
        }

        void Update()
        {
            if (Input.GetKeyDown(spawnKey))
            {
                SpawnAllInstantly();
            }

            if (Input.GetKeyDown(clearKey))
            {
                ClearAllRequests();
            }
        }

        public void SpawnAllInstantly()
        {
            Debug.Log($"Instantly spawning {entityCount} pathfinding requests...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            SpawnBatchOptimized(entityCount);

            stopwatch.Stop();
            Debug.Log($"✅ Spawned {entityCount} entities in {stopwatch.ElapsedMilliseconds}ms");
        }

        // Optimized batch spawning using EntityManager bulk operations
        void SpawnBatchOptimized(int count)
        {
            if (GridManager.Instance == null) return;

            var grid = GridManager.Instance;

            // Pre-generate all positions to avoid repeated grid checks
            var startPositions = new NativeArray<int2>(count, Allocator.Temp);
            var targetPositions = new NativeArray<int2>(count, Allocator.Temp);

            // Generate all positions upfront
            for (int i = 0; i < count; i++)
            {
                startPositions[i] = GenerateRandomWalkablePosition(grid);
                targetPositions[i] = GenerateRandomWalkablePosition(grid);
            }

            // Create archetype for efficient entity creation
            var archetype = entityManager.CreateArchetype(
                typeof(PathfindingRequest),
                typeof(PathResult)
            );

            // Bulk create entities
            var entities = new NativeArray<Entity>(count, Allocator.Temp);
            entityManager.CreateEntity(archetype, entities);

            // Set component data for all entities
            for (int i = 0; i < count; i++)
            {
                entityManager.SetComponentData(entities[i], new PathfindingRequest
                {
                    startPosition = startPositions[i],
                    targetPosition = targetPositions[i],
                    isProcessing = false,
                    hasResult = false
                });

                // Initialize empty path buffer (already added by archetype)
                var pathBuffer = entityManager.GetBuffer<PathResult>(entities[i]);
                pathBuffer.Clear();
            }

            // Cleanup
            entities.Dispose();
            startPositions.Dispose();
            targetPositions.Dispose();
        }

        // Optimized position generation with retry limit and fallback
        int2 GenerateRandomWalkablePosition(GridManager grid)
        {
            const int maxAttempts = 50;
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
            while (!grid.IsWalkable(position) && attempts < maxAttempts);

            // Fallback: if we can't find a walkable position, try corners
            if (!grid.IsWalkable(position))
            {
                var fallbackPositions = new int2[]
                {
                    new int2(0, 0),
                    new int2(grid.gridSize.x - 1, 0),
                    new int2(0, grid.gridSize.y - 1),
                    new int2(grid.gridSize.x - 1, grid.gridSize.y - 1),
                    new int2(grid.gridSize.x / 2, grid.gridSize.y / 2)
                };

                foreach (var fallback in fallbackPositions)
                {
                    if (grid.IsWalkable(fallback))
                    {
                        return fallback;
                    }
                }

                // Last resort: return any position (pathfinding will handle invalid cases)
                Debug.LogWarning("Could not find walkable position, using fallback");
            }

            return position;
        }

        public void ClearAllRequests()
        {
            var query = entityManager.CreateEntityQuery(typeof(PathfindingRequest));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            Debug.Log($"Clearing {entities.Length} pathfinding requests...");

            // Bulk destroy for better performance
            entityManager.DestroyEntity(entities);

            entities.Dispose();
            query.Dispose();

            Debug.Log("✅ All pathfinding requests cleared");
        }

        // Add this method to get current entity count
        public int GetCurrentEntityCount()
        {
            var query = entityManager.CreateEntityQuery(typeof(PathfindingRequest));
            int count = query.CalculateEntityCount();
            query.Dispose();
            return count;
        }
    }
}