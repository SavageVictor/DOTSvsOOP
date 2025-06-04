using Unity.Entities;
using Unity.Mathematics;

namespace DOTS_ECS
{
    public struct PathfindingRequest : IComponentData
    {
        public int2 startPosition;
        public int2 targetPosition;
        public bool isProcessing;
        public bool hasResult;
    }

    public struct PathResult : IBufferElementData
    {
        public int2 position;
    }

    // ADD THIS NEW COMPONENT:
    public struct PathfindingProcessing : IComponentData
    {
        // Empty tag component to mark entities being processed
    } 
}