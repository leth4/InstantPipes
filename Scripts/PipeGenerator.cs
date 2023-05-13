using System;
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

    public float RingThickness = 1;
    public float RingRadius = 1.3f;

    public float CapThickness = 1;
    public float CapRadius = 1.3f;

    public Material Material;

    public bool HasRings;
    public bool HasCaps;

    private Mesh _mesh;

    private Renderer _renderer;

    public List<Pipe> Pipes = new();
    public PathCreator PathCreator = new();

    private float _maxDistanceBetweenPointsSquared;
    public float MaxCurvature => Mathf.Sqrt(_maxDistanceBetweenPointsSquared) / 2;

    private void OnEnable()
    {
        _mesh = new Mesh { name = "Pipes" };
        GetComponent<MeshFilter>().sharedMesh = _mesh;
        GetComponent<MeshCollider>().sharedMesh = null;
        GetComponent<MeshCollider>().sharedMesh = _mesh;
        _renderer = GetComponent<Renderer>();
        UpdateMesh();
    }

    public void AddPipe(List<Vector3> points)
    {
        Pipes.Add(new Pipe(points));
    }

    public void UpdateMesh()
    {
        _mesh.Clear();
        _maxDistanceBetweenPointsSquared = 0;
        GetComponent<MeshCollider>().sharedMesh = null;

        var submeshes = new List<CombineInstance>();
        foreach (var pipe in Pipes)
        {
            _mesh.Clear();
            // CheckMaxDistance();
            var instance = new CombineInstance { mesh = pipe.GenerateMesh(this) };
            submeshes.Add(instance);
            _mesh.CombineMeshes(submeshes.ToArray(), false, false);

            GetComponent<MeshCollider>().sharedMesh = null;
            GetComponent<MeshCollider>().sharedMesh = _mesh;
        }

        var materialArray = new Material[Pipes.Count];
        for (int i = 0; i < materialArray.Length; i++) materialArray[i] = Material;
        _renderer.sharedMaterials = materialArray;
    }

    // private void CheckMaxDistance()
    // {
    //     for (int i = 1; i < Points.Count; i++)
    //     {
    //         var sqrDist = (Points[i] - Points[i - 1]).sqrMagnitude;
    //         if (sqrDist > _maxDistanceBetweenPointsSquared)
    //             _maxDistanceBetweenPointsSquared = sqrDist;
    //     }
    // }
}
