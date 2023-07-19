using System.Collections.Generic;
using UnityEngine;

namespace InstantPipes
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    public class PipeGenerator : MonoBehaviour
    {
        public float Radius = 1;
        public int EdgeCount = 10;
        public int CurvedSegmentCount = 10;
        public float Curvature = 0.5f;

        public bool HasRings;
        public bool HasExtrusion;
        public float RingThickness = 1;
        public float RingRadius = 1.3f;

        public bool HasCaps;
        public float CapThickness = 1;
        public float CapRadius = 1.3f;
        public float CapOffset = 0f;

        public int PipesAmount = 1;

        public Material Material;
        public Material RingMaterial;
        public float RingsUVScale = 1;
        public bool IsSeparateRingsMaterial = false;

        private Renderer _renderer;
        private MeshCollider _collider;
        private Mesh _mesh;

        public List<Pipe> Pipes = new();
        public PathCreator PathCreator = new();

        private float _maxDistanceBetweenPoints;
        public float MaxCurvature => _maxDistanceBetweenPoints / 2;

        private void OnEnable()
        {
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<MeshCollider>();

            _mesh = new Mesh { name = "Pipes" };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _collider.sharedMesh = _mesh;

            UpdateMesh();
        }

        public void UpdateMesh()
        {
            _maxDistanceBetweenPoints = 0;
            _collider.sharedMesh = null;

            var submeshes = new List<CombineInstance>();
            foreach (var pipe in Pipes)
            {
                _mesh.Clear();

                var meshes = pipe.GenerateMeshes(this);
                meshes.ForEach(mesh => submeshes.Add(new() { mesh = mesh }));
                _mesh.CombineMeshes(submeshes.ToArray(), false, false);

                _collider.sharedMesh = _mesh;

                _maxDistanceBetweenPoints = Mathf.Max(_maxDistanceBetweenPoints, pipe.GetMaxDistanceBetweenPoints());
            }

            var materials = new List<Material>();
            if (IsSeparateRingsMaterial && (HasCaps || HasRings))
            {
                for (int i = 0; i < Pipes.Count * 2; i++) materials.Add(i % 2 == 0 ? Material : RingMaterial);
            }
            else if (HasCaps || HasRings)
            {
                for (int i = 0; i < Pipes.Count * 2; i++) materials.Add(Material);
            }
            else
            {
                for (int i = 0; i < Pipes.Count; i++) materials.Add(Material);
            }
            _renderer.sharedMaterials = materials.ToArray();
        }

        public bool AddPipe(Vector3 startPoint, Vector3 startNormal, Vector3 endPoint, Vector3 endNormal)
        {
            var failed = false;

            var vectorLength = PipesAmount * Radius + (PipesAmount - 1) * PathCreator.GridSize;
            var startVector = Vector3.Cross(startNormal, endPoint - startPoint).normalized * vectorLength;
            var endVector = Vector3.Cross(endNormal, endPoint - startPoint).normalized * vectorLength;
            var stepSize = startVector.magnitude / (PipesAmount);

            var temporaryColliders = new List<GameObject>();

            for (int i = 0; i < PipesAmount; i++)
            {
                var start = startVector.normalized * (stepSize * (i - PipesAmount / 2f + 0.5f));
                var end = endVector.normalized * (stepSize * (i - PipesAmount / 2f + 0.5f));

                temporaryColliders.Add(CreateTemporaryCollider(startPoint + start, startNormal));
                temporaryColliders.Add(CreateTemporaryCollider(endPoint + end, endNormal));
            }

            try
            {
                for (int i = 0; i < PipesAmount; i++)
                {
                    var start = startVector.normalized * (stepSize * (i - PipesAmount / 2f + 0.5f));
                    var end = endVector.normalized * (stepSize * (i - PipesAmount / 2f + 0.5f));

                    Pipes.Add(new Pipe(PathCreator.Create(start + startPoint, startNormal, end + endPoint, endNormal)));
                    if (!PathCreator.LastPathSuccess) failed = true;
                    UpdateMesh();
                }
            }
            finally
            {
                temporaryColliders.ForEach(collider => Object.DestroyImmediate(collider));
            }

            return !failed;
        }

        public void InsertPoint(int pipeIndex, int pointIndex)
        {
            var position = Vector3.zero;
            if (pointIndex != Pipes[pipeIndex].Points.Count - 1)
                position = (Pipes[pipeIndex].Points[pointIndex + 1] + Pipes[pipeIndex].Points[pointIndex]) / 2;
            else
                position = Pipes[pipeIndex].Points[pointIndex] + Vector3.one;
            Pipes[pipeIndex].Points.Insert(pointIndex + 1, position);
            UpdateMesh();
        }

        public void RemovePoint(int pipeIndex, int pointIndex)
        {
            Pipes[pipeIndex].Points.RemoveAt(pointIndex);
            UpdateMesh();
        }

        public void RemovePipe(int pipeIndex)
        {
            Pipes.RemoveAt(pipeIndex);
            UpdateMesh();
        }

        public bool RegeneratePaths()
        {
            var pipesCopy = new List<Pipe>(Pipes);
            Pipes = new();
            UpdateMesh();
            var failed = false;

            var temporaryColliders = new List<GameObject>();

            foreach (var pipe in pipesCopy)
            {
                temporaryColliders.Add(CreateTemporaryCollider(pipe.Points[0], (pipe.Points[1] - pipe.Points[0]).normalized));
                temporaryColliders.Add(CreateTemporaryCollider(pipe.Points[^1], (pipe.Points[^2] - pipe.Points[^1]).normalized));
            }

            try
            {
                foreach (var pipe in pipesCopy)
                {
                    Pipes.Add(new Pipe(PathCreator.Create(pipe.Points[0], pipe.Points[1] - pipe.Points[0], pipe.Points[^1], pipe.Points[^2] - pipe.Points[^1])));
                    if (!PathCreator.LastPathSuccess) failed = true;
                    UpdateMesh();
                }
            }
            finally
            {
                temporaryColliders.ForEach(collider => Object.DestroyImmediate(collider));
            }

            return !failed;
        }

        private GameObject CreateTemporaryCollider(Vector3 point, Vector3 normal)
        {
            var tempCollider = new GameObject();
            tempCollider.transform.position = point + (normal * PathCreator.Height) / 2;
            tempCollider.transform.localScale = new Vector3(Radius * 2, PathCreator.Height - Radius * 3f, Radius * 2);
            tempCollider.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            tempCollider.AddComponent<CapsuleCollider>();
            return tempCollider;
        }
    }
}