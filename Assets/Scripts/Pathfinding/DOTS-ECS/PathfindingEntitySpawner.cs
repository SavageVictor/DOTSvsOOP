using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Collections;

public class PathfindingEntitySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private int entityCount = 50;
    [SerializeField] private int2 gridSize = new int2(20, 20);
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private float spawnDelay = 0.1f; // Delay between spawns

    [Header("Position Generation")]
    [SerializeField] private bool useRandomPositions = true;
    [SerializeField] private int2 fixedStartPosition = new int2(0, 0);
    [SerializeField] private bool avoidSameStartAndTarget = true;

    [Header("Batch Settings")]
    [SerializeField] private bool spawnInBatches = false;
    [SerializeField] private int batchSize = 10;
    [SerializeField] private float batchInterval = 2f;

    [Header("Controls")]
    [SerializeField] private KeyCode spawnKey = KeyCode.S;
    [SerializeField] private KeyCode clearAllKey = KeyCode.C;

    [Header("Debug")]
    [SerializeField] private bool logSpawnedEntities = true;
    [SerializeField] private bool showGridBounds = true;

    private EntityManager entityManager;
    private Unity.Mathematics.Random random;
    private int spawnedEntityCount = 0;

    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);

        if (spawnOnStart)
        {
            if (spawnInBatches)
                StartCoroutine(SpawnEntitiesInBatches());
            else
                StartCoroutine(SpawnEntitiesWithDelay());
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(spawnKey))
        {
            if (spawnInBatches)
                StartCoroutine(SpawnEntitiesInBatches());
            else
                StartCoroutine(SpawnEntitiesWithDelay());
        }

        if (Input.GetKeyDown(clearAllKey))
        {
            ClearAllPathfindingEntities();
        }
    }

    private IEnumerator SpawnEntitiesWithDelay()
    {
        Debug.Log($"Starting to spawn {entityCount} pathfinding entities...");

        for (int i = 0; i < entityCount; i++)
        {
            SpawnSingleEntity(i);

            if (spawnDelay > 0)
                yield return new WaitForSeconds(spawnDelay);
        }

        Debug.Log($"Finished spawning {entityCount} entities!");
    }

    private IEnumerator SpawnEntitiesInBatches()
    {
        Debug.Log($"Starting to spawn {entityCount} entities in batches of {batchSize}...");

        int remainingEntities = entityCount;
        int batchNumber = 1;

        while (remainingEntities > 0)
        {
            int currentBatchSize = Mathf.Min(batchSize, remainingEntities);

            Debug.Log($"Spawning batch {batchNumber} with {currentBatchSize} entities...");

            for (int i = 0; i < currentBatchSize; i++)
            {
                SpawnSingleEntity(spawnedEntityCount + i);
            }

            spawnedEntityCount += currentBatchSize;
            remainingEntities -= currentBatchSize;
            batchNumber++;

            if (remainingEntities > 0)
                yield return new WaitForSeconds(batchInterval);
        }

        Debug.Log($"Finished spawning all {entityCount} entities in {batchNumber - 1} batches!");
    }

    private void SpawnSingleEntity(int index)
    {
        var entity = entityManager.CreateEntity();

        // Add required components
        entityManager.AddComponent<PathfindingRequest>(entity);
        entityManager.AddBuffer<PathBuffer>(entity);
        entityManager.AddComponent<PathfindingComplete>(entity);

        // Generate positions
        int2 startPos = GenerateStartPosition(index);
        int2 targetPos = GenerateTargetPosition(startPos, index);

        // Set pathfinding request data
        entityManager.SetComponentData(entity, new PathfindingRequest
        {
            startPosition = startPos,
            targetPosition = targetPos,
            gridSize = gridSize,
            isProcessing = false
        });

        // Disable completion component initially
        entityManager.SetComponentEnabled<PathfindingComplete>(entity, false);

        if (logSpawnedEntities)
        {
            Debug.Log($"Entity {index}: Pathfinding from {startPos} to {targetPos}");
        }
    }

    private int2 GenerateStartPosition(int index)
    {
        if (useRandomPositions)
        {
            return new int2(
                random.NextInt(0, gridSize.x),
                random.NextInt(0, gridSize.y)
            );
        }
        else
        {
            return fixedStartPosition;
        }
    }

    private int2 GenerateTargetPosition(int2 startPos, int index)
    {
        int2 targetPos;
        int attempts = 0;
        const int maxAttempts = 100;

        do
        {
            targetPos = new int2(
                random.NextInt(0, gridSize.x),
                random.NextInt(0, gridSize.y)
            );
            attempts++;

            // Prevent infinite loop
            if (attempts >= maxAttempts)
            {
                Debug.LogWarning($"Could not find different target position after {maxAttempts} attempts. Using generated position anyway.");
                break;
            }
        }
        while (avoidSameStartAndTarget && targetPos.x == startPos.x && targetPos.y == startPos.y);

        return targetPos;
    }

    public void SpawnEntitiesImmediate()
    {
        Debug.Log($"Spawning {entityCount} entities immediately...");

        for (int i = 0; i < entityCount; i++)
        {
            SpawnSingleEntity(i);
        }

        Debug.Log($"Spawned {entityCount} entities immediately!");
    }

    public void SpawnEntitiesImmediate(int count)
    {
        Debug.Log($"Spawning {count} entities immediately...");

        for (int i = 0; i < count; i++)
        {
            SpawnSingleEntity(spawnedEntityCount + i);
        }

        spawnedEntityCount += count;
        Debug.Log($"Spawned {count} entities immediately!");
    }

    public void ClearAllPathfindingEntities()
    {
        var query = entityManager.CreateEntityQuery(typeof(PathfindingRequest));
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        Debug.Log($"Clearing {entities.Length} pathfinding entities...");

        entityManager.DestroyEntity(entities);
        entities.Dispose();
        query.Dispose();

        spawnedEntityCount = 0;
        Debug.Log("All pathfinding entities cleared!");
    }

    // Public methods for external control
    public void SetEntityCount(int count) => entityCount = count;
    public void SetGridSize(int2 size) => gridSize = size;
    public void SetSpawnDelay(float delay) => spawnDelay = delay;
    public void SetBatchSize(int size) => batchSize = size;
    public void SetBatchInterval(float interval) => batchInterval = interval;

    // Gizmos for visualization
    void OnDrawGizmos()
    {
        if (!showGridBounds) return;

        // Draw grid bounds
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3(gridSize.x * 0.5f - 0.5f, 0, gridSize.y * 0.5f - 0.5f);
        Vector3 size = new Vector3(gridSize.x, 0.1f, gridSize.y);
        Gizmos.DrawWireCube(center, size);

        // Draw corner markers
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(new Vector3(0, 0, 0), 0.2f); // Bottom-left
        Gizmos.DrawSphere(new Vector3(gridSize.x - 1, 0, gridSize.y - 1), 0.2f); // Top-right

        // Draw fixed start position if not using random
        if (!useRandomPositions)
        {
            Gizmos.color = Color.blue;
            Vector3 startWorldPos = new Vector3(fixedStartPosition.x, 0.2f, fixedStartPosition.y);
            Gizmos.DrawSphere(startWorldPos, 0.3f);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showGridBounds) return;

        // Draw more detailed grid when selected
        Gizmos.color = Color.gray;
        for (int x = 0; x <= gridSize.x; x++)
        {
            Vector3 start = new Vector3(x - 0.5f, 0, -0.5f);
            Vector3 end = new Vector3(x - 0.5f, 0, gridSize.y - 0.5f);
            Gizmos.DrawLine(start, end);
        }

        for (int y = 0; y <= gridSize.y; y++)
        {
            Vector3 start = new Vector3(-0.5f, 0, y - 0.5f);
            Vector3 end = new Vector3(gridSize.x - 0.5f, 0, y - 0.5f);
            Gizmos.DrawLine(start, end);
        }
    }
}