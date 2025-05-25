using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine;

namespace DOTSPathfinding
{
    // ===============================
    // COMPONENTS
    // ===============================

    // Component for entities that can pathfind
    public struct PathfindingAgent : IComponentData
    {
        public float speed;
        public int2 targetPosition;
        public bool hasPath;
        public bool isMoving;
    }

    // Current path for an agent
    public struct PathData : IComponentData
    {
        public int currentPathIndex;
        public int pathLength;
    }

    // Buffer to store the actual path points
    [InternalBufferCapacity(64)]
    public struct PathBuffer : IBufferElementData
    {
        public int2 position;
    }

    // Request component for new pathfinding
    public struct PathfindingRequest : IComponentData
    {
        public int2 startPosition;
        public int2 targetPosition;
        public bool isProcessing;
    }

    // Grid data as a singleton component
    public struct GridData : IComponentData
    {
        public int2 gridSize;
        public float nodeSize;
        public float3 gridOrigin;
    }

    // Buffer to store grid nodes
    [InternalBufferCapacity(2500)] // For 50x50 grid
    public struct GridBuffer : IBufferElementData
    {
        public bool isWalkable;
        public float cost;
    }

    // ===============================
    // AUTHORING (MonoBehaviour to ECS conversion)
    // ===============================

    public class PathfindingAgentAuthoring : UnityEngine.MonoBehaviour
    {
        [Header("Agent Settings")]
        public float speed = 5f;
        public Vector2Int targetPosition = new Vector2Int(10, 10);

        class Baker : Baker<PathfindingAgentAuthoring>
        {
            public override void Bake(PathfindingAgentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new PathfindingAgent
                {
                    speed = authoring.speed,
                    targetPosition = new int2(authoring.targetPosition.x, authoring.targetPosition.y),
                    hasPath = false,
                    isMoving = false
                });

