using System.Collections.Generic;
using UnityEngine;

public static class PathCreator
{
    public static float Height = 5;
    public static float Radius;

    public static float GridSize = 3;
    public static float Chaos = 0;
    public static int StraightPathPriority = 0;
    public static int NearObstaclesPriority = 0;

    public static List<Vector3> Create(Vector3 startPosition, Vector3 startNormal, Vector3 endPosition, Vector3 endNormal)
    {
        var path = new List<Vector3>();
        var pathStart = startPosition + startNormal.normalized * Height;
        var pathEnd = endPosition + endNormal.normalized * Height;
        var baseDirection = (pathEnd - pathStart).normalized;

        Point startPoint = new(pathStart);
        Point endPoint = new(pathEnd);

        var extraPoints = FindPath(endPoint, startPoint);

        path.Add(startPosition);
        path.Add(startPoint.Position);

        if (extraPoints != null)
        {
            for (int i = 1; i < extraPoints.Count - 1; i++)
            {
                path.Add(extraPoints[i].Position);
            }
        }
        else
        {
            Debug.Log($"Can't find a path!");
        }

        RemoveExtra(path);

        path.Add(endPoint.Position);
        path.Add(endPosition);

        return path;
    }

    private static void RemoveExtra(List<Vector3> points)
    {
        for (int i = 0; i < points.Count - 2; i++)
        {
            if ((points[i] - points[i + 2]).normalized == (points[i] - points[i + 1]).normalized)
            {
                points.RemoveAt(i + 1);
                i--;
            }
        }
    }

    private static List<Point> FindPath(Point start, Point target)
    {
        var toSearch = new List<Point> { start };
        var visited = new List<Vector3>();

        int iterations = 0;

        while (toSearch.Count > 0 && iterations < 1000)
        {
            iterations++;
            var current = toSearch[0];
            foreach (var t in toSearch)
                if (t.F < current.F || t.F == current.F && t.H < current.H) current = t;

            if (visited.Contains(current.Position))
            {
                toSearch.Remove(current);
                iterations--;
                continue;
            }

            // Temporary solution

            visited.Add(current.Position);
            toSearch.Remove(current);

            Dictionary<Vector3, Point> pointDictionary = new();

            if (Vector3.Distance(current.Position, target.Position) <= GridSize)
            {
                var currentPathTile = current;
                var path = new List<Point>();
                while (currentPathTile != start)
                {
                    path.Add(currentPathTile);
                    currentPathTile = currentPathTile.Connection;
                }
                return path;
            }

            var neighborPositions = current.GetNeighbors();
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
                if (visited.Contains(neighbor.Position)) continue;

                var costToNeighbor = current.G + 1;
                if (current.Connection != null && (current.Connection.Position - current.Position).normalized == (current.Connection.Position - neighbor.Position).normalized)
                    costToNeighbor -= StraightPathPriority;

                costToNeighbor += UnityEngine.Random.Range(0, Chaos);

                // TEMPORARY! VERY SLOW!

                if (neighbor.GetNeighbors().Count < 6)
                    costToNeighbor -= NearObstaclesPriority;

                if (!toSearch.Contains(neighbor) || costToNeighbor < neighbor.G)
                {
                    neighbor.G = costToNeighbor;
                    neighbor.Connection = current;

                    if (!toSearch.Contains(neighbor))
                    {
                        neighbor.H = neighbor.GetDistance(target);
                        toSearch.Add(neighbor);
                    }
                }
            }
        }

        return null;
    }

}


public class Point
{
    public Vector3 Position;
    public List<Point> Neighbors;
    public Point Connection;

    public float G;
    public float H;
    public float F => G + H;

    public Point(Vector3 position)
    {
        Position = position;
        Neighbors = new();
    }

    public float GetDistance(Point other)
    {
        var distance = Vector3.Distance(other.Position, Position);
        return distance * 10;
    }

    public List<Vector3> GetNeighbors()
    {
        var radiusVector = new Vector3(PathCreator.Radius * 2, PathCreator.Radius * 2, PathCreator.Radius * 2);
        var step = PathCreator.GridSize;
        var list = new List<Vector3>();

        if (!Physics.CheckBox(Position + new Vector3(0, 0, step), radiusVector)) list.Add(Position + new Vector3(0, 0, step));
        if (!Physics.CheckBox(Position + new Vector3(0, 0, -step), radiusVector)) list.Add(Position + new Vector3(0, 0, -step));
        if (!Physics.CheckBox(Position + new Vector3(0, step, 0), radiusVector)) list.Add(Position + new Vector3(0, step, 0));
        if (!Physics.CheckBox(Position + new Vector3(0, -step, 0), radiusVector)) list.Add(Position + new Vector3(0, -step, 0));
        if (!Physics.CheckBox(Position + new Vector3(step, 0, 0), radiusVector)) list.Add(Position + new Vector3(step, 0, 0));
        if (!Physics.CheckBox(Position + new Vector3(-step, 0, 0), radiusVector)) list.Add(Position + new Vector3(-step, 0, 0));

        return list;
    }
}