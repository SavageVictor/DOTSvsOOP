//using Unity.Entities;
//using Unity.Collections;
//using Unity.Mathematics;

//namespace PerformanceComparison.DOTS
//{
//    // DOTS Components (Data only)
//    public struct PathfindingAgent : IComponentData
//    {
//        public int2 startPosition;
//        public int2 targetPosition;
//        public bool pathfindingRequested;
//        public bool pathfindingComplete;
//        public int pathLength;
//    }

//    public struct PathNode : IBufferElementData
//    {
//        public int2 position;
//    }

//    public struct GridData : IComponentData
//    {
//        public int width;
//        public int height;
//        public BlobAssetReference<GridBlob> gridBlob;
//    }

//    public struct GridBlob
//    {
//        public BlobArray<bool> walkableNodes;
//        public BlobArray<float> costs;
//    }

//    // Node structure for A* algorithm - SINGLE DEFINITION
//    public struct AStarNode : System.IComparable<AStarNode>
//    {
//        public int2 position;
//        public float gCost;
//        public float hCost;
//        public float fCost;
//        public int2 parentPosition;
//        public bool isWalkable;

//        public int CompareTo(AStarNode other)
//        {
//            int compare = fCost.CompareTo(other.fCost);
//            if (compare == 0)
//                compare = hCost.CompareTo(other.hCost);
//            return compare;
//        }
//    }
//}