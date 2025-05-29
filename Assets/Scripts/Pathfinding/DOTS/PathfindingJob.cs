//using System;
//using Unity.Burst;
//using Unity.Collections;
//using Unity.Jobs;
//using Unity.Mathematics;

//[BurstCompile]
//public struct PathfindingJob : IJob
//{

//    public int2 startPosition;
//    public int2 endPosition;
//    public int2 gridSize;

//    [ReadOnly] public bool usePreGeneratedGrid;
//    [ReadOnly] public NativeArray<PathNode> preGeneratedGrid;

//    public NativeList<int2> resultPath;

//    public void Execute()
//    {
//        NativeArray<PathNode> pathNodeArray;
//        bool shouldDisposeGrid = false;

//        if (usePreGeneratedGrid && preGeneratedGrid.IsCreated)
//        {
//            pathNodeArray = preGeneratedGrid;
//        }
//        else
//        {
//            throw new Exception("You need to generate the grid beforehand");
//        }

//        NativeArray<int2> neighbourOffsetArray = PathfindingUtilities.GetNeighbourOffsets(Allocator.Temp);

//        NativeList<int2> foundPath = ExecutePathfinding(pathNodeArray, neighbourOffsetArray);

//        if (resultPath.IsCreated)
//        {
//            resultPath.Clear();
//            for (int i = 0; i < foundPath.Length; i++)
//            {
//                resultPath.Add(foundPath[i]);
//            }
//        }

//        foundPath.Dispose();
//        neighbourOffsetArray.Dispose();
//        if (shouldDisposeGrid)
//        {
//            pathNodeArray.Dispose();
//        }
//    }

//    private NativeList<int2> ExecutePathfinding(NativeArray<PathNode> pathNodeArray,
//        NativeArray<int2> neighbourOffsetArray)
//    {

//        int endNodeIndex = PathfindingUtilities.CalculateIndex(endPosition.x, endPosition.y, gridSize.x);

//        PathNode startNode = pathNodeArray[PathfindingUtilities.CalculateIndex(startPosition.x, startPosition.y, gridSize.x)];
//        startNode.gCost = 0;
//        startNode.CalculateFCost();
//        pathNodeArray[startNode.index] = startNode;

//        NativeList<int> openList = new NativeList<int>(Allocator.Temp);
//        NativeList<int> closedList = new NativeList<int>(Allocator.Temp);

//        openList.Add(startNode.index);

//        while (openList.Length > 0)
//        {
//            int currentNodeIndex = PathfindingUtilities.GetLowestCostFNodeIndex(openList, pathNodeArray);
//            PathNode currentNode = pathNodeArray[currentNodeIndex];

//            if (currentNodeIndex == endNodeIndex)
//            {
//                break;
//            }

//            for (int i = 0; i < openList.Length; i++)
//            {
//                if (openList[i] == currentNodeIndex)
//                {
//                    openList.RemoveAtSwapBack(i);
//                    break;
//                }
//            }

//            closedList.Add(currentNodeIndex);

//            for (int i = 0; i < neighbourOffsetArray.Length; i++)
//            {
//                int2 neighbourOffset = neighbourOffsetArray[i];
//                int2 neighbourPosition = new int2(currentNode.x + neighbourOffset.x, currentNode.y + neighbourOffset.y);

//                if (!PathfindingUtilities.IsPositionInsideGrid(neighbourPosition, gridSize))
//                {
//                    continue;
//                }

//                int neighbourNodeIndex = PathfindingUtilities.CalculateIndex(neighbourPosition.x, neighbourPosition.y, gridSize.x);

//                if (closedList.Contains(neighbourNodeIndex))
//                {
//                    continue;
//                }

//                PathNode neighbourNode = pathNodeArray[neighbourNodeIndex];
//                if (!neighbourNode.isWalkable)
//                {
//                    continue;
//                }

//                int2 currentNodePosition = new int2(currentNode.x, currentNode.y);
//                int tentativeGCost = currentNode.gCost + PathfindingUtilities.CalculateDistanceCost(currentNodePosition, neighbourPosition);

//                if (tentativeGCost < neighbourNode.gCost)
//                {
//                    neighbourNode.cameFromNodeIndex = currentNodeIndex;
//                    neighbourNode.gCost = tentativeGCost;
//                    neighbourNode.CalculateFCost();
//                    pathNodeArray[neighbourNodeIndex] = neighbourNode;

//                    if (!openList.Contains(neighbourNode.index))
//                    {
//                        openList.Add(neighbourNode.index);
//                    }
//                }
//            }
//        }

//        PathNode endNode = pathNodeArray[endNodeIndex];
//        NativeList<int2> path = PathfindingUtilities.CalculatePath(pathNodeArray, endNode);

//        openList.Dispose();
//        closedList.Dispose();

//        return path;
//    }
//}