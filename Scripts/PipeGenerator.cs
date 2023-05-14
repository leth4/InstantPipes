using System.Collections.Generic;
using UnityEngine;

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
    public float RingThickness = 1;
    public float RingRadius = 1.3f;

    public bool HasCaps;
    public float CapThickness = 1;
    public float CapRadius = 1.3f;

    public Material Material;

    private Renderer _renderer;
    private MeshCollider _collider;
    private Mesh _mesh;

    public List<Pipe> Pipes = new();
    public PathCreator PathCreator = new();

    private float _maxDistanceBetweenPoints;
    public float MaxCurvature => _maxDistanceBetweenPoints / 2;

    private void OnDrawGizmos()
    {
        foreach (var point in PathCreator.Points) Gizmos.DrawSphere(point, 0.1f);
    }

    private void OnEnable()
    {
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<MeshCollider>();

        _mesh = new Mesh { name = "Pipes" };
        GetComponent<MeshFilter>().sharedMesh = _mesh;
        _collider.sharedMesh = null;
        _collider.sharedMesh = _mesh;

        UpdateMesh();
    }

    public void AddPipe(List<Vector3> points)
    {
        Pipes.Add(new Pipe(points));
    }

    public void UpdateMesh()
    {
        _maxDistanceBetweenPoints = 0;
        _collider.sharedMesh = null;

        var submeshes = new List<CombineInstance>();
        foreach (var pipe in Pipes)
        {
            _mesh.Clear();
            var instance = new CombineInstance { mesh = pipe.GenerateMesh(this) };
            submeshes.Add(instance);
            _mesh.CombineMeshes(submeshes.ToArray(), false, false);

            _collider.sharedMesh = null;
            _collider.sharedMesh = _mesh;

            var maxDistance = pipe.GetMaxDistanceBetweenPoints();
            _maxDistanceBetweenPoints = Mathf.Max(_maxDistanceBetweenPoints, maxDistance);
        }

        var materialArray = new Material[Pipes.Count];
        for (int i = 0; i < materialArray.Length; i++) materialArray[i] = Material;
        _renderer.sharedMaterials = materialArray;
    }

    public void RemovePipe(int pipeIndex)
    {

    }
}
