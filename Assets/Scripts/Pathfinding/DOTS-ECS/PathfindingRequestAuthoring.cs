using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Authoring component for pathfinding requests
public class PathfindingRequestAuthoring : MonoBehaviour
{
    public int2 startPosition = new int2(0, 0);
    public int2 targetPosition = new int2(10, 10);
    public int2 gridSize = new int2(20, 20);

    public class Baker : Baker<PathfindingRequestAuthoring>
    {
        public override void Bake(PathfindingRequestAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new PathfindingRequest
            {
                startPosition = authoring.startPosition,
                targetPosition = authoring.targetPosition,
                gridSize = authoring.gridSize,
                isProcessing = false
            });

            AddBuffer<PathBuffer>(entity);
            AddComponent<PathfindingComplete>(entity);
            SetComponentEnabled<PathfindingComplete>(entity, false);
        }
    }
}

