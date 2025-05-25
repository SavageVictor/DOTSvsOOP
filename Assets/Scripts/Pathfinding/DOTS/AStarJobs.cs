//using Unity.Burst;
//using Unity.Collections;
//using Unity.Entities;
//using Unity.Jobs;
//using Unity.Mathematics;

//namespace PerformanceComparison.DOTS
//{
//    /// <summary>
//    /// Burst-compiled A* pathfinding job for single pathfinding request.
//    /// Optimized for maximum performance with native collections and burst compilation.
//    /// </summary>
//    [BurstCompile]
//    public partial struct AStarPathfindingJob : IJob
//    {
//        [ReadOnly] public int2 startPos;
//        [ReadOnly] public int2 targetPos;
//        [ReadOnly] public int gridWidth;
//        [ReadOnly] public int gridHeight;
//        [ReadOnly] public BlobAssetReference<GridBlob> gridData;

//        public NativeList<int2> resultPath;

//        public void Execute()
//        {
//            // Clear previous results
//            resultPath.Clear();

//            // Check if start and target are valid and walkable
//            if (!IsWalkable(startPos) || !IsWalkable(targetPos))
//                return;

//            // If start equals target, return single node path
//            if (startPos.x == targetPos.x && startPos.y == targetPos.y)
//            {
//                resultPath.Add(startPos);
//                return;
//            }

//            // Initialize data structures
//            var openSet = new NativeList<AStarNode>(Allocator.Temp);
//            var closedSet = new NativeHashSet<int2>(gridWidth * gridHeight, Allocator.Temp);
//            var cameFrom = new NativeHashMap<int2, int2>(gridWidth * gridHeight, Allocator.Temp);

//            // Create start node
//            var startNode = new AStarNode
//            {
//                position = startPos,
//                gCost = 0,
//                hCost = CalculateHeuristic(startPos, targetPos),
//                parentPosition = new int2(-1, -1),
//                isWalkable = true
//            };
//            startNode.fCost = startNode.gCost + startNode.hCost;

//            openSet.Add(startNode);

//            while (openSet.Length > 0)
//            {
//                // Find node with lowest F cost
//                int currentIndex = GetLowestFCostNodeIndex(openSet);
//                var currentNode = openSet[currentIndex];
//                openSet.RemoveAtSwapBack(currentIndex);
//                closedSet.Add(currentNode.position);

//                // Check if we reached the target
//                if (currentNode.position.x == targetPos.x && currentNode.position.y == targetPos.y)
//                {
//                    ReconstructPath(cameFrom, currentNode.position);
//                    break;
//                }

//                // Explore neighbors
//                ProcessNeighbors(currentNode, openSet, closedSet, cameFrom);
//            }

//            // Cleanup
//            openSet.Dispose();
//            closedSet.Dispose();
//            cameFrom.Dispose();
//        }

//        private int GetLowestFCostNodeIndex(NativeList<AStarNode> openSet)
//        {
//            int currentIndex = 0;
//            for (int i = 1; i < openSet.Length; i++)
//            {
//                if (openSet[i].fCost < openSet[currentIndex].fCost ||
//                    (openSet[i].fCost == openSet[currentIndex].fCost && openSet[i].hCost < openSet[currentIndex].hCost))
//                {
//                    currentIndex = i;
//                }
//            }
//            return currentIndex;
//        }

//        private void ProcessNeighbors(AStarNode currentNode, NativeList<AStarNode> openSet,
//            NativeHashSet<int2> closedSet, NativeHashMap<int2, int2> cameFrom)
//        {
//            var neighbors = GetNeighbors(currentNode.position);
//            for (int i = 0; i < neighbors.Length; i++)
//            {
//                var neighbor = neighbors[i];

//                if (closedSet.Contains(neighbor) || !IsWalkable(neighbor))
//                    continue;

//                float tentativeGCost = currentNode.gCost + CalculateDistance(currentNode.position, neighbor);

//                int existingIndex = FindNodeInOpenSet(openSet, neighbor);

//                if (existingIndex == -1)
//                {
//                    // Add new node to open set
//                    AddNodeToOpenSet(openSet, neighbor, tentativeGCost, cameFrom, currentNode.position);
//                }
//                else if (tentativeGCost < openSet[existingIndex].gCost)
//                {
//                    // Update existing node with better path
//                    UpdateNodeInOpenSet(openSet, existingIndex, tentativeGCost, cameFrom, currentNode.position);
//                }
//            }
//            neighbors.Dispose();
//        }

//        private int FindNodeInOpenSet(NativeList<AStarNode> openSet, int2 position)
//        {
//            for (int j = 0; j < openSet.Length; j++)
//            {
//                if (openSet[j].position.x == position.x && openSet[j].position.y == position.y)
//                {
//                    return j;
//                }
//            }
//            return -1;
//        }

//        private void AddNodeToOpenSet(NativeList<AStarNode> openSet, int2 neighbor, float gCost,
//            NativeHashMap<int2, int2> cameFrom, int2 parentPosition)
//        {
//            var neighborNode = new AStarNode
//            {
//                position = neighbor,
//                gCost = gCost,
//                hCost = CalculateHeuristic(neighbor, targetPos),
//                parentPosition = parentPosition,
//                isWalkable = true
//            };
//            neighborNode.fCost = neighborNode.gCost + neighborNode.hCost;

