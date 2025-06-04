using Unity.Mathematics;
using UnityEngine;

namespace Mono
{
    public class GridManager : MonoBehaviour
    {
        [Header("Grid Settings")]
        public int2 gridSize = new int2(20, 20);

        [Header("Obstacles")]
        public bool generateRandomObstacles = true;
        [Range(0f, 0.5f)]
        public float obstaclePercentage = 0.2f;

        private bool[,] walkableGrid;
        private static GridManager instance;
        private int gridVersion = 0;

        public static GridManager Instance => instance;
        public int GridVersion => gridVersion;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
                InitializeGrid();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void InitializeGrid()
        {
            walkableGrid = new bool[gridSize.x, gridSize.y];

            // Initialize all cells as walkable
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    walkableGrid[x, y] = true;
                }
            }

            if (generateRandomObstacles)
            {
                GenerateRandomObstacles();
            }

            gridVersion++;
        }

        void GenerateRandomObstacles()
        {
            int obstacleCount = Mathf.RoundToInt(gridSize.x * gridSize.y * obstaclePercentage);

            for (int i = 0; i < obstacleCount; i++)
            {
                int x = UnityEngine.Random.Range(0, gridSize.x);
                int y = UnityEngine.Random.Range(0, gridSize.y);
                walkableGrid[x, y] = false;
            }
        }

        public bool IsWalkable(int2 gridPos)
        {
            if (gridPos.x < 0 || gridPos.x >= gridSize.x || gridPos.y < 0 || gridPos.y >= gridSize.y)
                return false;

            return walkableGrid[gridPos.x, gridPos.y];
        }

        public void RegenerateGrid()
        {
            InitializeGrid();
        }
    }

    // PathNode for A* algorithm
    public class PathNode
    {
        public int2 position;
        public int gCost;
        public int hCost;
        public int fCost => gCost + hCost;
        public PathNode parent;

        public PathNode(int2 pos)
        {
            position = pos;
        }
    } 
}