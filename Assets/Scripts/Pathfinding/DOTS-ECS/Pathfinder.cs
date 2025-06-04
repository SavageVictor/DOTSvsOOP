using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace DOTS_ECS
{
    [BurstCompile]
    public struct ParallelPathfindingJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<bool> walkableGrid;
        [ReadOnly] public int2 gridSize;
        [ReadOnly] public NativeArray<PathfindingJobData> requests;
        [WriteOnly] public NativeArray<PathfindingJobResult> results;

        public void Execute(int index)
        {
            var request = requests[index];
            if (!request.shouldProcess)
            {
                results[index] = new PathfindingJobResult(0);
                return;
            }

            var path = FindPath(request.startPosition, request.targetPosition);
            results[index] = path;
        }

        [BurstCompile]
        private PathfindingJobResult FindPath(int2 start, int2 target)
        {
            var result = new PathfindingJobResult(0);

            if (!IsValidAndWalkable(start) || !IsValidAndWalkable(target))
                return result;

            // Pre-allocate with reasonable sizes to avoid dynamic allocation
            var openSet = new NativeList<PathNode>(256, Allocator.Temp);
            var closedSet = new NativeHashSet<int>(gridSize.x * gridSize.y, Allocator.Temp);
            var cameFrom = new NativeHashMap<int, int>(gridSize.x * gridSize.y, Allocator.Temp);

            var startNode = new PathNode
            {
                position = start,
                gCost = 0,
                hCost = CalculateDistance(start, target),
                index = GetIndex(start)
            };
            startNode.fCost = startNode.gCost + startNode.hCost;

            openSet.Add(startNode);

            while (openSet.Length > 0)
            {
                int currentNodeIndex = GetLowestFCostIndex(openSet);
                var currentNode = openSet[currentNodeIndex];

                if (currentNode.position.Equals(target))
                {
                    result = ReconstructPath(cameFrom, currentNode.index, start, target);
                    break;
                }

                openSet.RemoveAtSwapBack(currentNodeIndex);
                closedSet.Add(currentNode.index);

                // Check 8 neighbors
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int2 neighborPos = currentNode.position + new int2(dx, dy);
                        if (!IsValidAndWalkable(neighborPos)) continue;

                        int neighborIndex = GetIndex(neighborPos);
                        if (closedSet.Contains(neighborIndex)) continue;

                        int moveCost = (dx == 0 || dy == 0) ? 10 : 14;
                        int tentativeGCost = currentNode.gCost + moveCost;

                        bool inOpenSet = false;
                        int openSetIndex = -1;

                        // Check if neighbor is in open set
                        for (int i = 0; i < openSet.Length; i++)
                        {
                            if (openSet[i].index == neighborIndex)
                            {
                                inOpenSet = true;
                                openSetIndex = i;
                                break;
                            }
                        }

                        if (!inOpenSet || tentativeGCost < openSet[openSetIndex].gCost)
                        {
                            var neighborNode = new PathNode
                            {
                                position = neighborPos,
                                gCost = tentativeGCost,
                                hCost = CalculateDistance(neighborPos, target),
                                index = neighborIndex
                            };
                            neighborNode.fCost = neighborNode.gCost + neighborNode.hCost;

                            cameFrom[neighborIndex] = currentNode.index;

                            if (!inOpenSet)
                            {
                                openSet.Add(neighborNode);
                            }
                            else
                            {
                                openSet[openSetIndex] = neighborNode;
                            }
                        }
                    }
                }
            }

            openSet.Dispose();
            closedSet.Dispose();
            cameFrom.Dispose();

            return result;
        }

        private PathfindingJobResult ReconstructPath(NativeHashMap<int, int> cameFrom, int targetIndex, int2 start, int2 target)
        {
            var result = new PathfindingJobResult(0); // Parameter not used anymore
            var tempPath = new NativeList<int2>(Allocator.Temp);

            int currentIndex = targetIndex;
            tempPath.Add(target);

            while (cameFrom.ContainsKey(currentIndex))
            {
                currentIndex = cameFrom[currentIndex];
                int2 pos = GetPosition(currentIndex);
                tempPath.Add(pos);

                if (pos.Equals(start)) break;
            }

            // Reverse the path
            var reversedPath = new NativeList<int2>(tempPath.Length, Allocator.Temp);
            for (int i = tempPath.Length - 1; i >= 0; i--)
            {
                reversedPath.Add(tempPath[i]);
            }

            result.SetPath(reversedPath);

            tempPath.Dispose();
            reversedPath.Dispose();
            return result;
        }

        [BurstCompile]
        private int GetLowestFCostIndex(NativeList<PathNode> openSet)
        {
            int lowestIndex = 0;
            for (int i = 1; i < openSet.Length; i++)
            {
                if (openSet[i].fCost < openSet[lowestIndex].fCost ||
                    (openSet[i].fCost == openSet[lowestIndex].fCost && openSet[i].hCost < openSet[lowestIndex].hCost))
                {
                    lowestIndex = i;
                }
            }
            return lowestIndex;
        }

        [BurstCompile]
        private int CalculateDistance(int2 a, int2 b)
        {
            int dx = math.abs(a.x - b.x);
            int dy = math.abs(a.y - b.y);
            return dx > dy ? 14 * dy + 10 * (dx - dy) : 14 * dx + 10 * (dy - dx);
        }

        [BurstCompile]
        private bool IsValidAndWalkable(int2 pos)
        {
            if (pos.x < 0 || pos.x >= gridSize.x || pos.y < 0 || pos.y >= gridSize.y)
                return false;
            return walkableGrid[GetIndex(pos)];
        }

        [BurstCompile]
        private int GetIndex(int2 pos)
        {
            return pos.x + pos.y * gridSize.x;
        }

        [BurstCompile]
        private int2 GetPosition(int index)
        {
            return new int2(index % gridSize.x, index / gridSize.x);
        }
    }

    public struct PathfindingJobData
    {
        public int2 startPosition;
        public int2 targetPosition;
        public int entityIndex;
        public bool shouldProcess;
    }

    // Updated PathfindingJobResult in Pathfinder.cs
    public struct PathfindingJobResult
    {
        public int pathLength;
        public bool wasTruncated; // NEW: Track if path was cut off due to capacity

        // Use a larger fixed list for longer paths
        public FixedList4096Bytes<int2> pathPositions; // 4096 ÷ 8 = 512 max coordinates

        public PathfindingJobResult(int unused)
        {
            pathLength = 0;
            wasTruncated = false;
            pathPositions = new FixedList4096Bytes<int2>();
        }

        public void SetPath(NativeList<int2> path)
        {
            int maxCapacity = pathPositions.Capacity;
            pathLength = path.Length;
            wasTruncated = pathLength > maxCapacity;

            pathPositions.Clear();

            // Only store what fits, but track if we had to truncate
            int elementsToStore = math.min(pathLength, maxCapacity);
            for (int i = 0; i < elementsToStore; i++)
            {
                pathPositions.Add(path[i]);
            }

            // If truncated, the pathLength will be > pathPositions.Length
        }

        public int2 GetPathPosition(int index)
        {
            if (index >= 0 && index < pathPositions.Length)
                return pathPositions[index];
            return new int2(-1, -1);
        }

        public bool IsComplete(int2 targetPosition)
        {
            if (wasTruncated || pathPositions.Length == 0) return false;

            // Check if the last stored position matches the target
            var lastPos = pathPositions[pathPositions.Length - 1];
            return lastPos.Equals(targetPosition);
        }

        public void Dispose()
        {
            // FixedList doesn't need disposal
        }
    }

    // Simplified PathNode structure
    [BurstCompile]
    public struct PathNode
    {
        public int2 position;
        public int gCost;
        public int hCost;
        public int fCost;
        public int index;
    } 
}