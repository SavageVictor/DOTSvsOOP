using Unity.Burst.Intrinsics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public struct PathfindingJob : IJobChunk
{
    [ReadOnly] public GridData gridData;
    public ComponentTypeHandle<PathfindingRequest> pathfindingRequestHandle;
    public BufferTypeHandle<PathBuffer> pathBufferHandle;
    public ComponentTypeHandle<PathfindingComplete> pathfindingCompleteHandle;
    [ReadOnly] public EntityTypeHandle entityHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var pathfindingRequests = chunk.GetNativeArray(ref pathfindingRequestHandle);
        var pathBuffers = chunk.GetBufferAccessor(ref pathBufferHandle);
        var pathfindingCompletes = chunk.GetNativeArray(ref pathfindingCompleteHandle);

        for (int i = 0; i < chunk.Count; i++)
        {
            var request = pathfindingRequests[i];
            var pathBuffer = pathBuffers[i];

            if (request.isProcessing)
                continue;

            // Mark as processing
            request.isProcessing = true;
            pathfindingRequests[i] = request;

            // Execute A* pathfinding
            var path = FindPath(request.startPosition, request.targetPosition, request.gridSize, gridData.gridBlob);

            // Clear existing path and add new one
            pathBuffer.Clear();
            for (int j = 0; j < path.Length; j++)
            {
                pathBuffer.Add(new PathBuffer { position = path[j] });
            }

            // Mark as complete
            pathfindingCompletes[i] = new PathfindingComplete { pathFound = path.Length > 0 };

            path.Dispose();
        }
    }

    private NativeList<int2> FindPath(int2 startPos, int2 targetPos, int2 gridSize, BlobAssetReference<GridBlob> gridBlob)
    {
        var path = new NativeList<int2>(Allocator.Temp);

        if (!IsValidPosition(startPos, gridSize) || !IsValidPosition(targetPos, gridSize))
            return path;

        // Create working copy of nodes
        var nodes = new NativeArray<PathNode>(gridBlob.Value.nodes.Length, Allocator.Temp);
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i] = gridBlob.Value.nodes[i];
        }

        var openSet = new NativeList<int>(Allocator.Temp);
        var closedSet = new NativeHashSet<int>(gridSize.x * gridSize.y, Allocator.Temp);

        int startIndex = GetIndex(startPos, gridSize.x);
        int targetIndex = GetIndex(targetPos, gridSize.x);

        // Initialize start node
        var startNode = nodes[startIndex];
        startNode.gCost = 0;
        startNode.hCost = CalculateHeuristic(startPos, targetPos);
        startNode.CalculateFCost();
        nodes[startIndex] = startNode;

        openSet.Add(startIndex);

        while (openSet.Length > 0)
        {
            int currentIndex = GetLowestFCostNodeIndex(openSet, nodes);

            if (currentIndex == targetIndex)
            {
                // Path found, reconstruct it
                path = ReconstructPath(nodes, targetIndex);
                break;
            }

            // Remove current node from open set
            for (int i = 0; i < openSet.Length; i++)
            {
                if (openSet[i] == currentIndex)
                {
                    openSet.RemoveAtSwapBack(i);
                    break;
                }
            }
            closedSet.Add(currentIndex);

            var currentNode = nodes[currentIndex];

            // Check all neighbors
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int2 neighborPos = currentNode.position + new int2(dx, dy);

                    if (!IsValidPosition(neighborPos, gridSize))
                        continue;

                    int neighborIndex = GetIndex(neighborPos, gridSize.x);

                    if (closedSet.Contains(neighborIndex))
                        continue;

                    var neighborNode = nodes[neighborIndex];

                    if (!neighborNode.isWalkable)
                        continue;

                    int moveCost = (dx == 0 || dy == 0) ? 10 : 14; // Straight vs diagonal
                    int tentativeGCost = currentNode.gCost + moveCost;

                    if (tentativeGCost < neighborNode.gCost)
                    {
                        neighborNode.cameFromIndex = currentIndex;
                        neighborNode.gCost = tentativeGCost;
                        neighborNode.hCost = CalculateHeuristic(neighborPos, targetPos);
                        neighborNode.CalculateFCost();
                        nodes[neighborIndex] = neighborNode;

                        if (!openSet.Contains(neighborIndex))
                        {
                            openSet.Add(neighborIndex);
                        }
                    }
                }
            }
        }

        nodes.Dispose();
        openSet.Dispose();
        closedSet.Dispose();

        return path;
    }

    private int GetLowestFCostNodeIndex(NativeList<int> openSet, NativeArray<PathNode> nodes)
    {
        int lowestIndex = openSet[0];
        int lowestFCost = nodes[lowestIndex].fCost;

        for (int i = 1; i < openSet.Length; i++)
        {
            int nodeIndex = openSet[i];
            int fCost = nodes[nodeIndex].fCost;

            if (fCost < lowestFCost || (fCost == lowestFCost && nodes[nodeIndex].hCost < nodes[lowestIndex].hCost))
            {
                lowestFCost = fCost;
                lowestIndex = nodeIndex;
            }
        }

        return lowestIndex;
    }

    private NativeList<int2> ReconstructPath(NativeArray<PathNode> nodes, int targetIndex)
    {
        var path = new NativeList<int2>(Allocator.Temp);
        int currentIndex = targetIndex;

        while (currentIndex != -1)
        {
            path.Add(nodes[currentIndex].position);
            currentIndex = nodes[currentIndex].cameFromIndex;
        }

        // Reverse path to go from start to target
        for (int i = 0; i < path.Length / 2; i++)
        {
            int2 temp = path[i];
            path[i] = path[path.Length - 1 - i];
            path[path.Length - 1 - i] = temp;
        }

        return path;
    }

    private int CalculateHeuristic(int2 a, int2 b)
    {
        int dx = math.abs(a.x - b.x);
        int dy = math.abs(a.y - b.y);
        return 10 * (dx + dy) + (14 - 2 * 10) * math.min(dx, dy); // Manhattan + diagonal bonus
    }

    private bool IsValidPosition(int2 pos, int2 gridSize)
    {
        return pos.x >= 0 && pos.x < gridSize.x && pos.y >= 0 && pos.y < gridSize.y;
    }

    private int GetIndex(int2 pos, int gridWidth)
    {
        return pos.x + pos.y * gridWidth;
    }
}