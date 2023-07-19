using System;
using System.Collections.Generic;
using UnityEngine;

namespace InstantPipes
{
    [System.Serializable]
    public class Pipe
    {
        public List<Vector3> Points;

        private List<Vector3> _verts;
        private List<Vector3> _normals;
        private List<Vector2> _uvs;
        private List<int> _triIndices;
        private List<BezierPoint> _bezierPoints;
        private List<PlaneInfo> _planes;
        private PipeGenerator _generator;

        private float _currentAngleOffset;
        private Quaternion _previousRotation;

        private float _ringThickness => _generator.HasExtrusion ? 0 : _generator.RingThickness;

        public Pipe(List<Vector3> points)
        {
            Points = points;
        }

        public float GetMaxDistanceBetweenPoints()
        {
            var maxDistance = 0f;
            for (int i = 1; i < Points.Count; i++)
            {
                maxDistance = Mathf.Max(maxDistance, Vector3.Distance(Points[i], Points[i - 1]));
            }
            return maxDistance;
        }

        public List<Mesh> GenerateMeshes(PipeGenerator generator)
        {
            var meshes = new List<Mesh>();

            _generator = generator;

            ClearMeshInfo();

            var ringPoints = new List<int>();

            var direction = (Points[0] - Points[1]).normalized;
            var rotation = (direction != Vector3.zero) ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;
            _previousRotation = rotation;
            _bezierPoints.Add(new BezierPoint(Points[0], rotation));

            for (int pipePoint = 1; pipePoint < Points.Count - 1; pipePoint++)
            {
                for (int s = 0; s < _generator.CurvedSegmentCount + 1; s++)
                {
                    _bezierPoints.Add(GetBezierPoint((s / (float)_generator.CurvedSegmentCount), pipePoint));
                    if (s == 0 || s == _generator.CurvedSegmentCount)
                        ringPoints.Add(_bezierPoints.Count - 1);
                }
            }

            _bezierPoints.Add(new BezierPoint(Points[^1], _previousRotation));

            GenerateVertices();
            GenerateUVs();
            GenerateTriangles();

            meshes.Add(new()
            {
                vertices = _verts.ToArray(),
                normals = _normals.ToArray(),
                uv = _uvs.ToArray(),
                triangles = _triIndices.ToArray()
            });
            _verts = new();
            _normals = new();
            _uvs = new();
            _triIndices = new();

            if (_generator.HasRings)
            {
                if (_generator.HasExtrusion)
                {
                    GenerateVertices(isExtruded: true);
                    GenerateUVs();
                    GenerateTriangles(isExtruded: true);

                    for (int i = 1; i < _bezierPoints.Count - 1; i += _generator.CurvedSegmentCount + 1)
                    {
                        _planes.Add(new PlaneInfo(_bezierPoints[i], _generator.RingRadius + _generator.Radius, false));
                        if (i + _generator.CurvedSegmentCount >= _bezierPoints.Count) break;
                        _planes.Add(new PlaneInfo(_bezierPoints[i + _generator.CurvedSegmentCount], _generator.RingRadius + _generator.Radius, true));
                    }
                }
                else
                {
                    foreach (var point in ringPoints) GenerateDisc(_bezierPoints[point]);
                }
            }

            if (_generator.HasCaps)
            {
                GenerateDisc(_bezierPoints[^1]);
                GenerateDisc(_bezierPoints[0]);
            }

            foreach (var plane in _planes) GeneratePlane(plane);

            meshes.Add(new()
            {
                vertices = _verts.ToArray(),
                normals = _normals.ToArray(),
                uv = _uvs.ToArray(),
                triangles = _triIndices.ToArray()
            });

            return meshes;
        }

        private void ClearMeshInfo()
        {
            _verts = new List<Vector3>();
            _normals = new List<Vector3>();
            _uvs = new List<Vector2>();
            _triIndices = new List<int>();
            _bezierPoints = new List<BezierPoint>();
            _planes = new List<PlaneInfo>();
        }

