// Authoring component for grid setup
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class GridAuthoring : MonoBehaviour
{
    public int2 gridSize = new int2(20, 20);
    [Range(0f, 1f)]
    public float obstaclePercentage = 0.2f;
    public bool generateRandomObstacles = true;

    public class Baker : Baker<GridAuthoring>
    {
        public override void Bake(GridAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            CreateGrid(entity, authoring.gridSize, authoring.obstaclePercentage, authoring.generateRandomObstacles);
        }

        private void CreateGrid(Entity entity, int2 gridSize, float obstaclePercentage, bool generateRandomObstacles)
        {
            using var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var gridBlob = ref builder.ConstructRoot<GridBlob>();

            var nodesArray = builder.Allocate(ref gridBlob.nodes, gridSize.x * gridSize.y);

            // Initialize nodes
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    int index = x + y * gridSize.x;
                    nodesArray[index] = new PathNode
                    {
                        position = new int2(x, y),
                        gCost = int.MaxValue,
                        hCost = 0,
                        fCost = int.MaxValue,
                        isWalkable = true,
                        cameFromIndex = -1
                    };
                }
            }

            // Add random obstacles
            if (generateRandomObstacles)
            {
                var random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
                int obstacleCount = (int)(gridSize.x * gridSize.y * obstaclePercentage);

                for (int i = 0; i < obstacleCount; i++)
                {
                    int x = random.NextInt(0, gridSize.x);
                    int y = random.NextInt(0, gridSize.y);
                    int index = x + y * gridSize.x;

                    var node = nodesArray[index];
                    node.isWalkable = false;
                    nodesArray[index] = node;
                }
            }

            var blobAsset = builder.CreateBlobAssetReference<GridBlob>(Unity.Collections.Allocator.Persistent);

            AddComponent(entity, new GridData
            {
                size = gridSize,
                gridBlob = blobAsset
            });
        }
    }
}