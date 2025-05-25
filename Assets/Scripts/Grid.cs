using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GridSystem
{
    // Grid node structure - DOTS friendly (unmanaged data)
    public struct GridNode
    {
        public bool isWalkable;
        public float cost; // Movement cost through this node
        public int2 coordinates; // Grid coordinates

        public GridNode(int2 coords, bool walkable = true, float movementCost = 1f)
        {
            coordinates = coords;
            isWalkable = walkable;
            cost = movementCost;
        }
    }

    // Main grid class
    public class Grid : UnityEngine.MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int2 gridSize = new int2(50, 50);
        [SerializeField] private float nodeSize = 1f;
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;

        [Header("Visualization")]
        [SerializeField] private bool showGrid = true;
        [SerializeField] private bool showWalkable = true;
        [SerializeField] private Color walkableColor = Color.white;
        [SerializeField] private Color unwalkableColor = Color.red;

        // Grid data - using NativeArray for DOTS compatibility
        private NativeArray<GridNode> nodes;
        private bool isInitialized = false;

        public int2 GridSize => gridSize;
        public float NodeSize => nodeSize;
        public Vector3 GridOrigin => gridOrigin;
        public bool IsInitialized => isInitialized;
        public int NodeCount => gridSize.x * gridSize.y;

        private void Awake()
        {
            InitializeGrid();
        }

        private void OnDestroy()
        {
            if (nodes.IsCreated)
                nodes.Dispose();
        }

        public void InitializeGrid()
        {
            if (nodes.IsCreated)
                nodes.Dispose();

            int totalNodes = gridSize.x * gridSize.y;
            nodes = new NativeArray<GridNode>(totalNodes, Allocator.Persistent);

            // Initialize all nodes
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    int index = GetIndex(x, y);
                    nodes[index] = new GridNode(new int2(x, y), true, 1f);
                }
            }

            isInitialized = true;
            Debug.Log($"Grid initialized: {gridSize.x}x{gridSize.y} = {totalNodes} nodes");
        }

        // Convert 2D coordinates to 1D array index
        public int GetIndex(int x, int y)
        {
            return y * gridSize.x + x;
        }

        public int GetIndex(int2 coords)
        {
            return GetIndex(coords.x, coords.y);
        }

        // Convert 1D index back to 2D coordinates
        public int2 GetCoordinates(int index)
        {
            int y = index / gridSize.x;
            int x = index % gridSize.x;
            return new int2(x, y);
        }

        // Check if coordinates are within grid bounds
        public bool IsValidCoordinate(int x, int y)
        {
            return x >= 0 && x < gridSize.x && y >= 0 && y < gridSize.y;
        }

        public bool IsValidCoordinate(int2 coords)
        {
            return IsValidCoordinate(coords.x, coords.y);
        }

        // Get node at specific coordinates
        public GridNode GetNode(int x, int y)
        {
            if (!IsValidCoordinate(x, y))
                throw new System.ArgumentOutOfRangeException("Coordinates out of grid bounds");

            return nodes[GetIndex(x, y)];
        }

        public GridNode GetNode(int2 coords)
        {
            return GetNode(coords.x, coords.y);
        }

        // Set node walkability
        public void SetNodeWalkable(int x, int y, bool walkable)
        {
            if (!IsValidCoordinate(x, y)) return;

            int index = GetIndex(x, y);
            GridNode node = nodes[index];
            node.isWalkable = walkable;
            nodes[index] = node;
        }

        public void SetNodeWalkable(int2 coords, bool walkable)
        {
            SetNodeWalkable(coords.x, coords.y, walkable);
        }

        // Set node cost
        public void SetNodeCost(int x, int y, float cost)
        {
            if (!IsValidCoordinate(x, y)) return;

            int index = GetIndex(x, y);
            GridNode node = nodes[index];
            node.cost = math.max(0.1f, cost); // Ensure cost is never zero or negative
            nodes[index] = node;
        }

        public void SetNodeCost(int2 coords, float cost)
        {
            SetNodeCost(coords.x, coords.y, cost);
        }

        // Convert world position to grid coordinates
        public int2 WorldToGrid(Vector3 worldPosition)
        {
            Vector3 localPos = worldPosition - gridOrigin;
            int x = Mathf.FloorToInt(localPos.x / nodeSize);
            int y = Mathf.FloorToInt(localPos.z / nodeSize); // Using Z for 2D grid in 3D space
            return new int2(x, y);
        }

        // Convert grid coordinates to world position (center of the node)
        public Vector3 GridToWorld(int x, int y)
        {
            float worldX = gridOrigin.x + (x + 0.5f) * nodeSize;
            float worldZ = gridOrigin.z + (y + 0.5f) * nodeSize;
            return new Vector3(worldX, gridOrigin.y, worldZ);
        }

        public Vector3 GridToWorld(int2 coords)
        {
            return GridToWorld(coords.x, coords.y);
        }

        // Get neighbors of a node (4-directional)
        public NativeList<int2> GetNeighbors4(int2 coords, Allocator allocator = Allocator.Temp)
        {
            var neighbors = new NativeList<int2>(4, allocator);

            // Up, Down, Left, Right
            var directions = new int2[]
            {
                new int2(0, 1),   // Up
                new int2(0, -1),  // Down
                new int2(-1, 0),  // Left
                new int2(1, 0)    // Right
            };

            foreach (var dir in directions)
            {
                int2 neighbor = coords + dir;
                if (IsValidCoordinate(neighbor))
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        // Get neighbors of a node (8-directional)
        public NativeList<int2> GetNeighbors8(int2 coords, Allocator allocator = Allocator.Temp)
        {
            var neighbors = new NativeList<int2>(8, allocator);

            // All 8 directions
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue; // Skip the center node

                    int2 neighbor = coords + new int2(x, y);
                    if (IsValidCoordinate(neighbor))
                    {
                        neighbors.Add(neighbor);
                    }
                }
            }

            return neighbors;
        }

        // Get a copy of the grid data for DOTS systems
        public NativeArray<GridNode> GetGridData(Allocator allocator)
        {
            if (!nodes.IsCreated)
                throw new System.InvalidOperationException("Grid not initialized");

            var copy = new NativeArray<GridNode>(nodes.Length, allocator);
            copy.CopyFrom(nodes);
            return copy;
        }

        // Apply grid data from DOTS systems
        public void ApplyGridData(NativeArray<GridNode> gridData)
        {
            if (!nodes.IsCreated || gridData.Length != nodes.Length)
                throw new System.InvalidOperationException("Invalid grid data");

            nodes.CopyFrom(gridData);
        }

        // Utility methods for testing and setup
        [ContextMenu("Clear All Obstacles")]
        public void ClearAllObstacles()
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                GridNode node = nodes[i];
                node.isWalkable = true;
                nodes[i] = node;
            }
        }

        [ContextMenu("Add Random Obstacles")]
        public void AddRandomObstacles()
        {
            int obstacleCount = Mathf.RoundToInt(NodeCount * 0.2f); // 20% obstacles

            for (int i = 0; i < obstacleCount; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, nodes.Length);
                GridNode node = nodes[randomIndex];
                node.isWalkable = false;
                nodes[randomIndex] = node;
            }
        }

        // Visualization
        private void OnDrawGizmos()
        {
            if (!showGrid || !isInitialized || !nodes.IsCreated) return;

            Gizmos.color = Color.gray;

            // Draw grid lines
            for (int x = 0; x <= gridSize.x; x++)
            {
                Vector3 start = GridToWorld(x, 0) - new Vector3(nodeSize * 0.5f, 0, nodeSize * 0.5f);
                Vector3 end = GridToWorld(x, gridSize.y - 1) + new Vector3(-nodeSize * 0.5f, 0, nodeSize * 0.5f);
                Gizmos.DrawLine(start, end);
            }

            for (int y = 0; y <= gridSize.y; y++)
            {
                Vector3 start = GridToWorld(0, y) - new Vector3(nodeSize * 0.5f, 0, nodeSize * 0.5f);
                Vector3 end = GridToWorld(gridSize.x - 1, y) + new Vector3(nodeSize * 0.5f, 0, -nodeSize * 0.5f);
                Gizmos.DrawLine(start, end);
            }

            // Draw node states
            if (showWalkable)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    for (int y = 0; y < gridSize.y; y++)
                    {
                        GridNode node = GetNode(x, y);
                        Vector3 worldPos = GridToWorld(x, y);

                        Gizmos.color = node.isWalkable ? walkableColor : unwalkableColor;

                        if (!node.isWalkable)
                        {
                            Gizmos.DrawCube(worldPos, Vector3.one * nodeSize * 0.8f);
                        }
                        else if (node.cost > 1f)
                        {
                            // Show high-cost areas with different color intensity
                            Color costColor = Color.yellow;
                            costColor.a = Mathf.Clamp01(node.cost / 5f);
                            Gizmos.color = costColor;
                            Gizmos.DrawCube(worldPos, Vector3.one * nodeSize * 0.6f);
                        }
                    }
                }
            }
        }
    }

    // Editor helper for quick grid setup
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(Grid))]
    public class GridEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            Grid grid = (Grid)target;

            GUILayout.Space(10);

            if (GUILayout.Button("Reinitialize Grid"))
            {
                grid.InitializeGrid();
            }

            if (GUILayout.Button("Clear All Obstacles"))
            {
                grid.ClearAllObstacles();
            }

            if (GUILayout.Button("Add Random Obstacles"))
            {
                grid.AddRandomObstacles();
            }

            GUILayout.Space(10);

            if (grid.IsInitialized)
            {
                GUILayout.Label($"Grid Info:");
                GUILayout.Label($"Size: {grid.GridSize.x} x {grid.GridSize.y}");
                GUILayout.Label($"Total Nodes: {grid.NodeCount}");
            }
        }
    }
#endif
}