        private BezierPoint GetBezierPoint(float t, int x)
        {
            Vector3 prev, next;

            if ((Points[x] - Points[x - 1]).magnitude > _generator.Curvature * 2 + _ringThickness)
                prev = Points[x] - (Points[x] - Points[x - 1]).normalized * _generator.Curvature;
            else
                prev = (Points[x] + Points[x - 1]) / 2 + (Points[x] - Points[x - 1]).normalized * _ringThickness / 2;

            if ((Points[x] - Points[x + 1]).magnitude > _generator.Curvature * 2 + _ringThickness)
                next = Points[x] - (Points[x] - Points[x + 1]).normalized * _generator.Curvature;
            else
                next = (Points[x] + Points[x + 1]) / 2 + (Points[x] - Points[x + 1]).normalized * _ringThickness / 2;

            if (x == 1)
            {
                if ((Points[x] - Points[x - 1]).magnitude > _generator.Curvature + _ringThickness * 2.5f)
                    prev = Points[x] - (Points[x] - Points[x - 1]).normalized * _generator.Curvature;
                else
                    prev = Points[x - 1] + (Points[x] - Points[x - 1]).normalized * _ringThickness * 2.5f;
            }

            else if (x == Points.Count - 2)
            {
                if ((Points[x] - Points[x + 1]).magnitude > _generator.Curvature + _ringThickness * 2.5f)
                    next = Points[x] - (Points[x] - Points[x + 1]).normalized * _generator.Curvature;
                else
                    next = Points[x + 1] + (Points[x] - Points[x + 1]).normalized * _ringThickness * 2.5f;
            }

            Vector3 a = Vector3.Lerp(prev, Points[x], t);
            Vector3 b = Vector3.Lerp(Points[x], next, t);
            var position = Vector3.Lerp(a, b, t);

            Vector3 aNext = Vector3.LerpUnclamped(prev, Points[x], t + 0.001f);
            Vector3 bNext = Vector3.LerpUnclamped(Points[x], next, t + 0.001f);

            var tangent = Vector3.Cross(a - b, aNext - bNext);
            var rotation = (a != b) ? Quaternion.LookRotation((a - b).normalized, tangent) : Quaternion.identity;

            // Rotate new tangent along the forward axis to match the previous part

            if (t == 0)
            {
                _currentAngleOffset = Quaternion.Angle(_previousRotation, rotation);
                var offsetRotation = Quaternion.AngleAxis(_currentAngleOffset, Vector3.forward);
                if (Quaternion.Angle(rotation * offsetRotation, _previousRotation) > 0)
                    _currentAngleOffset *= -1;
            }
            rotation *= Quaternion.AngleAxis(_currentAngleOffset, Vector3.forward);

            _previousRotation = rotation;
            return new BezierPoint(position, rotation);
        }

        private void GenerateUVs()
        {
            float length = 0;
            for (int i = 1; i < _bezierPoints.Count; i++)
                length += (_bezierPoints[i].Pos - _bezierPoints[i - 1].Pos).magnitude;

            float currentUV = 0;
            for (int i = 0; i < _bezierPoints.Count; i++)
            {
                if (i != 0)
                    currentUV += (_bezierPoints[i].Pos - _bezierPoints[i - 1].Pos).magnitude / length;

                for (int edge = 0; edge < _generator.EdgeCount; edge++)
                {
                    _uvs.Add(new Vector2(edge / (float)_generator.EdgeCount, currentUV * length));
                }
                _uvs.Add(new Vector2(1, currentUV * length));
            }
        }

        private void GenerateVertices(bool isExtruded = false)
        {
            for (int point = 0; point < _bezierPoints.Count; point++)
            {
                for (int i = 0; i < _generator.EdgeCount; i++)
                {
                    float t = i / (float)_generator.EdgeCount;
                    float angRad = t * 6.2831853f;
                    Vector3 direction = new Vector3(MathF.Sin(angRad), Mathf.Cos(angRad), 0);
                    _normals.Add(_bezierPoints[point].LocalToWorldVector(direction.normalized));
                    _verts.Add(_bezierPoints[point].LocalToWorldPosition(direction * (isExtruded ? _generator.RingRadius + _generator.Radius : _generator.Radius)));
                }

                // Extra vertice to fix smoothed UVs

                _normals.Add(_normals[^_generator.EdgeCount]);
                _verts.Add(_verts[^_generator.EdgeCount]);
            }
        }

        private void GenerateTriangles(bool isExtruded = false)
        {
            var edges = _generator.EdgeCount + 1;
            for (int s = 0; s < _bezierPoints.Count - 1; s++)
            {
                if (isExtruded && s % (_generator.CurvedSegmentCount + 1) == 0) continue;
                if (!isExtruded && _generator.HasRings && _generator.HasExtrusion && s % (_generator.CurvedSegmentCount + 1) != 0) continue;

                int rootIndex = s * edges;
                int rootIndexNext = (s + 1) * edges;
                for (int i = 0; i < edges; i++)
                {
                    int currentA = rootIndex + i;
                    int currentB = rootIndex + (i + 1) % edges;
                    int nextA = rootIndexNext + i;
                    int nextB = rootIndexNext + (i + 1) % edges;

                    _triIndices.Add(nextB);
                    _triIndices.Add(nextA);
                    _triIndices.Add(currentA);
                    _triIndices.Add(currentB);
                    _triIndices.Add(nextB);
                    _triIndices.Add(currentA);
                }
            }
        }

