using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS_ECS
{
    public class PathfindingAuthoring : MonoBehaviour
    {
        [Header("Path Request")]
        public int2 startPosition = new int2(0, 0);
        public int2 targetPosition = new int2(10, 10);

        public class Baker : Baker<PathfindingAuthoring>
        {
            public override void Bake(PathfindingAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new PathfindingRequest
                {
                    startPosition = authoring.startPosition,
                    targetPosition = authoring.targetPosition,
                    isProcessing = false,
                    hasResult = false
                });

                AddBuffer<PathResult>(entity);
            }
        }
    } 
}