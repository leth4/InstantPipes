using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace InstantPipes
{
    [System.Serializable]
    public class Pipe
    {
        public List<UnityEngine.Vector3> Points;

        private List<UnityEngine.Vector3> _verts;
        private List<UnityEngine.Vector3> _normals;
        private List<UnityEngine.Vector2> _uvs;
        private List<int> _triIndices;
        private List<BezierPoint> _bezierPoints;
        private PipeGenerator _generator;

        private float _currentAngleOffset;
        private UnityEngine.Quaternion _previousRotation;

        public Pipe(List<UnityEngine.Vector3> points)
        {
            Points = points;
        }

        public float GetMaxDistanceBetweenPoints()
        {
            var maxDistance = 0f;
            for (int i = 1; i < Points.Count; i++)
            {
                maxDistance = Mathf.Max(maxDistance, UnityEngine.Vector3.Distance(Points[i], Points[i - 1]));
            }
            return maxDistance;
        }

        public Mesh GenerateMesh(PipeGenerator generator)
        {
            _generator = generator;

            ClearMeshInfo();

            var ringPoints = new List<int>();

            var direction = (Points[0] - Points[1]).normalized;
            var rotation = (direction != UnityEngine.Vector3.zero)
                ? UnityEngine.Quaternion.LookRotation(direction, UnityEngine.Vector3.up)
                : UnityEngine.Quaternion.identity;
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

            if (_generator.HasCaps)
            {
                GenerateDisc(_bezierPoints[^1]);
                GenerateDisc(_bezierPoints[0]);
            }

            if (_generator.HasRings)
            {
                foreach (var point in ringPoints)
                    GenerateDisc(_bezierPoints[point]);
            }

            Mesh mesh = new Mesh();
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triIndices, 0);

            return mesh;
        }

        private void ClearMeshInfo()
        {
            _verts = new List<UnityEngine.Vector3>();
            _normals = new List<UnityEngine.Vector3>();
            _uvs = new List<UnityEngine.Vector2>();
            _triIndices = new List<int>();
            _bezierPoints = new List<BezierPoint>();
        }

        private BezierPoint GetBezierPoint(float t, int x)
        {
            UnityEngine.Vector3 prev, next;

            if ((Points[x] - Points[x - 1]).magnitude > _generator.Curvature * 2 + _generator.RingThickness)
                prev = Points[x] - (Points[x] - Points[x - 1]).normalized * _generator.Curvature;
            else
                prev = (Points[x] + Points[x - 1]) / 2 + (Points[x] - Points[x - 1]).normalized * _generator.RingThickness / 2;

            if ((Points[x] - Points[x + 1]).magnitude > _generator.Curvature * 2 + _generator.RingThickness)
                next = Points[x] - (Points[x] - Points[x + 1]).normalized * _generator.Curvature;
            else
                next = (Points[x] + Points[x + 1]) / 2 + (Points[x] - Points[x + 1]).normalized * _generator.RingThickness / 2;

            if (x == 1)
            {
                if ((Points[x] - Points[x - 1]).magnitude > _generator.Curvature + _generator.RingThickness * 2.5f)
                    prev = Points[x] - (Points[x] - Points[x - 1]).normalized * _generator.Curvature;
                else
                    prev = Points[x - 1] + (Points[x] - Points[x - 1]).normalized * _generator.RingThickness * 2.5f;
            }

            else if (x == Points.Count - 2)
            {
                if ((Points[x] - Points[x + 1]).magnitude > _generator.Curvature + _generator.RingThickness * 2.5f)
                    next = Points[x] - (Points[x] - Points[x + 1]).normalized * _generator.Curvature;
                else
                    next = Points[x + 1] + (Points[x] - Points[x + 1]).normalized * _generator.RingThickness * 2.5f;
            }

            UnityEngine.Vector3 a = UnityEngine.Vector3.Lerp(prev, Points[x], t);
            UnityEngine.Vector3 b = UnityEngine.Vector3.Lerp(Points[x], next, t);
            var position = UnityEngine.Vector3.Lerp(a, b, t);

            UnityEngine.Vector3 aNext = UnityEngine.Vector3.LerpUnclamped(prev, Points[x], t + 0.001f);
            UnityEngine.Vector3 bNext = UnityEngine.Vector3.LerpUnclamped(Points[x], next, t + 0.001f);

            var tangent = UnityEngine.Vector3.Cross(a - b, aNext - bNext);
            var rotation = (a != b) ? UnityEngine.Quaternion.LookRotation((a - b).normalized, tangent) : UnityEngine.Quaternion.identity;

            // Rotate new tangent along the forward axis to match the previous part

            if (t == 0)
            {
                _currentAngleOffset = UnityEngine.Quaternion.Angle(_previousRotation, rotation);
                var offsetRotation = UnityEngine.Quaternion.AngleAxis(_currentAngleOffset, UnityEngine.Vector3.forward);
                if (UnityEngine.Quaternion.Angle(rotation * offsetRotation, _previousRotation) > 0)
                    _currentAngleOffset *= -1;
            }
            rotation *= UnityEngine.Quaternion.AngleAxis(_currentAngleOffset, UnityEngine.Vector3.forward);

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
                    _uvs.Add(new UnityEngine.Vector2(edge / (float)_generator.EdgeCount, currentUV * length));
                }
                _uvs.Add(new UnityEngine.Vector2(1, currentUV * length));
            }
        }

        private void GenerateVertices()
        {
            for (int point = 0; point < _bezierPoints.Count; point++)
            {
                for (int i = 0; i < _generator.EdgeCount; i++)
                {
                    float t = i / (float)_generator.EdgeCount;
                    float angRad = t * 6.2831853f;
                    UnityEngine.Vector3 direction = new UnityEngine.Vector3(MathF.Sin(angRad), Mathf.Cos(angRad), 0);
                    _normals.Add(_bezierPoints[point].LocalToWorldVector(direction.normalized));
                    _verts.Add(_bezierPoints[point].LocalToWorldPosition(direction * _generator.Radius));
                }

                // Extra vertice to fix smoothed UVs

                _normals.Add(_normals[^_generator.EdgeCount]);
                _verts.Add(_verts[^_generator.EdgeCount]);
            }
        }

        private void GenerateTriangles()
        {
            var edges = _generator.EdgeCount + 1;
            for (int s = 0; s < _bezierPoints.Count - 1; s++)
            {
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

            List<UnityEngine.Vector2> planeUVs = new List<UnityEngine.Vector2>();

            if (isFirst)
                point.Pos -= point.LocalToWorldVector(UnityEngine.Vector3.forward) * (_generator.CapThickness + _generator.CapOffset);
            else if (!isLast)
                point.Pos -= point.LocalToWorldVector(UnityEngine.Vector3.forward) * (_generator.RingThickness / 2 + _generator.CapOffset);
            else
                point.Pos -= point.LocalToWorldVector(UnityEngine.Vector3.forward) * _generator.CapOffset;

            var radius = (isLast || isFirst) ? _generator.CapRadius + _generator.Radius : _generator.RingRadius + _generator.Radius;
            var uv = (isLast || isFirst) ? _generator.CapThickness : _generator.RingThickness;

            for (int p = 0; p < 2; p++)
            {
                for (int i = 0; i < _generator.EdgeCount; i++)
                {
                    float t = i / (float)_generator.EdgeCount;
                    float angRad = t * 6.2831853f;
                    UnityEngine.Vector3 direction = new UnityEngine.Vector3(MathF.Sin(angRad), Mathf.Cos(angRad), 0);
                    _normals.Add(point.LocalToWorldVector(direction.normalized));
                    _verts.Add(point.LocalToWorldPosition(direction * radius));
                    _uvs.Add(new UnityEngine.Vector2(t, uv * p));
                    planeUVs.Add(direction);
                }

                _normals.Add(_normals[^_generator.EdgeCount]);
                _verts.Add(_verts[^_generator.EdgeCount]);
                _uvs.Add(new UnityEngine.Vector2(1, uv * p));
                planeUVs.Add(planeUVs[^1]);
                if (isLast || isFirst)
                    point.Pos += point.LocalToWorldVector(UnityEngine.Vector3.forward) * _generator.CapThickness;
                else
                    point.Pos += point.LocalToWorldVector(UnityEngine.Vector3.forward) * _generator.RingThickness;
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

            rootIndex = _verts.Count;

            for (int i = 0; i < planeUVs.Count; i++)
            {
                _verts.Add(_verts[^(planeUVs.Count)]);
                if (i > _generator.EdgeCount)
                    _normals.Add(point.LocalToWorldVector(UnityEngine.Vector3.forward));
                else
                    _normals.Add(point.LocalToWorldVector(UnityEngine.Vector3.back));
                _uvs.Add(planeUVs[i] * _generator.RingsUVScale);
            }

            for (int i = 1; i < edges - 1; i++)
            {
                _triIndices.Add(0 + rootIndex);
                _triIndices.Add(i + rootIndex);
                _triIndices.Add(i + 1 + rootIndex);
                _triIndices.Add(edges + i + 1 + rootIndex);
                _triIndices.Add(edges + i + rootIndex);
                _triIndices.Add(edges + rootIndex);
            }
        }

        private struct BezierPoint
        {
            public UnityEngine.Vector3 Pos;
            public UnityEngine.Quaternion Rot;

            public BezierPoint(UnityEngine.Vector3 pos, UnityEngine.Quaternion rot)
            {
                this.Pos = pos;
                this.Rot = rot;
            }

            public UnityEngine.Vector3 LocalToWorldPosition(UnityEngine.Vector3 localSpacePos) => Rot * localSpacePos + Pos;
            public UnityEngine.Vector3 LocalToWorldVector(UnityEngine.Vector3 localSpacePos) => Rot * localSpacePos;
        }
    }
}