        private void GenerateDisc(BezierPoint point)
        {
            var rootIndex = _verts.Count;
            bool isFirst = (point.Pos == _bezierPoints[0].Pos);
            bool isLast = (point.Pos == _bezierPoints[^1].Pos);

            if (isFirst)
                point.Pos -= point.LocalToWorldVector(Vector3.forward) * (_generator.CapThickness + _generator.CapOffset);
            else if (isLast)
                point.Pos += point.LocalToWorldVector(Vector3.forward) * _generator.CapOffset;
            else
                point.Pos -= point.LocalToWorldVector(Vector3.forward) * _ringThickness / 2;

            var radius = (isLast || isFirst) ? _generator.CapRadius + _generator.Radius : _generator.RingRadius + _generator.Radius;
            var uv = (isLast || isFirst) ? _generator.CapThickness : _ringThickness;

            for (int p = 0; p < 2; p++)
            {
                for (int i = 0; i < _generator.EdgeCount; i++)
                {
                    float t = i / (float)_generator.EdgeCount;
                    float angRad = t * 6.2831853f;
                    Vector3 direction = new Vector3(MathF.Sin(angRad), Mathf.Cos(angRad), 0);
                    _normals.Add(point.LocalToWorldVector(direction.normalized));
                    _verts.Add(point.LocalToWorldPosition(direction * radius));
                    _uvs.Add(new Vector2(t, uv * p));
                }

                _normals.Add(_normals[^_generator.EdgeCount]);
                _verts.Add(_verts[^_generator.EdgeCount]);
                _uvs.Add(new Vector2(1, uv * p));

                _planes.Add(new PlaneInfo(point, radius, p == 0));

                if (isLast || isFirst)
                    point.Pos += point.LocalToWorldVector(Vector3.forward) * _generator.CapThickness;
                else
                    point.Pos += point.LocalToWorldVector(Vector3.forward) * _ringThickness;

            }

            var edges = _generator.EdgeCount + 1;

            for (int i = 0; i < edges; i++)
            {
                _triIndices.Add(i + rootIndex);
                _triIndices.Add(edges + i + rootIndex);
                _triIndices.Add(edges + (i + 1) % edges + rootIndex);
                _triIndices.Add(i + rootIndex);
                _triIndices.Add(edges + (i + 1) % edges + rootIndex);
                _triIndices.Add((i + 1) % edges + rootIndex);
            }
        }

        private void GeneratePlane(PlaneInfo plane)
        {
            var edges = _generator.EdgeCount + 1;
            var rootIndex = _verts.Count;

            var planePointVectors = new List<Vector3>();

            for (int p = 0; p < 2; p++)
            {
                for (int i = 0; i < _generator.EdgeCount; i++)
                {
                    float t = i / (float)_generator.EdgeCount;
                    float angRad = t * 6.2831853f;
                    Vector3 direction = new Vector3(MathF.Sin(angRad), Mathf.Cos(angRad), 0);
                    planePointVectors.Add(direction);
                }
                planePointVectors.Add(planePointVectors[^1]);
            }

            for (int i = 0; i < planePointVectors.Count; i++)
            {
                _verts.Add(plane.Point.LocalToWorldPosition(planePointVectors[i] * plane.Radius));
                if (i > _generator.EdgeCount)
                    _normals.Add(plane.Point.LocalToWorldVector(Vector3.forward));
                else
                    _normals.Add(plane.Point.LocalToWorldVector(Vector3.back));
                _uvs.Add(planePointVectors[i] * _generator.RingsUVScale);
            }

            for (int i = 1; i < edges - 1; i++)
            {
                if (plane.IsForward)
                {
                    _triIndices.Add(0 + rootIndex);
                    _triIndices.Add(i + rootIndex);
                    _triIndices.Add(i + 1 + rootIndex);
                }
                else
                {
                    _triIndices.Add(edges + i + 1 + rootIndex);
                    _triIndices.Add(edges + i + rootIndex);
                    _triIndices.Add(edges + rootIndex);
                }
            }
        }

        private struct BezierPoint
        {
            public Vector3 Pos;
            public Quaternion Rot;

            public BezierPoint(Vector3 pos, Quaternion rot)
            {
                this.Pos = pos;
                this.Rot = rot;
            }

            public Vector3 LocalToWorldPosition(Vector3 localSpacePos) => Rot * localSpacePos + Pos;
            public Vector3 LocalToWorldVector(Vector3 localSpacePos) => Rot * localSpacePos;
        }

        private struct PlaneInfo
        {
            public BezierPoint Point;
            public float Radius;
            public bool IsForward;

            public PlaneInfo(BezierPoint point, float radius, bool isForward)
            {
                this.Point = point;
                this.Radius = radius;
                this.IsForward = isForward;
            }
        }
    }
}