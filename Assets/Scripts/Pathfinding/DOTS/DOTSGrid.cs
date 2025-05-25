//using System.Collections.Generic;
//using Unity.Collections;
//using Unity.Entities;
//using UnityEngine;

//namespace PerformanceComparison.DOTS
//{
//    /// <summary>
//    /// DOTS-optimized grid implementation that can be converted to blob assets
//    /// for efficient memory access in jobs and systems.
//    /// </summary>
//    public class DOTSGrid : IGrid
//    {
//        public int Width { get; private set; }
//        public int Height { get; private set; }

//        private bool[,] walkableGrid;
//        private float[,] costGrid;

//        public DOTSGrid(int width, int height)
//        {
//            Width = width;
//            Height = height;
//            walkableGrid = new bool[width, height];
//            costGrid = new float[width, height];

//            // Initialize all nodes as walkable with default cost
//            for (int x = 0; x < width; x++)
//            {
//                for (int y = 0; y < height; y++)
//                {
//                    walkableGrid[x, y] = true;
//                    costGrid[x, y] = 1.0f;
//                }
//            }
//        }

//        public bool IsWalkable(int x, int y)
//        {
//            if (x < 0 || x >= Width || y < 0 || y >= Height)
//                return false;
//            return walkableGrid[x, y];
//        }

//        public bool IsWalkable(Vector2Int position) => IsWalkable(position.x, position.y);

//        public float GetCost(Vector2Int from, Vector2Int to)
//        {
//            if (!IsWalkable(to.x, to.y)) return float.MaxValue;

//            // Calculate movement cost (diagonal movement costs more)
//            float baseCost = costGrid[to.x, to.y];
//            bool isDiagonal = from.x != to.x && from.y != to.y;
//            return baseCost * (isDiagonal ? 1.414f : 1.0f);
//        }

//        public Vector2Int[] GetNeighbors(Vector2Int position)
//        {
//            var neighbors = new List<Vector2Int>();

//            for (int x = -1; x <= 1; x++)
//            {
//                for (int y = -1; y <= 1; y++)
//                {
//                    if (x == 0 && y == 0) continue;

//                    int checkX = position.x + x;
//                    int checkY = position.y + y;

//                    if (IsWalkable(checkX, checkY))
//                    {
//                        neighbors.Add(new Vector2Int(checkX, checkY));
//                    }
//                }
//            }

//            return neighbors.ToArray();
//        }

//        public void SetWalkable(int x, int y, bool walkable)
//        {
//            if (x >= 0 && x < Width && y >= 0 && y < Height)
//            {
//                walkableGrid[x, y] = walkable;
//            }
//        }

//        public void SetCost(int x, int y, float cost)
//        {
//            if (x >= 0 && x < Width && y >= 0 && y < Height)
//            {
//                costGrid[x, y] = cost;
//            }
//        }

//        /// <summary>
//        /// Creates a blob asset reference for efficient use in DOTS jobs and systems.
//        /// The blob asset stores grid data in a format optimized for burst-compiled code.
//        /// </summary>
//        public BlobAssetReference<GridBlob> CreateBlobAsset()
//        {
//            using (var builder = new BlobBuilder(Allocator.Temp))
//            {
//                ref var gridBlob = ref builder.ConstructRoot<GridBlob>();

//                var walkableArray = builder.Allocate(ref gridBlob.walkableNodes, Width * Height);
//                var costArray = builder.Allocate(ref gridBlob.costs, Width * Height);

//                for (int x = 0; x < Width; x++)
//                {
//                    for (int y = 0; y < Height; y++)
//                    {
//                        int index = y * Width + x;
//                        walkableArray[index] = walkableGrid[x, y];
//                        costArray[index] = costGrid[x, y];
//                    }
//                }

//                return builder.CreateBlobAssetReference<GridBlob>(Allocator.Persistent);
//            }
//        }

//        /// <summary>
//        /// Creates a DOTSGrid from a generic IGrid interface.
//        /// Useful for converting MonoBehaviour grids to DOTS format.
//        /// </summary>
//        public static DOTSGrid FromGenericGrid(IGrid sourceGrid)
//        {
//            var dotsGrid = new DOTSGrid(sourceGrid.Width, sourceGrid.Height);

//            for (int x = 0; x < sourceGrid.Width; x++)
//            {
//                for (int y = 0; y < sourceGrid.Height; y++)
//                {
//                    dotsGrid.SetWalkable(x, y, sourceGrid.IsWalkable(x, y));

//                    // Extract cost if possible, otherwise use default
//                    var cost = sourceGrid.GetCost(new Vector2Int(x, y), new Vector2Int(x, y));
//                    if (!float.IsInfinity(cost))
//                    {
//                        dotsGrid.SetCost(x, y, cost);
//                    }
//                }
//            }

//            return dotsGrid;
//        }

//        /// <summary>
//        /// Adds obstacles to the grid for testing purposes.
//        /// </summary>
//        public void AddRandomObstacles(float obstacleRatio, int seed = 42)
//        {
//            var random = new System.Random(seed);
//            int obstacleCount = (int)(Width * Height * obstacleRatio);

//            for (int i = 0; i < obstacleCount; i++)
//            {
//                int x = random.Next(0, Width);
//                int y = random.Next(0, Height);
//                SetWalkable(x, y, false);
//            }
//        }
//    }
//}