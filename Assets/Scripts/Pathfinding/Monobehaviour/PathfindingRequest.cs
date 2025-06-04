using System.Collections.Generic;
using System;
using Unity.Mathematics;

namespace Mono
{
    [Serializable]
    public class PathfindingRequest
    {
        public int id;
        public int2 startPosition;
        public int2 targetPosition;
        public bool isProcessing;
        public bool hasResult;
        public List<int2> pathPositions = new List<int2>();
        public bool success;
        public float processingTime;
        public bool wasTruncated;

        [NonSerialized]
        public DateTime requestTime;
    } 
}