//            openSet.Add(neighborNode);
//            cameFrom[neighbor] = parentPosition;
//        }

//        private void UpdateNodeInOpenSet(NativeList<AStarNode> openSet, int index, float newGCost,
//            NativeHashMap<int2, int2> cameFrom, int2 newParent)
//        {
//            var existingNode = openSet[index];
//            existingNode.gCost = newGCost;
//            existingNode.fCost = existingNode.gCost + existingNode.hCost;
//            existingNode.parentPosition = newParent;
//            openSet[index] = existingNode;
//            cameFrom[existingNode.position] = newParent;
//        }

//        private void ReconstructPath(NativeHashMap<int2, int2> cameFrom, int2 current)
//        {
//            var path = new NativeList<int2>(Allocator.Temp);
//            path.Add(current);

//            while (cameFrom.ContainsKey(current))
//            {
//                current = cameFrom[current];
//                path.Add(current);
//            }

//            // Reverse the path and copy to result
//            for (int i = path.Length - 1; i >= 0; i--)
//            {
//                resultPath.Add(path[i]);
//            }

//            path.Dispose();
//        }

//        private NativeList<int2> GetNeighbors(int2 position)
//        {
//            var neighbors = new NativeList<int2>(8, Allocator.Temp);

//            for (int x = -1; x <= 1; x++)
//            {
//                for (int y = -1; y <= 1; y++)
//                {
//                    if (x == 0 && y == 0) continue;

//                    int2 neighbor = new int2(position.x + x, position.y + y);

//                    if (neighbor.x >= 0 && neighbor.x < gridWidth &&
//                        neighbor.y >= 0 && neighbor.y < gridHeight)
//                    {
//                        neighbors.Add(neighbor);
//                    }
//                }
//            }

//            return neighbors;
//        }

//        private bool IsWalkable(int2 position)
//        {
//            if (position.x < 0 || position.x >= gridWidth ||
//                position.y < 0 || position.y >= gridHeight)
//                return false;

//            int index = position.y * gridWidth + position.x;
//            return gridData.Value.walkableNodes[index];
//        }

//        private float CalculateHeuristic(int2 from, int2 to)
//        {
//            // Using Manhattan distance as heuristic (admissible for 4-directional movement)
//            // For 8-directional movement, you might want to use Diagonal distance or Euclidean
//            return math.abs(to.x - from.x) + math.abs(to.y - from.y);
//        }

//        private float CalculateDistance(int2 from, int2 to)
//        {
//            int2 diff = to - from;
//            // Diagonal movement costs sqrt(2), orthogonal costs 1
//            return math.sqrt(diff.x * diff.x + diff.y * diff.y);
//        }
//    }

//    /// <summary>
//    /// Parallel pathfinding job for processing multiple pathfinding requests simultaneously.
//    /// Optimized for scenarios with many agents requiring pathfinding at the same time.
//    /// </summary>
//    [BurstCompile]
//    public partial struct ParallelPathfindingJob : IJobParallelFor
//    {
//        [ReadOnly] public NativeArray<int2> startPositions;
//        [ReadOnly] public NativeArray<int2> targetPositions;
//        [ReadOnly] public int gridWidth;
//        [ReadOnly] public int gridHeight;
//        [ReadOnly] public BlobAssetReference<GridBlob> gridData;

//        [NativeDisableParallelForRestriction]
//        public NativeArray<NativeList<int2>> resultPaths;

//        public void Execute(int index)
//        {
//            var pathList = new NativeList<int2>(Allocator.Temp);

//            var job = new AStarPathfindingJob
//            {
//                startPos = startPositions[index],
//                targetPos = targetPositions[index],
//                gridWidth = gridWidth,
//                gridHeight = gridHeight,
//                gridData = gridData,
//                resultPath = pathList
//            };

//            job.Execute();

//            // Copy results to the output array
//            var outputPath = new NativeList<int2>(pathList.Length, Allocator.Persistent);
//            for (int i = 0; i < pathList.Length; i++)
//            {
//                outputPath.Add(pathList[i]);
//            }

//            resultPaths[index] = outputPath;
//            pathList.Dispose();
//        }
//    }

//    /// <summary>
//    /// Job for batch processing pathfinding requests with shared data structures.
//    /// More memory efficient than ParallelPathfindingJob for large numbers of agents.
//    /// </summary>
//    [BurstCompile]
//    public partial struct BatchPathfindingJob : IJob
//    {
//        [ReadOnly] public NativeArray<int2> startPositions;
//        [ReadOnly] public NativeArray<int2> targetPositions;
//        [ReadOnly] public int gridWidth;
//        [ReadOnly] public int gridHeight;
//        [ReadOnly] public BlobAssetReference<GridBlob> gridData;

//        public NativeArray<NativeList<int2>> resultPaths;

//        public void Execute()
//        {
//            for (int i = 0; i < startPositions.Length; i++)
//            {
//                var pathList = new NativeList<int2>(Allocator.Persistent);

//                var job = new AStarPathfindingJob
//                {
//                    startPos = startPositions[i],
//                    targetPos = targetPositions[i],
//                    gridWidth = gridWidth,
//                    gridHeight = gridHeight,
//                    gridData = gridData,
//                    resultPath = pathList
//                };

//                job.Execute();
//                resultPaths[i] = pathList;
//            }
//        }
//    }
//}