//using Unity.Collections;
//using Unity.Mathematics;
//using UnityEngine;

//public static class GridGenerator
//{

//    public static NativeArray<PathNode> GenerateGrid(int2 gridSize, int2 endPosition, Allocator allocator)
//    {
//        NativeArray<PathNode> pathNodeArray = new NativeArray<PathNode>(gridSize.x * gridSize.y, allocator);

//        for (int x = 0; x < gridSize.x; x++)
//        {
//            for (int y = 0; y < gridSize.y; y++)
//            {
//                PathNode pathNode = new PathNode();
//                pathNode.x = x;
//                pathNode.y = y;
//                pathNode.index = CalculateIndex(x, y, gridSize.x);
//                pathNode.gCost = int.MaxValue;
//                pathNode.hCost = PathfindingUtilities.CalculateDistanceCost(new int2(x, y), endPosition);
//                pathNode.CalculateFCost();
//                pathNode.isWalkable = true;
//                pathNode.cameFromNodeIndex = -1;

//                pathNodeArray[pathNode.index] = pathNode;
//            }
//        }

//        return pathNodeArray;
//    }

//    public static NativeArray<PathNode> GenerateGridWithObstacles(int2 gridSize, int2 endPosition,
//        NativeArray<int2> obstacles, Allocator allocator)
//    {

//        NativeArray<PathNode> pathNodeArray = GenerateGrid(gridSize, endPosition, allocator);

//        // Добавляем препятствия
//        for (int i = 0; i < obstacles.Length; i++)
//        {
//            int2 obstacle = obstacles[i];
//            if (IsPositionInsideGrid(obstacle, gridSize))
//            {
//                int obstacleIndex = CalculateIndex(obstacle.x, obstacle.y, gridSize.x);
//                PathNode obstacleNode = pathNodeArray[obstacleIndex];
//                obstacleNode.SetIsWalkable(false);
//                pathNodeArray[obstacleIndex] = obstacleNode;
//            }
//        }

//        return pathNodeArray;
//    }

//    private static int CalculateIndex(int x, int y, int gridWidth)
//    {
//        return x + y * gridWidth;
//    }

//    private static bool IsPositionInsideGrid(int2 gridPosition, int2 gridSize)
//    {
//        return gridPosition.x >= 0 &&
//               gridPosition.y >= 0 &&
//               gridPosition.x < gridSize.x &&
//               gridPosition.y < gridSize.y;
//    }
//}