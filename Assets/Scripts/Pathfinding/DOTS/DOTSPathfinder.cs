//using System.Collections.Generic;
//using Unity.Burst;
//using Unity.Collections;
//using Unity.Entities;
//using Unity.Jobs;
//using Unity.Mathematics;
//using UnityEngine;

//namespace PerformanceComparison.DOTS
//{
//    // ==========================================
//    // ECS COMPONENTS (Updated for pathfinding requests)
//    // ==========================================

//    public struct PathfindingRequest : IComponentData
//    {
//        public int2 startPosition;
//        public int2 targetPosition;
//        public bool isProcessing;
//        public bool isComplete;
//        public bool hasPath;
//    }

//    public struct PathfindingResult : IBufferElementData
//    {
//        public int2 position;
//    }

//    // ==========================================
//    // ECS JOB ENTITIES (True DOTS approach)
//    // ==========================================

//    /// <summary>
//    /// Job Entity that processes pathfinding requests using ECS queries
//    /// This is the "true DOTS" approach using IJobEntity
//    /// </summary>
//    [BurstCompile]
//    public partial struct PathfindingJob : IJobEntity
//    {
//        [ReadOnly] public BlobAssetReference<GridBlob> gridData;
//        [ReadOnly] public int gridWidth;
//        [ReadOnly] public int gridHeight;

//        // This will be called for each entity that matches the query
//        public void Execute(ref PathfindingRequest request, DynamicBuffer<PathfindingResult> pathBuffer)
//        {
//            if (request.isProcessing || request.isComplete)
//                return;

//            request.isProcessing = true;
//            pathBuffer.Clear();

//            // Execute A* pathfinding
//            var path = new NativeList<int2>(Allocator.Temp);
//            bool foundPath = FindPath(request.startPosition, request.targetPosition, path);

//            // Store results in the buffer
//            if (foundPath)
//            {
//                for (int i = 0; i < path.Length; i++)
//                {
//                    pathBuffer.Add(new PathfindingResult { position = path[i] });
//                }
//                request.hasPath = true;
//            }
//            else
//            {
//                request.hasPath = false;
//            }

//            request.isProcessing = false;
//            request.isComplete = true;
//            path.Dispose();
//        }

//        private bool FindPath(int2 start, int2 goal, NativeList<int2> resultPath)
//        {
//            if (!IsWalkable(start) || !IsWalkable(goal))
//                return false;

//            if (start.x == goal.x && start.y == goal.y)
//            {
//                resultPath.Add(start);
//                return true;
//            }

//            // A* implementation using native collections
//            var openSet = new NativeList<AStarNode>(Allocator.Temp);
//            var closedSet = new NativeHashSet<int2>(gridWidth * gridHeight, Allocator.Temp);
//            var cameFrom = new NativeHashMap<int2, int2>(gridWidth * gridHeight, Allocator.Temp);

//            var startNode = new AStarNode
//            {
//                position = start,
//                gCost = 0,
//                hCost = CalculateHeuristic(start, goal)
//            };
//            startNode.fCost = startNode.gCost + startNode.hCost;

//            openSet.Add(startNode);

//            bool pathFound = false;

//            while (openSet.Length > 0)
//            {
//                int currentIndex = GetLowestFCostIndex(openSet);
//                var currentNode = openSet[currentIndex];
//                openSet.RemoveAtSwapBack(currentIndex);
//                closedSet.Add(currentNode.position);

//                if (currentNode.position.x == goal.x && currentNode.position.y == goal.y)
//                {
//                    ReconstructPath(cameFrom, currentNode.position, resultPath);
//                    pathFound = true;
//                    break;
//                }

//                ProcessNeighbors(currentNode, goal, openSet, closedSet, cameFrom);
//            }

//            openSet.Dispose();
//            closedSet.Dispose();
//            cameFrom.Dispose();

//            return pathFound;
//        }

//        private int GetLowestFCostIndex(NativeList<AStarNode> nodes)
//        {
//            int lowest = 0;
//            for (int i = 1; i < nodes.Length; i++)
//            {
//                if (nodes[i].fCost < nodes[lowest].fCost ||
//                    (nodes[i].fCost == nodes[lowest].fCost && nodes[i].hCost < nodes[lowest].hCost))
//                {
//                    lowest = i;
//                }
//            }
//            return lowest;
//        }