                AddComponent<PathData>(entity);
                AddBuffer<PathBuffer>(entity);
                AddComponent<PathfindingRequest>(entity);
            }
        }
    }

    public class GridDataAuthoring : UnityEngine.MonoBehaviour
    {
        [Header("Grid Reference")]
        public GridSystem.Grid gridReference;

        class Baker : Baker<GridDataAuthoring>
        {
            public override void Bake(GridDataAuthoring authoring)
            {
                if (authoring.gridReference == null) return;

                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new GridData
                {
                    gridSize = authoring.gridReference.GridSize,
                    nodeSize = authoring.gridReference.NodeSize,
                    gridOrigin = authoring.gridReference.GridOrigin
                });

                var gridBuffer = AddBuffer<GridBuffer>(entity);

                // We'll populate this at runtime
            }
        }
    }

    // ===============================
    // SYSTEMS
    // ===============================

    // Initialize grid data from MonoBehaviour grid
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct GridInitializationSystem : ISystem
    {
        private bool isInitialized;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            isInitialized = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (isInitialized) return;

            // Find the MonoBehaviour grid in the scene
            var grid = GameObject.FindFirstObjectByType<GridSystem.Grid>();
            if (grid == null || !grid.IsInitialized) return;

            // Find our grid entity - use GridBuffer as the buffer element type
            var query = SystemAPI.QueryBuilder().WithAll<GridData, GridBuffer>().Build();
            var entities = query.ToEntityArray(Allocator.Temp);

            if (entities.Length > 0)
            {
                var entity = entities[0];
                var gridData = SystemAPI.GetComponentRW<GridData>(entity);
                var gridBuffer = SystemAPI.GetBuffer<GridBuffer>(entity);

                // Get grid data from MonoBehaviour
                var gridNodes = grid.GetGridData(Allocator.Temp);

                // Clear and resize buffer
                gridBuffer.Clear();
                gridBuffer.Capacity = gridNodes.Length;

                // Copy data
                for (int i = 0; i < gridNodes.Length; i++)
                {
                    gridBuffer.Add(new GridBuffer
                    {
                        isWalkable = gridNodes[i].isWalkable,
                        cost = gridNodes[i].cost
                    });
                }

                gridNodes.Dispose();
                entities.Dispose();
                isInitialized = true;
                Debug.Log($"Grid initialized in DOTS: {gridData.ValueRO.gridSize.x}x{gridData.ValueRO.gridSize.y}");
            }
            else
            {
                entities.Dispose();
            }
        }
    }

    // System to handle pathfinding requests
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct PathfindingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GridData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gridEntity = SystemAPI.GetSingletonEntity<GridData>();
            var gridData = SystemAPI.GetComponent<GridData>(gridEntity);
            var gridBuffer = SystemAPI.GetBuffer<GridBuffer>(gridEntity);

            foreach (var (agent, pathData, pathBuffer, request, entity) in
                SystemAPI.Query<RefRW<PathfindingAgent>, RefRW<PathData>, DynamicBuffer<PathBuffer>, RefRW<PathfindingRequest>>()
                .WithEntityAccess())
            {
                // Skip if already processing or no request
                if (request.ValueRO.isProcessing || agent.ValueRO.hasPath) continue;

                // Start pathfinding
                request.ValueRW.isProcessing = true;

                // Get current position from transform
                var transform = SystemAPI.GetComponent<LocalTransform>(entity);
                int2 startPos = WorldToGrid(transform.Position, gridData);
                int2 targetPos = agent.ValueRO.targetPosition;

                // Perform A* pathfinding
                var path = FindPath(startPos, targetPos, gridData, gridBuffer, Allocator.Temp);

                // Update components with result
                pathBuffer.Clear();
                if (path.IsCreated && path.Length > 0)
                {
                    for (int i = 0; i < path.Length; i++)
                    {
                        pathBuffer.Add(new PathBuffer { position = path[i] });
                    }

                    pathData.ValueRW.pathLength = path.Length;
                    pathData.ValueRW.currentPathIndex = 0;
                    agent.ValueRW.hasPath = true;
                    agent.ValueRW.isMoving = true;

                    Debug.Log($"Path found with {path.Length} nodes");
                }
                else
                {
                    agent.ValueRW.hasPath = false;
                    Debug.Log("No path found");
                }

                if (path.IsCreated) path.Dispose();
                request.ValueRW.isProcessing = false;
            }
        }

        [BurstCompile]
        private static int2 WorldToGrid(float3 worldPos, GridData gridData)
        {
            float3 localPos = worldPos - gridData.gridOrigin;
            int x = (int)math.floor(localPos.x / gridData.nodeSize);
            int z = (int)math.floor(localPos.z / gridData.nodeSize);
            return new int2(x, z);
        }

        [BurstCompile]
        private static NativeArray<int2> FindPath(int2 start, int2 target, GridData gridData, DynamicBuffer<GridBuffer> grid, Allocator allocator)
        {
            int maxNodes = gridData.gridSize.x * gridData.gridSize.y;

            // Priority queue simulation with simple arrays
            var openSet = new NativeList<int2>(maxNodes, Allocator.Temp);
            var cameFrom = new NativeHashMap<int2, int2>(maxNodes, Allocator.Temp);
            var gScore = new NativeHashMap<int2, float>(maxNodes, Allocator.Temp);
            var fScore = new NativeHashMap<int2, float>(maxNodes, Allocator.Temp);

            openSet.Add(start);
            gScore[start] = 0;
            fScore[start] = Heuristic(start, target);

            while (openSet.Length > 0)
            {
                // Find node with lowest fScore
                int2 current = GetLowestFScore(openSet, fScore);

                if (current.Equals(target))
                {
                    // Reconstruct path
                    var path = ReconstructPath(cameFrom, current, Allocator.Temp);

                    // Cleanup
                    openSet.Dispose();
                    cameFrom.Dispose();
                    gScore.Dispose();
                    fScore.Dispose();

                    // Convert to requested allocator
                    var finalPath = new NativeArray<int2>(path.Length, allocator);
                    finalPath = path.AsArray();
                    path.Dispose();

                    return finalPath;
                }

                // Remove current from open set
                for (int i = 0; i < openSet.Length; i++)
                {
                    if (openSet[i].Equals(current))
                    {
                        openSet.RemoveAt(i);
                        break;
                    }
                }

                // Check neighbors
                var neighbors = GetNeighbors(current, gridData);
                for (int i = 0; i < neighbors.Length; i++)
                {
                    int2 neighbor = neighbors[i];

                    // Skip if not walkable
                    int index = neighbor.y * gridData.gridSize.x + neighbor.x;
                    if (index >= grid.Length || !grid[index].isWalkable) continue;

                    float tentativeGScore = gScore[current] + grid[index].cost;

                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + Heuristic(neighbor, target);

                        if (!ContainsNode(openSet, neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                    }
                }

                neighbors.Dispose();
            }

            // No path found
            openSet.Dispose();
            cameFrom.Dispose();
            gScore.Dispose();
            fScore.Dispose();

            return new NativeArray<int2>(0, allocator);
        }

        [BurstCompile]
        private static float Heuristic(int2 a, int2 b)
        {
            return math.distance(a, b); // Euclidean distance
        }

        [BurstCompile]
        private static int2 GetLowestFScore(NativeList<int2> openSet, NativeHashMap<int2, float> fScore)
        {
            int2 lowest = openSet[0];
            float lowestScore = fScore[lowest];

            for (int i = 1; i < openSet.Length; i++)
            {
                float score = fScore[openSet[i]];
                if (score < lowestScore)
                {
                    lowest = openSet[i];
                    lowestScore = score;
                }
            }

            return lowest;
        }

        [BurstCompile]
        private static bool ContainsNode(NativeList<int2> list, int2 node)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Equals(node)) return true;
            }
            return false;
        }

        [BurstCompile]
        private static NativeArray<int2> GetNeighbors(int2 node, GridData gridData)
        {
            var neighbors = new NativeList<int2>(4, Allocator.Temp);

            // 4-directional movement
            var directions = new NativeArray<int2>(4, Allocator.Temp);
            directions[0] = new int2(0, 1);   // Up
            directions[1] = new int2(0, -1);  // Down
            directions[2] = new int2(-1, 0);  // Left
            directions[3] = new int2(1, 0);   // Right

            for (int i = 0; i < directions.Length; i++)
            {
                int2 neighbor = node + directions[i];
                if (neighbor.x >= 0 && neighbor.x < gridData.gridSize.x &&
                    neighbor.y >= 0 && neighbor.y < gridData.gridSize.y)
                {
                    neighbors.Add(neighbor);
                }
            }

            directions.Dispose();

            var result = new NativeArray<int2>(neighbors.Length, Allocator.Temp);
            for (int i = 0; i < neighbors.Length; i++)
            {
                result[i] = neighbors[i];
            }
            neighbors.Dispose();

            return result;
        }

        [BurstCompile]
        private static NativeList<int2> ReconstructPath(NativeHashMap<int2, int2> cameFrom, int2 current, Allocator allocator)
        {
            var path = new NativeList<int2>(allocator);
            path.Add(current);

            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }

            // Reverse path to go from start to end
            for (int i = 0; i < path.Length / 2; i++)
            {
                int2 temp = path[i];
                path[i] = path[path.Length - 1 - i];
                path[path.Length - 1 - i] = temp;
            }

            return path;
        }
    }

    // System to move agents along their paths
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PathfindingSystem))]
    [BurstCompile]
    public partial struct AgentMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GridData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gridEntity = SystemAPI.GetSingletonEntity<GridData>();
            var gridData = SystemAPI.GetComponent<GridData>(gridEntity);
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, agent, pathData, pathBuffer) in
                SystemAPI.Query<RefRW<LocalTransform>, RefRW<PathfindingAgent>, RefRW<PathData>, DynamicBuffer<PathBuffer>>())
            {
                if (!agent.ValueRO.isMoving || !agent.ValueRO.hasPath || pathData.ValueRO.pathLength == 0) continue;

                // Get current target position
                if (pathData.ValueRO.currentPathIndex >= pathData.ValueRO.pathLength)
                {
                    // Reached end of path
                    agent.ValueRW.isMoving = false;
                    agent.ValueRW.hasPath = false;
                    continue;
                }

                int2 targetGridPos = pathBuffer[pathData.ValueRO.currentPathIndex].position;
                float3 targetWorldPos = GridToWorld(targetGridPos, gridData);

                // Move towards target
                float3 direction = math.normalize(targetWorldPos - transform.ValueRO.Position);
                float3 movement = direction * agent.ValueRO.speed * deltaTime;

                // Check if reached current waypoint
                float distanceToTarget = math.distance(transform.ValueRO.Position, targetWorldPos);
                if (distanceToTarget <= 0.1f)
                {
                    // Move to next waypoint
                    pathData.ValueRW.currentPathIndex++;
                    transform.ValueRW.Position = targetWorldPos;
                }
                else
                {
                    transform.ValueRW.Position += movement;
                }
            }
        }

        [BurstCompile]
        private static float3 GridToWorld(int2 gridPos, GridData gridData)
        {
            float worldX = gridData.gridOrigin.x + (gridPos.x + 0.5f) * gridData.nodeSize;
            float worldZ = gridData.gridOrigin.z + (gridPos.y + 0.5f) * gridData.nodeSize;
            return new float3(worldX, gridData.gridOrigin.y, worldZ);
        }
    }
}