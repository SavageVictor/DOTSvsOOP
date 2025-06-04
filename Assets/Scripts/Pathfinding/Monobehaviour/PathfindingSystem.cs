using System.Collections.Generic;
using System;
using Unity.Mathematics;
using UnityEngine;

namespace Mono
{
    public class PathfindingSystem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int maxPathLength = 512;
        [SerializeField] private bool enableDebugLogging = false;

        private List<PathfindingRequest> pendingRequests = new List<PathfindingRequest>();
        private List<PathfindingRequest> completedRequests = new List<PathfindingRequest>();

        private int nextRequestId = 0;
        private static PathfindingSystem instance;

        public static PathfindingSystem Instance => instance;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Update()
        {
            ProcessAllPendingRequests();
        }

        public int RequestPath(int2 start, int2 target)
        {
            var request = new PathfindingRequest
            {
                id = nextRequestId++,
                startPosition = start,
                targetPosition = target,
                isProcessing = false,
                hasResult = false,
                requestTime = DateTime.Now
            };

            pendingRequests.Add(request);

            if (enableDebugLogging)
            {
                Debug.Log($"🔄 Path request {request.id}: {start} → {target}");
            }

            return request.id;
        }

        public PathfindingRequest GetPathResult(int requestId)
        {
            return completedRequests.Find(r => r.id == requestId);
        }

        public List<PathfindingRequest> GetAllCompletedRequests()
        {
            return new List<PathfindingRequest>(completedRequests);
        }

        public void ClearCompletedRequests()
        {
            completedRequests.Clear();
        }

        private void ProcessAllPendingRequests()
        {
            // Process ALL pending requests immediately - no throttling
            while (pendingRequests.Count > 0)
            {
                var request = pendingRequests[0];
                pendingRequests.RemoveAt(0);

                ProcessPathfindingRequestImmediate(request);
            }
        }

        private void ProcessPathfindingRequestImmediate(PathfindingRequest request)
        {
            var startTime = System.Diagnostics.Stopwatch.StartNew();

            request.isProcessing = true;

            var path = FindPath(request.startPosition, request.targetPosition);

            request.pathPositions = path;
            request.success = path.Count > 0 && path[path.Count - 1].Equals(request.targetPosition);
            request.wasTruncated = path.Count >= maxPathLength;
            request.processingTime = (float)startTime.Elapsed.TotalMilliseconds;
            request.hasResult = true;
            request.isProcessing = false;

            if (request.wasTruncated)
            {
                request.success = false; // Truncated paths are considered failures
            }

            completedRequests.Add(request);

            if (enableDebugLogging)
            {
                string status = request.success ? "SUCCESS" : (request.wasTruncated ? "TRUNCATED" : "FAILED");
                Debug.Log($"✅ Path {request.id} completed: {status} - Length: {request.pathPositions.Count}, Time: {request.processingTime:F1}ms");
            }
        }

        private List<int2> FindPath(int2 start, int2 target)
        {
            var result = new List<int2>();

            if (GridManager.Instance == null || !GridManager.Instance.IsWalkable(start) || !GridManager.Instance.IsWalkable(target))
                return result;

            var openSet = new List<PathNode>();
            var closedSet = new HashSet<int2>();
            var grid = GridManager.Instance;

            var startNode = new PathNode(start) { gCost = 0, hCost = CalculateDistance(start, target) };
            openSet.Add(startNode);

            int iterations = 0;
            int maxIterations = grid.gridSize.x * grid.gridSize.y; // Prevent infinite loops

            while (openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;

                // Find node with lowest fCost
                var currentNode = openSet[0];
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (openSet[i].fCost < currentNode.fCost ||
                        (openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost))
                    {
                        currentNode = openSet[i];
                    }
                }

                openSet.Remove(currentNode);
                closedSet.Add(currentNode.position);

                // Check if we reached the target
                if (currentNode.position.Equals(target))
                {
                    result = ReconstructPath(currentNode);
                    break;
                }

                // Check neighbors
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int2 neighborPos = currentNode.position + new int2(dx, dy);

                        if (!grid.IsWalkable(neighborPos) || closedSet.Contains(neighborPos))
                            continue;

                        int moveCost = (dx == 0 || dy == 0) ? 10 : 14; // Straight vs diagonal movement
                        int newGCost = currentNode.gCost + moveCost;

                        var neighborNode = openSet.Find(n => n.position.Equals(neighborPos));

                        if (neighborNode == null)
                        {
                            neighborNode = new PathNode(neighborPos)
                            {
                                gCost = newGCost,
                                hCost = CalculateDistance(neighborPos, target),
                                parent = currentNode
                            };
                            openSet.Add(neighborNode);
                        }
                        else if (newGCost < neighborNode.gCost)
                        {
                            neighborNode.gCost = newGCost;
                            neighborNode.parent = currentNode;
                        }
                    }
                }

                // Prevent paths that are too long
                if (result.Count >= maxPathLength)
                {
                    break;
                }
            }

            return result;
        }

        private List<int2> ReconstructPath(PathNode targetNode)
        {
            var path = new List<int2>();
            var currentNode = targetNode;

            while (currentNode != null)
            {
                path.Add(currentNode.position);
                currentNode = currentNode.parent;

                // Safety check to prevent infinite loops
                if (path.Count > maxPathLength)
                    break;
            }

            path.Reverse();
            return path;
        }

        private int CalculateDistance(int2 a, int2 b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return dx > dy ? 14 * dy + 10 * (dx - dy) : 14 * dx + 10 * (dy - dx);
        }
    } 
}