//        private void ProcessNeighbors(AStarNode current, int2 goal, NativeList<AStarNode> openSet,
//            NativeHashSet<int2> closedSet, NativeHashMap<int2, int2> cameFrom)
//        {
//            for (int x = -1; x <= 1; x++)
//            {
//                for (int y = -1; y <= 1; y++)
//                {
//                    if (x == 0 && y == 0) continue;

//                    int2 neighbor = current.position + new int2(x, y);

//                    if (!IsValidPosition(neighbor) || !IsWalkable(neighbor) || closedSet.Contains(neighbor))
//                        continue;

//                    float tentativeGCost = current.gCost + CalculateDistance(current.position, neighbor);

//                    int existingIndex = FindInOpenSet(openSet, neighbor);
//                    if (existingIndex == -1)
//                    {
//                        var neighborNode = new AStarNode
//                        {
//                            position = neighbor,
//                            gCost = tentativeGCost,
//                            hCost = CalculateHeuristic(neighbor, goal)
//                        };
//                        neighborNode.fCost = neighborNode.gCost + neighborNode.hCost;

//                        openSet.Add(neighborNode);
//                        cameFrom[neighbor] = current.position;
//                    }
//                    else if (tentativeGCost < openSet[existingIndex].gCost)
//                    {
//                        var existingNode = openSet[existingIndex];
//                        existingNode.gCost = tentativeGCost;
//                        existingNode.fCost = existingNode.gCost + existingNode.hCost;
//                        openSet[existingIndex] = existingNode;
//                        cameFrom[neighbor] = current.position;
//                    }
//                }
//            }
//        }

//        private int FindInOpenSet(NativeList<AStarNode> openSet, int2 position)
//        {
//            for (int i = 0; i < openSet.Length; i++)
//            {
//                if (openSet[i].position.x == position.x && openSet[i].position.y == position.y)
//                    return i;
//            }
//            return -1;
//        }

//        private void ReconstructPath(NativeHashMap<int2, int2> cameFrom, int2 current, NativeList<int2> path)
//        {
//            var tempPath = new NativeList<int2>(Allocator.Temp);
//            tempPath.Add(current);

//            while (cameFrom.ContainsKey(current))
//            {
//                current = cameFrom[current];
//                tempPath.Add(current);
//            }

//            // Reverse the path
//            for (int i = tempPath.Length - 1; i >= 0; i--)
//            {
//                path.Add(tempPath[i]);
//            }

//            tempPath.Dispose();
//        }

//        private bool IsValidPosition(int2 pos)
//        {
//            return pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;
//        }

//        private bool IsWalkable(int2 pos)
//        {
//            if (!IsValidPosition(pos)) return false;
//            int index = pos.y * gridWidth + pos.x;
//            return gridData.Value.walkableNodes[index];
//        }

//        private float CalculateHeuristic(int2 from, int2 to)
//        {
//            return math.abs(to.x - from.x) + math.abs(to.y - from.y);
//        }

//        private float CalculateDistance(int2 from, int2 to)
//        {
//            int2 diff = to - from;
//            return math.sqrt(diff.x * diff.x + diff.y * diff.y);
//        }
//    }

//    // ==========================================
//    // ECS SYSTEM FOR PATHFINDING
//    // ==========================================

//    [BurstCompile]
//    public partial struct PathfindingSystem : ISystem
//    {
//        private EntityQuery pathfindingQuery;
//        private EntityQuery gridQuery;

//        [BurstCompile]
//        public void OnCreate(ref SystemState state)
//        {
//            // Create query for entities that need pathfinding
//            pathfindingQuery = SystemAPI.QueryBuilder()
//                .WithAll<PathfindingRequest, DynamicBuffer<PathfindingResult>>()
//                .Build();

//            // Create query for grid data
//            gridQuery = SystemAPI.QueryBuilder()
//                .WithAll<GridData>()
//                .Build();

//            // Require grid to exist
//            state.RequireForUpdate(gridQuery);
//        }

//        [BurstCompile]
//        public void OnUpdate(ref SystemState state)
//        {
//            // Get grid data
//            if (gridQuery.CalculateEntityCount() == 0) return;

