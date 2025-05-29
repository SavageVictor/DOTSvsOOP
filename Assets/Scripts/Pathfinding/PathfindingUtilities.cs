//using Unity.Collections;
//using Unity.Mathematics;

//public static class PathfindingUtilities
//{

//    public const int MOVE_STRAIGHT_COST = 10;
//    public const int MOVE_DIAGONAL_COST = 14;

//    public static int CalculateDistanceCost(int2 aPosition, int2 bPosition)
//    {
//        int xDistance = math.abs(aPosition.x - bPosition.x);
//        int yDistance = math.abs(aPosition.y - bPosition.y);
//        int remaining = math.abs(xDistance - yDistance);
//        return MOVE_DIAGONAL_COST * math.min(xDistance, yDistance) + MOVE_STRAIGHT_COST * remaining;
//    }

//    public static NativeArray<int2> GetNeighbourOffsets(Allocator allocator)
//    {
//        NativeArray<int2> neighbourOffsetArray = new NativeArray<int2>(8, allocator);
//        neighbourOffsetArray[0] = new int2(-1, 0);  // Left
//        neighbourOffsetArray[1] = new int2(+1, 0);  // Right
//        neighbourOffsetArray[2] = new int2(0, +1);  // Up
//        neighbourOffsetArray[3] = new int2(0, -1);  // Down
//        neighbourOffsetArray[4] = new int2(-1, -1); // Left Down
//        neighbourOffsetArray[5] = new int2(-1, +1); // Left Up
//        neighbourOffsetArray[6] = new int2(+1, -1); // Right Down
//        neighbourOffsetArray[7] = new int2(+1, +1); // Right Up
//        return neighbourOffsetArray;
//    }

//    public static int CalculateIndex(int x, int y, int gridWidth)
//    {
//        return x + y * gridWidth;
//    }

//    public static bool IsPositionInsideGrid(int2 gridPosition, int2 gridSize)
//    {
//        return gridPosition.x >= 0 &&
//               gridPosition.y >= 0 &&
//               gridPosition.x < gridSize.x &&
//               gridPosition.y < gridSize.y;
//    }

//    public static int GetLowestCostFNodeIndex(NativeList<int> openList, NativeArray<PathNode> pathNodeArray)
//    {
//        PathNode lowestCostPathNode = pathNodeArray[openList[0]];
//        for (int i = 1; i < openList.Length; i++)
//        {
//            PathNode testPathNode = pathNodeArray[openList[i]];
//            if (testPathNode.fCost < lowestCostPathNode.fCost)
//            {
//                lowestCostPathNode = testPathNode;
//            }
//        }
//        return lowestCostPathNode.index;
//    }

//    public static NativeList<int2> CalculatePath(NativeArray<PathNode> pathNodeArray, PathNode endNode)
//    {
//        if (endNode.cameFromNodeIndex == -1)
//        {
//            return new NativeList<int2>(Allocator.Temp);
//        }

//        NativeList<int2> path = new NativeList<int2>(Allocator.Temp);
//        path.Add(new int2(endNode.x, endNode.y));

//        PathNode currentNode = endNode;
//        while (currentNode.cameFromNodeIndex != -1)
//        {
//            PathNode cameFromNode = pathNodeArray[currentNode.cameFromNodeIndex];
//            path.Add(new int2(cameFromNode.x, cameFromNode.y));
//            currentNode = cameFromNode;
//        }

//        return path;
//    }
//}