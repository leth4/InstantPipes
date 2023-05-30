using System.Collections.Generic;
using UnityEngine;

namespace InstantPipes
{
    [System.Serializable]
    public class PathCreator
    {
        public float Height = 5;
        public float GridRotationY = 0;
        public float Radius = 1;
        public float GridSize = 3;
        public float Chaos = 0;
        public float StraightPathPriority = 10;
        public float NearObstaclesPriority = 0;
        public int MaxIterations = 1000;

        public bool LastPathSuccess = true;

        public List<Vector3> Create(Vector3 startPosition, Vector3 startNormal, Vector3 endPosition, Vector3 endNormal)
        {
            var path = new List<Vector3>();
            var pathStart = startPosition + startNormal.normalized * Height;
            var pathEnd = endPosition + endNormal.normalized * Height;
            var baseDirection = (pathEnd - pathStart).normalized;

            var pathPoints = FindPath(new(pathStart), new(pathEnd), startNormal.normalized);

            path.Add(startPosition);
            path.Add(pathStart);

            LastPathSuccess = true;

            if (pathPoints != null)
            {
                pathPoints.ForEach(pathPoint => path.Add(pathPoint.Position));
            }
            else
            {
                LastPathSuccess = false;
            }

            path.Add(pathEnd);
            path.Add(endPosition);

            return path;
        }

        private List<Point> FindPath(Point start, Point target, Vector3 startNormal)
        {
            var toSearch = new List<Point> { start };
            var visited = new List<Vector3>();
            var priorityFactor = start.GetDistanceTo(target) / 100;

            Dictionary<Vector3, Point> pointDictionary = new();

            int iterations = 0;
            while (toSearch.Count > 0 && iterations < MaxIterations)
            {
                iterations++;

                var current = toSearch[0];
                foreach (var t in toSearch)
                    if (t.F < current.F || t.F == current.F && t.H < current.H) current = t;

                visited.Add(current.Position);
                toSearch.Remove(current);

                if (Vector3.Distance(current.Position, target.Position) <= GridSize * 2)
                {
                    var currentPathPoint = current;
                    var path = new List<Point>();
                    while (currentPathPoint != start)
                    {
                        if (path.Count == 0 || !AreOnSameLine(path[^1].Position, currentPathPoint.Position, currentPathPoint.Connection.Position))
                        {
                            path.Add(currentPathPoint);
                        }
                        currentPathPoint = currentPathPoint.Connection;
                    }
                    path.Reverse();
                    return path;
                }

                var neighborPositions = current.GetNeighbors(GridSize, Radius, Quaternion.AngleAxis(GridRotationY, Vector3.up));
                foreach (var position in neighborPositions)
                {
                    if (!pointDictionary.ContainsKey(position))
                    {
                        pointDictionary.Add(position, new Point(position));
                    }
                    current.Neighbors.Add(pointDictionary[position]);
                }

                foreach (var neighbor in current.Neighbors)
                {
                    if (ContainsVector(visited, neighbor.Position)) continue;

                    var costToNeighbor = current.G + GridSize;

                    if (current.Connection != null && (current.Connection.Position - current.Position).normalized != (current.Connection.Position - neighbor.Position).normalized)
                    {
                        costToNeighbor += StraightPathPriority * priorityFactor;
                    }

                    costToNeighbor += Random.Range(-Chaos, Chaos) * priorityFactor;

                    if (NearObstaclesPriority != 0)
                    {
                        costToNeighbor += NearObstaclesPriority * neighbor.GetDistanceToNearestObstacle() * priorityFactor / 10;
                    }


                    if (!toSearch.Contains(neighbor) || costToNeighbor < neighbor.G)
                    {
                        neighbor.G = costToNeighbor;
                        neighbor.Connection = current;

                        if (!toSearch.Contains(neighbor))
                        {
                            neighbor.H = neighbor.GetDistanceTo(target);
                            toSearch.Add(neighbor);
                        }
                    }
                }
            }

            return null;
        }

        private bool AreOnSameLine(Vector3 point1, Vector3 point2, Vector3 point3)
        {
            return Vector3.Cross(point2 - point1, point3 - point1).sqrMagnitude < 0.0001f;
        }

        private bool ContainsVector(List<Vector3> list, Vector3 vector)
        {
            foreach (var pos in list)
            {
                if ((vector - pos).sqrMagnitude < 0.0001f) return true;
            }
            return false;
        }

        private class Point
        {
            public Vector3 Position;
            public List<Point> Neighbors;
            public Point Connection;

            public float G;
            public float H;
            public float F => G + H;

            private readonly Vector3[] _directions = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

            public Point(Vector3 position)
            {
                Position = position;
                Neighbors = new();
            }

            public float GetDistanceTo(Point other)
            {
                var distance = Vector3.Distance(other.Position, Position);
                return distance * 10;
            }

            public float GetDistanceToNearestObstacle()
            {
                float minDistance = 200;

                foreach (var direction in _directions)
                    if (Physics.Raycast(Position, direction, out RaycastHit hitPoint, 200))
                        minDistance = Mathf.Min(minDistance, hitPoint.distance);

                return minDistance;
            }

            public List<Vector3> GetNeighbors(float gridSize, float radius, Quaternion rotation)
            {
                var list = new List<Vector3>();

                foreach (var direction in _directions)
                {
                    var rotatedDirection = rotation * direction;
                    if (!Physics.SphereCast(Position, radius, rotatedDirection, out RaycastHit hit, gridSize + radius))
                        list.Add(Position + rotatedDirection * gridSize);
                }

                return list;
            }
        }
    }
}