//            var gridEntity = gridQuery.GetSingletonEntity();
//            var gridData = SystemAPI.GetComponent<GridData>(gridEntity);

//            // Create and schedule the pathfinding job
//            var pathfindingJob = new PathfindingJob
//            {
//                gridData = gridData.gridBlob,
//                gridWidth = gridData.width,
//                gridHeight = gridData.height
//            };

//            // Schedule the job using ECS
//            state.Dependency = pathfindingJob.ScheduleParallel(state.Dependency);
//        }

//        public void OnDestroy(ref SystemState state) { }
//    }

//    // ==========================================
//    // UPDATED DOTS PATHFINDER (ECS Integration)
//    // ==========================================

//    /// <summary>
//    /// DOTS Pathfinder that uses true ECS patterns with systems and job entities
//    /// </summary>
//    public class DOTSECSPathfinder : IPathfinder
//    {
//        public string ImplementationName => "DOTS ECS A* (True ECS)";

//        private EntityManager entityManager;
//        private Entity gridEntity;
//        private DOTSGrid grid;
//        private bool isInitialized;
//        private World world;

//        public void Initialize()
//        {
//            if (isInitialized) return;

//            world = World.DefaultGameObjectInjectionWorld;
//            entityManager = world.EntityManager;

//            // Make sure pathfinding system exists
//            var pathfindingSystem = world.GetOrCreateSystemManaged<PathfindingSystemManaged>();

//            isInitialized = true;
//        }

//        public void Cleanup()
//        {
//            if (!isInitialized) return;

//            if (entityManager != null && entityManager.Exists(gridEntity))
//            {
//                var gridData = entityManager.GetComponentData<GridData>(gridEntity);
//                if (gridData.gridBlob.IsCreated)
//                {
//                    gridData.gridBlob.Dispose();
//                }
//                entityManager.DestroyEntity(gridEntity);
//            }

//            isInitialized = false;
//        }

//        public List<Vector2Int> FindPath(IGrid inputGrid, Vector2Int start, Vector2Int goal)
//        {
//            if (!isInitialized)
//            {
//                throw new System.InvalidOperationException("DOTSECSPathfinder must be initialized before use.");
//            }

//            SetupGrid(inputGrid);

//            // Create a pathfinding request entity
//            var requestEntity = entityManager.CreateEntity();

//            entityManager.AddComponentData(requestEntity, new PathfindingRequest
//            {
//                startPosition = new int2(start.x, start.y),
//                targetPosition = new int2(goal.x, goal.y),
//                isProcessing = false,
//                isComplete = false,
//                hasPath = false
//            });

//            var pathBuffer = entityManager.AddBuffer<PathfindingResult>(requestEntity);

//            // Force system update to process the request
//            var pathfindingSystem = world.GetExistingSystemManaged<PathfindingSystemManaged>();
//            pathfindingSystem?.Update();

//            // Wait for completion (in a real scenario, this would be async)
//            int maxIterations = 100;
//            int iterations = 0;
//            while (iterations < maxIterations)
//            {
//                pathfindingSystem?.Update();
//                var request = entityManager.GetComponentData<PathfindingRequest>(requestEntity);
//                if (request.isComplete)
//                    break;
//                iterations++;
//            }

//            // Extract the result
//            var result = new List<Vector2Int>();
//            var request = entityManager.GetComponentData<PathfindingRequest>(requestEntity);

//            if (request.hasPath)
//            {
//                var buffer = entityManager.GetBuffer<PathfindingResult>(requestEntity);
//                for (int i = 0; i < buffer.Length; i++)
//                {
//                    var pos = buffer[i].position;
//                    result.Add(new Vector2Int(pos.x, pos.y));
//                }
//            }

//            // Cleanup request entity
//            entityManager.DestroyEntity(requestEntity);

//            return result;
//        }

//        /// <summary>
//        /// Finds multiple paths by creating multiple request entities
//        /// This showcases the true power of ECS - processing many entities efficiently
//        /// </summary>
//        public List<Vector2Int>[] FindMultiplePaths(IGrid inputGrid, Vector2Int[] starts, Vector2Int[] goals)
//        {
//            if (!isInitialized)
//            {
//                throw new System.InvalidOperationException("DOTSECSPathfinder must be initialized before use.");
//            }

