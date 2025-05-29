using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Example usage system - shows how to use the pathfinding results
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial class PathfindingResultSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Process completed pathfinding requests
        Entities
            .WithAll<PathfindingComplete>()
            .ForEach((Entity entity, ref PathfindingRequest request, in DynamicBuffer<PathBuffer> pathBuffer, in PathfindingComplete complete) =>
            {
                if (complete.pathFound)
                {
                    Debug.Log($"Path found with {pathBuffer.Length} nodes");

                    // Log the path (for debugging)
                    for (int i = 0; i < pathBuffer.Length; i++)
                    {
                        Debug.Log($"Path node {i}: {pathBuffer[i].position}");
                    }
                }
                else
                {
                    Debug.Log("No path found!");
                }

                // Remove the pathfinding request and completion marker
                EntityManager.RemoveComponent<PathfindingRequest>(entity);
                EntityManager.RemoveComponent<PathfindingComplete>(entity);

            }).WithStructuralChanges().Run();
    }
}

// Utility system to create pathfinding requests at runtime
public partial class PathfindingManagerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Example: Create a pathfinding request every 2 seconds
        if (SystemAPI.Time.ElapsedTime % 2.0 < SystemAPI.Time.DeltaTime)
        {
            CreatePathfindingRequest(new int2(0, 0), new int2(19, 19), new int2(20, 20));
        }
    }

    public void CreatePathfindingRequest(int2 start, int2 target, int2 gridSize)
    {
        var entity = EntityManager.CreateEntity();

        EntityManager.AddComponent<PathfindingRequest>(entity);
        EntityManager.AddBuffer<PathBuffer>(entity);
        EntityManager.AddComponent<PathfindingComplete>(entity);

        EntityManager.SetComponentData(entity, new PathfindingRequest
        {
            startPosition = start,
            targetPosition = target,
            gridSize = gridSize,
            isProcessing = false
        });

        EntityManager.SetComponentEnabled<PathfindingComplete>(entity, false);
    }
}

// MonoBehaviour helper for easy runtime pathfinding requests
public class PathfindingManager : MonoBehaviour
{
    [Header("Pathfinding Settings")]
    public int2 gridSize = new int2(20, 20);

    [Header("Test Path")]
    public int2 startPosition = new int2(0, 0);
    public int2 targetPosition = new int2(19, 19);

    [Header("Controls")]
    public KeyCode findPathKey = KeyCode.Space;

    private EntityManager entityManager;

    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    void Update()
    {
        if (Input.GetKeyDown(findPathKey))
        {
            RequestPathfinding(startPosition, targetPosition);
        }
    }

    public void RequestPathfinding(int2 start, int2 target)
    {
        var entity = entityManager.CreateEntity();

        entityManager.AddComponent<PathfindingRequest>(entity);
        entityManager.AddBuffer<PathBuffer>(entity);
        entityManager.AddComponent<PathfindingComplete>(entity);

        entityManager.SetComponentData(entity, new PathfindingRequest
        {
            startPosition = start,
            targetPosition = target,
            gridSize = gridSize,
            isProcessing = false
        });

        entityManager.SetComponentEnabled<PathfindingComplete>(entity, false);

        Debug.Log($"Pathfinding requested from {start} to {target}");
    }

    // Helper method to create pathfinding requests from code
    public static void RequestPath(int2 start, int2 target, int2 gridSize)
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var entity = entityManager.CreateEntity();

        entityManager.AddComponent<PathfindingRequest>(entity);
        entityManager.AddBuffer<PathBuffer>(entity);
        entityManager.AddComponent<PathfindingComplete>(entity);

        entityManager.SetComponentData(entity, new PathfindingRequest
        {
            startPosition = start,
            targetPosition = target,
            gridSize = gridSize,
            isProcessing = false
        });

        entityManager.SetComponentEnabled<PathfindingComplete>(entity, false);
    }
}

// Example component for units that need pathfinding
public struct UnitPathfinding : IComponentData
{
    public int2 currentTarget;
    public bool needsNewPath;
    public float moveSpeed;
}

// Example system showing how to integrate pathfinding with movement
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class UnitMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        Entities.ForEach((Entity entity, ref UnitPathfinding unitPath, in DynamicBuffer<PathBuffer> pathBuffer) =>
        {
            if (pathBuffer.Length > 0)
            {
                // Simple movement along the path
                // This is just an example - you'd typically use Transform components
                Debug.Log($"Unit at entity {entity.Index} following path with {pathBuffer.Length} nodes");

                // Remove the pathfinding components once path is being followed
                EntityManager.RemoveComponent<PathfindingRequest>(entity);
                EntityManager.RemoveComponent<PathfindingComplete>(entity);
            }
        }).WithStructuralChanges().Run();
    }
}