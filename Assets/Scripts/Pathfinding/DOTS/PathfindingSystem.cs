//using Unity.Collections;
//using Unity.Entities;
//using Unity.Jobs;
//using Unity.Mathematics;

//namespace PerformanceComparison.DOTS
//{
//    /// <summary>
//    /// ECS System that processes pathfinding requests for entities with PathfindingAgent components.
//    /// Handles both single and batch pathfinding operations efficiently.
//    /// </summary>
//    public partial class PathfindingSystem : ISystem
//    {
//        private EntityQuery gridQuery;
//        private EntityQuery pathfindingQuery;

//        protected override void OnCreate()
//        {
//            // Create queries for efficient entity filtering
//            gridQuery = GetEntityQuery(typeof(GridData));
//            pathfindingQuery = GetEntityQuery(typeof(PathfindingAgent), typeof(DynamicBuffer<PathNode>));

//            // Require at least one grid entity to exist
//            RequireForUpdate(gridQuery);
//        }

//        protected override void OnUpdate()
//        {
//            // Get grid data (assuming single grid entity exists)
//            if (gridQuery.CalculateEntityCount() == 0)
//                return;

//            var gridEntity = gridQuery.GetSingletonEntity();
//            var gridData = EntityManager.GetComponentData<GridData>(gridEntity);

//            // Process pathfinding requests
//            ProcessSinglePathfindingRequests(gridData);
//            ProcessBatchPathfindingRequests(gridData);
//        }

//        private void ProcessSinglePathfindingRequests(GridData gridData)
//        {
//            var ecb = new EntityCommandBuffer(Allocator.TempJob);
//            var jobHandles = new NativeList<JobHandle>(Allocator.TempJob);

//            Entities
//                .WithAll<PathfindingAgent>()
//                .ForEach((Entity entity, ref PathfindingAgent agent, ref DynamicBuffer<PathNode> pathBuffer) =>
//                {
//                    if (!agent.pathfindingRequested || agent.pathfindingComplete)
//                        return;

//                    // Clear previous path
//                    pathBuffer.Clear();

//                    // Create pathfinding job
//                    var resultPath = new NativeList<int2>(Allocator.TempJob);
//                    var pathfindingJob = new AStarPathfindingJob
//                    {
//                        startPos = agent.startPosition,
//                        targetPos = agent.targetPosition,
//                        gridWidth = gridData.width,
//                        gridHeight = gridData.height,
//                        gridData = gridData.gridBlob,
//                        resultPath = resultPath
//                    };

//                    var jobHandle = pathfindingJob.Schedule();

//                    // Schedule completion job to update entity
//                    var completionJob = new PathfindingCompletionJob
//                    {
//                        entity = entity,
//                        resultPath = resultPath,
//                        ecb = ecb.AsParallelWriter()
//                    };

//                    var completionHandle = completionJob.Schedule(jobHandle);
//                    jobHandles.Add(completionHandle);

//                    // Mark as processing
//                    agent.pathfindingRequested = false;

//                }).WithoutBurst().Run();

//            // Complete all jobs
//            if (jobHandles.Length > 0)
//            {
//                var combinedHandle = JobHandle.CombineDependencies(jobHandles.AsArray());
//                combinedHandle.Complete();
//            }

//            ecb.Playback(EntityManager);
//            ecb.Dispose();
//            jobHandles.Dispose();
//        }

//        private void ProcessBatchPathfindingRequests(GridData gridData)
//        {
//            var pathfindingEntities = pathfindingQuery.ToEntityArray(Allocator.TempJob);
//            var agents = pathfindingQuery.ToComponentDataArray<PathfindingAgent>(Allocator.TempJob);

//            if (pathfindingEntities.Length == 0)
//            {
//                pathfindingEntities.Dispose();
//                agents.Dispose();
//                return;
//            }

//            // Collect batch requests
//            var batchRequests = new NativeList<int>(Allocator.TempJob);
//            for (int i = 0; i < agents.Length; i++)
//            {
//                if (agents[i].pathfindingRequested && !agents[i].pathfindingComplete)
//                {
//                    batchRequests.Add(i);
//                }
//            }

//            if (batchRequests.Length > 1) // Only use batch processing for multiple requests
//            {
//                ProcessBatch(pathfindingEntities, agents, batchRequests, gridData);
//            }

//            batchRequests.Dispose();
//            pathfindingEntities.Dispose();
//            agents.Dispose();
//        }