//            SetupGrid(inputGrid);

//            var requestEntities = new Entity[starts.Length];

//            // Create multiple pathfinding request entities
//            for (int i = 0; i < starts.Length; i++)
//            {
//                var requestEntity = entityManager.CreateEntity();

//                entityManager.AddComponentData(requestEntity, new PathfindingRequest
//                {
//                    startPosition = new int2(starts[i].x, starts[i].y),
//                    targetPosition = new int2(goals[i].x, goals[i].y),
//                    isProcessing = false,
//                    isComplete = false,
//                    hasPath = false
//                });

//                entityManager.AddBuffer<PathfindingResult>(requestEntity);
//                requestEntities[i] = requestEntity;
//            }

//            // Process all requests (ECS will automatically parallelize)
//            var pathfindingSystem = world.GetExistingSystemManaged<PathfindingSystemManaged>();

//            int maxIterations = 100;
//            int iterations = 0;
//            bool allComplete = false;

//            while (iterations < maxIterations && !allComplete)
//            {
//                pathfindingSystem?.Update();

//                allComplete = true;
//                for (int i = 0; i < requestEntities.Length; i++)
//                {
//                    var request = entityManager.GetComponentData<PathfindingRequest>(requestEntities[i]);
//                    if (!request.isComplete)
//                    {
//                        allComplete = false;
//                        break;
//                    }
//                }
//                iterations++;
//            }

//            // Extract results
//            var results = new List<Vector2Int>[starts.Length];
//            for (int i = 0; i < requestEntities.Length; i++)
//            {
//                var result = new List<Vector2Int>();
//                var request = entityManager.GetComponentData<PathfindingRequest>(requestEntities[i]);

//                if (request.hasPath)
//                {
//                    var buffer = entityManager.GetBuffer<PathfindingResult>(requestEntities[i]);
//                    for (int j = 0; j < buffer.Length; j++)
//                    {
//                        var pos = buffer[j].position;
//                        result.Add(new Vector2Int(pos.x, pos.y));
//                    }
//                }

//                results[i] = result;
//                entityManager.DestroyEntity(requestEntities[i]);
//            }

//            return results;
//        }

//        private void SetupGrid(IGrid inputGrid)
//        {
//            if (grid == null || grid.Width != inputGrid.Width || grid.Height != inputGrid.Height)
//            {
//                if (entityManager.Exists(gridEntity))
//                {
//                    var oldGridData = entityManager.GetComponentData<GridData>(gridEntity);
//                    if (oldGridData.gridBlob.IsCreated)
//                    {
//                        oldGridData.gridBlob.Dispose();
//                    }
//                    entityManager.DestroyEntity(gridEntity);
//                }

//                if (inputGrid is DOTSGrid dotsGrid)
//                {
//                    grid = dotsGrid;
//                }
//                else
//                {
//                    grid = DOTSGrid.FromGenericGrid(inputGrid);
//                }

//                CreateGridEntity();
//            }
//        }

//        private void CreateGridEntity()
//        {
//            gridEntity = entityManager.CreateEntity();

//            var gridData = new GridData
//            {
//                width = grid.Width,
//                height = grid.Height,
//                gridBlob = grid.CreateBlobAsset()
//            };

//            entityManager.AddComponentData(gridEntity, gridData);
//        }
//    }

//    // ==========================================
//    // MANAGED SYSTEM WRAPPER (for explicit updates)
//    // ==========================================

//    public partial class PathfindingSystemManaged : SystemBase
//    {
//        protected override void OnUpdate()
//        {
//            // Get grid data
//            var gridQuery = GetEntityQuery(typeof(GridData));
//            if (gridQuery.CalculateEntityCount() == 0) return;

//            var gridEntity = gridQuery.GetSingletonEntity();
//            var gridData = EntityManager.GetComponentData<GridData>(gridEntity);

//            // Create and schedule the pathfinding job
//            var pathfindingJob = new PathfindingJob
//            {
//                gridData = gridData.gridBlob,
//                gridWidth = gridData.width,
//                gridHeight = gridData.height
//            };

//            // Schedule using ECS
//            Dependency = pathfindingJob.ScheduleParallel(Dependency);
//        }
//    }
//}