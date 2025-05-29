using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct PathfindingSystem : ISystem
{
    private EntityQuery pathfindingQuery;
    private EntityQuery gridQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        pathfindingQuery = SystemAPI.QueryBuilder()
            .WithAll<PathfindingRequest, PathBuffer>()
            .WithNone<PathfindingComplete>()
            .Build();

        gridQuery = SystemAPI.QueryBuilder()
            .WithAll<GridData>()
            .Build();

        state.RequireForUpdate(pathfindingQuery);
        state.RequireForUpdate(gridQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gridData = SystemAPI.GetSingleton<GridData>();

        var pathfindingJob = new PathfindingJob
        {
            gridData = gridData,
            pathfindingRequestHandle = SystemAPI.GetComponentTypeHandle<PathfindingRequest>(),
            pathBufferHandle = SystemAPI.GetBufferTypeHandle<PathBuffer>(),
            pathfindingCompleteHandle = SystemAPI.GetComponentTypeHandle<PathfindingComplete>(),
            entityHandle = SystemAPI.GetEntityTypeHandle()
        };

        state.Dependency = pathfindingJob.ScheduleParallel(pathfindingQuery, state.Dependency);
    }
}