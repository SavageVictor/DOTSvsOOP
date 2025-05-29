using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

// Component to mark entities that need pathfinding
public struct PathfindingRequest : IComponentData
{
    public int2 startPosition;
    public int2 targetPosition;
    public int2 gridSize;
    public bool isProcessing;
}

// Component to store the calculated path
public struct PathBuffer : IBufferElementData
{
    public int2 position;
}

// Component for grid data (singleton)
public struct GridData : IComponentData
{
    public int2 size;
    public BlobAssetReference<GridBlob> gridBlob;
}

// Blob asset for grid nodes
public struct GridBlob
{
    public BlobArray<PathNode> nodes;
}

// Path node structure
public struct PathNode
{
    public int2 position;
    public int gCost;
    public int hCost;
    public int fCost;
    public bool isWalkable;
    public int cameFromIndex;

    public void CalculateFCost()
    {
        fCost = gCost + hCost;
    }
}

// Component to mark pathfinding as complete
public struct PathfindingComplete : IComponentData, IEnableableComponent
{
    public bool pathFound;
}