//        private void ProcessBatch(NativeArray<Entity> entities, NativeArray<PathfindingAgent> agents,
//            NativeList<int> batchIndices, GridData gridData)
//        {
//            var startPositions = new NativeArray<int2>(batchIndices.Length, Allocator.TempJob);
//            var targetPositions = new NativeArray<int2>(batchIndices.Length, Allocator.TempJob);
//            var resultPaths = new NativeArray<NativeList<int2>>(batchIndices.Length, Allocator.TempJob);

//            // Prepare batch data
//            for (int i = 0; i < batchIndices.Length; i++)
//            {
//                var agentIndex = batchIndices[i];
//                startPositions[i] = agents[agentIndex].startPosition;
//                targetPositions[i] = agents[agentIndex].targetPosition;
//            }

//            // Execute batch job
//            var batchJob = new BatchPathfindingJob
//            {
//                startPositions = startPositions,
//                targetPositions = targetPositions,
//                gridWidth = gridData.width,
//                gridHeight = gridData.height,
//                gridData = gridData.gridBlob,
//                resultPaths = resultPaths
//            };

//            batchJob.Run();

//            // Apply results
//            for (int i = 0; i < batchIndices.Length; i++)
//            {
//                var agentIndex = batchIndices[i];
//                var entity = entities[agentIndex];
//                var pathBuffer = EntityManager.GetBuffer<PathNode>(entity);

//                pathBuffer.Clear();
//                var path = resultPaths[i];

//                for (int j = 0; j < path.Length; j++)
//                {
//                    pathBuffer.Add(new PathNode { position = path[j] });
//                }

//                // Update agent component
//                var agent = agents[agentIndex];
//                agent.pathfindingComplete = true;
//                agent.pathfindingRequested = false;
//                agent.pathLength = path.Length;
//                EntityManager.SetComponentData(entity, agent);

//                path.Dispose();
//            }

//            startPositions.Dispose();
//            targetPositions.Dispose();
//            resultPaths.Dispose();
//        }
//    }

//    /// <summary>
//    /// Job for completing pathfinding operations and updating entity data.
//    /// </summary>
//    [Unity.Burst.BurstCompile]
//    public struct PathfindingCompletionJob : IJob
//    {
//        public Entity entity;
//        public NativeList<int2> resultPath;
//        public EntityCommandBuffer.ParallelWriter ecb;

//        public void Execute()
//        {
//            // This job would need to be modified to work with ECB properly
//            // For now, this is a placeholder showing the intended structure

//            // The actual path buffer update should be done on the main thread
//            // or through a different mechanism since ECB doesn't support buffer operations directly

//            resultPath.Dispose();
//        }
//    }

//    /// <summary>
//    /// Utility system for creating pathfinding requests programmatically.
//    /// Useful for testing and benchmarking scenarios.
//    /// </summary>
//    public partial class PathfindingRequestSystem : SystemBase
//    {
//        private EndSimulationEntityCommandBufferSystem ecbSystem;

//        protected override void OnCreate()
//        {
//            ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
//        }

//        protected override void OnUpdate()
//        {
//            // This system can be used to create pathfinding requests
//            // For example, for testing purposes or AI behavior
//        }

//        /// <summary>
//        /// Creates a pathfinding request for an entity.
//        /// </summary>
//        public void RequestPathfinding(Entity entity, int2 start, int2 target)
//        {
//            var agent = EntityManager.GetComponentData<PathfindingAgent>(entity);
//            agent.startPosition = start;
//            agent.targetPosition = target;
//            agent.pathfindingRequested = true;
//            agent.pathfindingComplete = false;
//            EntityManager.SetComponentData(entity, agent);
//        }

//        /// <summary>
//        /// Creates multiple pathfinding agents for testing.
//        /// </summary>
//        public Entity[] CreatePathfindingAgents(int count, int2 gridSize)
//        {
//            var entities = new Entity[count];
//            var ecb = ecbSystem.CreateCommandBuffer();

//            for (int i = 0; i < count; i++)
//            {
//                var entity = ecb.CreateEntity();

//                ecb.AddComponent<PathfindingAgent>(entity, new PathfindingAgent
//                {
//                    startPosition = new int2(0, 0),
//                    targetPosition = new int2(gridSize.x - 1, gridSize.y - 1),
//                    pathfindingRequested = false,
//                    pathfindingComplete = false,
//                    pathLength = 0
//                });

//                ecb.AddBuffer<PathNode>(entity);
//                entities[i] = entity;
//            }

//            return entities;
//        }
//    }
//}