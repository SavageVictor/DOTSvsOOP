using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;

namespace DOTS_ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PathfindingSystem : ISystem
    {
        private EntityQuery pendingQuery;
        private EntityQuery processingQuery;
        private NativeArray<bool> walkableGrid;
        private int2 gridSize;
        private int cachedGridVersion;
        private JobHandle previousJobHandle;
        private bool hasActiveJob;
        private NativeArray<Entity> pendingEntities;
        private NativeArray<PathfindingJobResult> pendingResults;
        private NativeArray<PathfindingJobData> pendingRequests;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Query for entities that need pathfinding (not processing and no result)
            pendingQuery = SystemAPI.QueryBuilder()
                .WithAll<PathfindingRequest, PathResult>()
                .WithNone<PathfindingProcessing>()
                .Build();

            // Query for entities currently being processed
            processingQuery = SystemAPI.QueryBuilder()
                .WithAll<PathfindingRequest, PathResult, PathfindingProcessing>()
                .Build();

            cachedGridVersion = -1;
            hasActiveJob = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Complete previous frame's jobs and process results
            if (hasActiveJob)
            {
                if (previousJobHandle.IsCompleted)
                {
                    previousJobHandle.Complete();
                    ProcessCompletedJobs(ref state);
                    hasActiveJob = false;
                }
                else
                {
                    // Job is still running, don't start new jobs yet
                    return;
                }
            }

            // Update grid data only when it changes
            UpdateGridDataIfNeeded();
            if (!walkableGrid.IsCreated) return;

            // Start new pathfinding jobs for pending requests
            StartNewPathfindingJobs(ref state);
        }

        private void ProcessCompletedJobs(ref SystemState state)
        {
            if (processingQuery.IsEmpty) return;

            // Copy results back to entity buffers (do this in system context)
            if (pendingEntities.IsCreated && pendingResults.IsCreated)
            {
                for (int i = 0; i < pendingEntities.Length; i++)
                {
                    var entity = pendingEntities[i];
                    var result = pendingResults[i];

                    // Update the path buffer using SystemAPI
                    var pathBuffer = SystemAPI.GetBuffer<PathResult>(entity);
                    pathBuffer.Clear();

                    for (int j = 0; j < result.pathLength; j++)
                    {
                        pathBuffer.Add(new PathResult { position = result.GetPathPosition(j) });
                    }

                    // Dispose the result
                    result.Dispose();
                }

                // Dispose the arrays
                pendingEntities.Dispose();
                pendingResults.Dispose();

                // Dispose the job requests array now that job is complete
                if (pendingRequests.IsCreated)
                    pendingRequests.Dispose();
            }

            var entities = processingQuery.ToEntityArray(Allocator.TempJob);

            if (entities.Length > 0)
            {
                Debug.Log($"✅ Completed pathfinding for {entities.Length} entities");
            }

            foreach (var entity in entities)
            {
                // Get current request
                var request = SystemAPI.GetComponent<PathfindingRequest>(entity);

                // FIXED: Set the flags in the correct order
                // Mark as having result first
                request.hasResult = true;
                // Mark as NOT processing (this was the bug - it was staying true)
                request.isProcessing = false;

                // Update the component
                SystemAPI.SetComponent(entity, request);

                // Remove processing tag component
                state.EntityManager.RemoveComponent<PathfindingProcessing>(entity);
            }

            entities.Dispose();
        }

        private void StartNewPathfindingJobs(ref SystemState state)
        {
            // Only process entities that don't have results and aren't processing
            var filteredEntities = new NativeList<Entity>(Allocator.TempJob);
            var filteredRequests = new NativeList<PathfindingRequest>(Allocator.TempJob);

            foreach (var (request, entity) in SystemAPI.Query<RefRO<PathfindingRequest>>().WithEntityAccess().WithNone<PathfindingProcessing>())
            {
                // Only process if it doesn't have a result and isn't processing
                if (!request.ValueRO.hasResult && !request.ValueRO.isProcessing)
                {
                    filteredEntities.Add(entity);
                    filteredRequests.Add(request.ValueRO);
                }
            }

            if (filteredEntities.Length == 0)
            {
                filteredEntities.Dispose();
                filteredRequests.Dispose();
                return;
            }

            // Make sure any previous arrays are disposed
            if (pendingEntities.IsCreated) pendingEntities.Dispose();
            if (pendingResults.IsCreated) pendingResults.Dispose();
            if (pendingRequests.IsCreated) pendingRequests.Dispose();

            // Convert to arrays for job processing
            var entities = new NativeArray<Entity>(filteredEntities.AsArray(), Allocator.TempJob);
            var requests = new NativeArray<PathfindingRequest>(filteredRequests.AsArray(), Allocator.TempJob);

            // Create job data arrays
            var jobRequests = new NativeArray<PathfindingJobData>(entities.Length, Allocator.TempJob);
            var jobResults = new NativeArray<PathfindingJobResult>(entities.Length, Allocator.TempJob);

            // Mark entities as processing and prepare job data
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var request = requests[i];

                // Add processing component
                state.EntityManager.AddComponent<PathfindingProcessing>(entity);

                // Update request status - mark as processing but NOT as having result yet
                request.isProcessing = true;
                request.hasResult = false; // Ensure this is false while processing
                SystemAPI.SetComponent(entity, request);

                // Prepare job data
                jobRequests[i] = new PathfindingJobData
                {
                    startPosition = request.startPosition,
                    targetPosition = request.targetPosition,
                    entityIndex = i,
                    shouldProcess = true
                };

                // Initialize result
                jobResults[i] = new PathfindingJobResult(0);
            }

            Debug.Log($"🔄 Starting pathfinding for {entities.Length} new requests");

            // Dispose temporary lists
            filteredEntities.Dispose();
            filteredRequests.Dispose();
            requests.Dispose();

            // Schedule parallel pathfinding job
            var pathfindingJob = new ParallelPathfindingJob
            {
                walkableGrid = walkableGrid,
                gridSize = gridSize,
                requests = jobRequests,
                results = jobResults
            };

            // Schedule the pathfinding job
            var pathfindingHandle = pathfindingJob.Schedule(entities.Length, 1, state.Dependency);

            // Set up dependency and track the job
            state.Dependency = pathfindingHandle;
            previousJobHandle = pathfindingHandle;
            hasActiveJob = true;

            // Store data for later processing (transfer ownership)
            pendingEntities = entities;
            pendingResults = jobResults;
            pendingRequests = jobRequests;
        }

        private void UpdateGridDataIfNeeded()
        {
            if (GridManager.Instance == null) return;

            var grid = GridManager.Instance;

            // Only update if grid version changed
            if (cachedGridVersion == grid.GridVersion && walkableGrid.IsCreated)
                return;

            if (walkableGrid.IsCreated)
                walkableGrid.Dispose();

            gridSize = grid.gridSize;
            walkableGrid = new NativeArray<bool>(gridSize.x * gridSize.y, Allocator.Persistent);

            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    int index = x + y * gridSize.x;
                    walkableGrid[index] = grid.IsWalkable(new int2(x, y));
                }
            }

            cachedGridVersion = grid.GridVersion;
            Debug.Log($"🗺️ Updated grid data (version {cachedGridVersion})");
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (hasActiveJob)
                previousJobHandle.Complete();

            if (pendingEntities.IsCreated)
                pendingEntities.Dispose();
            if (pendingResults.IsCreated)
                pendingResults.Dispose();
            if (pendingRequests.IsCreated)
                pendingRequests.Dispose();

            if (walkableGrid.IsCreated)
                walkableGrid.Dispose();
        }
    } 
}