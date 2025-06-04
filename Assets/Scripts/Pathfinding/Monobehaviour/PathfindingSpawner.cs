using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Mono
{
    public class PathfindingSpawner : MonoBehaviour
    {
        public int EntityCount
        {
            get => entityCount;
            set => entityCount = value;
        }

        public float SpawnInterval
        {
            get => spawnInterval;
            set => spawnInterval = value;
        }


        [Header("Spawn Settings")]
        [SerializeField] private int entityCount = 10;
        [SerializeField] private float spawnInterval = 1f;
        [SerializeField] private KeyCode spawnKey = KeyCode.Space;
        [SerializeField] private KeyCode clearKey = KeyCode.C;

        private List<int> activeRequestIds = new List<int>();

        void Update()
        {
            if (Input.GetKeyDown(spawnKey))
            {
                StartCoroutine(SpawnPathfindingRequests());
            }

            if (Input.GetKeyDown(clearKey))
            {
                ClearAllRequests();
            }
        }

        public IEnumerator SpawnPathfindingRequests()
        {
            Debug.Log($"Spawning {entityCount} pathfinding requests...");

            for (int i = 0; i < entityCount; i++)
            {
                SpawnSingleRequest();
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        void SpawnSingleRequest()
        {
            if (GridManager.Instance == null || PathfindingSystem.Instance == null) return;

            var grid = GridManager.Instance;

            int2 start = GenerateRandomWalkablePosition(grid);
            int2 target = GenerateRandomWalkablePosition(grid);

            int requestId = PathfindingSystem.Instance.RequestPath(start, target);
            activeRequestIds.Add(requestId);

            Debug.Log($"Pathfinding request {requestId}: {start} → {target}");
        }

        int2 GenerateRandomWalkablePosition(GridManager grid)
        {
            int attempts = 0;
            int2 position;

            do
            {
                position = new int2(
                    UnityEngine.Random.Range(0, grid.gridSize.x),
                    UnityEngine.Random.Range(0, grid.gridSize.y)
                );
                attempts++;
            }
            while (!grid.IsWalkable(position) && attempts < 100);

            return position;
        }

        public void ClearAllRequests()
        {
            if (PathfindingSystem.Instance != null)
            {
                PathfindingSystem.Instance.ClearCompletedRequests();
                activeRequestIds.Clear();
                Debug.Log("Cleared all pathfinding requests");
            }
        }
    } 
}