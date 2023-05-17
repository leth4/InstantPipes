using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PipeGenerator))]
public class PipeEditor : Editor
{
    private PipeGenerator _generator;
    private RaycastHit _mouseHit;

    private Vector3 _startDragPoint;
    private Vector3 _startDragNormal;

    private bool _isDragging = false;

    private int _editingMode = 0;
    private int _selectedPipeIndex = -1;
    private int _selectedPointIndex = -1;

    private void OnEnable()
    {
        _generator = (PipeGenerator)target;
        Undo.undoRedoPerformed += OnUndo;
        Tools.hidden = true;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndo;
        Tools.hidden = false;
    }

    private void OnUndo()
    {
        _generator.UpdateMesh();
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();

        GUILayout.Label("Pipes", EditorStyles.boldLabel);
        var radius = EditorGUILayout.FloatField("Radius", _generator.Radius);
        var curvature = EditorGUILayout.Slider("Curvature", _generator.Curvature, 0.2f, _generator.MaxCurvature);
        var material = (Material)EditorGUILayout.ObjectField("Material", _generator.Material, typeof(Material), false);
        EditorGUILayout.Space(10);

        GUILayout.Label("Quality", EditorStyles.boldLabel);
        var edgeCount = EditorGUILayout.IntSlider("Edges", _generator.EdgeCount, 3, 40);
        var segmentCount = EditorGUILayout.IntSlider("Segments", _generator.CurvedSegmentCount, 1, 40);
        EditorGUILayout.Space(10);

        var hasRings = EditorGUILayout.ToggleLeft("Rings", _generator.HasRings, EditorStyles.boldLabel);
        float ringRadius = _generator.RingRadius, ringThickness = _generator.RingThickness;
        if (_generator.HasRings)
        {
            ringRadius = EditorGUILayout.Slider("Radius", _generator.RingRadius, 0.05f, radius);
            ringThickness = EditorGUILayout.Slider("Thickness", _generator.RingThickness, 0.1f, 1);
            EditorGUILayout.Space(10);
        }

        var hasCaps = EditorGUILayout.ToggleLeft("Caps", _generator.HasCaps, EditorStyles.boldLabel);
        float capRadius = _generator.CapRadius, capThickness = _generator.CapThickness;
        if (_generator.HasCaps)
        {
            capRadius = EditorGUILayout.Slider("Radius", _generator.CapRadius, 0.05f, radius);
            capThickness = EditorGUILayout.Slider("Thickness", _generator.CapThickness, 0.1f, 1);
        }
        EditorGUILayout.Space(10);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_generator, "Set Field Value");

            _generator.Material = material;
            _generator.Radius = radius;
            _generator.Curvature = Mathf.Clamp(curvature, 0.2f, _generator.MaxCurvature);
            _generator.HasRings = hasRings;
            _generator.RingRadius = Mathf.Clamp(ringRadius, 0.05f, radius);
            _generator.RingThickness = Mathf.Clamp(ringThickness, 0.1f, 1);
            _generator.HasCaps = hasCaps;
            _generator.CapRadius = Mathf.Clamp(capRadius, 0.05f, radius);
            _generator.CapThickness = Mathf.Clamp(capThickness, 0.1f, 1);
            _generator.EdgeCount = edgeCount;
            _generator.CurvedSegmentCount = segmentCount;

            _generator.PathCreator.Radius = radius + (hasRings ? ringRadius : 0);
            _generator.UpdateMesh();
        }

        _editingMode = GUILayout.Toolbar(_editingMode, new string[] { "Edit path", "Edit by hand" });
        EditorGUILayout.Space(10);

        if (_editingMode == 0)
        {
            EditPathGUI();
        }

        if (_editingMode == 1)
        {
            EditByHandGUI();
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Erase"))
        {
            Undo.RecordObject(_generator, "Erased all Pipes");
            _generator.Pipes.Clear();
            _generator.UpdateMesh();
        }
    }

    private void EditByHandGUI()
    {
        GUI.enabled = _selectedPipeIndex != -1;
        if (GUILayout.Button("Delete Selected Pipe"))
        {
            Undo.RecordObject(_generator, "Deleted a Pipe");
            _generator.Pipes.RemoveAt(_selectedPipeIndex);
            _generator.UpdateMesh();
            _selectedPipeIndex = -1;
        }
        GUI.enabled = true;

        GUI.enabled = _selectedPipeIndex != -1 && _selectedPointIndex != -1;
        if (GUILayout.Button("Delete Selected Point"))
        {
            Undo.RecordObject(_generator, "Deleted a point");
            _generator.Pipes[_selectedPipeIndex].Points.Remove(_generator.Pipes[_selectedPipeIndex].Points[_selectedPointIndex]);
            _generator.UpdateMesh();
            _selectedPipeIndex = -1;
            _selectedPointIndex = -1;
        }
        GUI.enabled = true;

        GUI.enabled = _selectedPipeIndex != -1 && _selectedPointIndex != -1;
        if (GUILayout.Button("Insert a point"))
        {
            Undo.RecordObject(_generator, "Inserted a point");
            var position = Vector3.zero;
            if (_selectedPointIndex != _generator.Pipes[_selectedPipeIndex].Points.Count - 1)
                position = _generator.Pipes[_selectedPipeIndex].Points[_selectedPointIndex] + (_generator.Pipes[_selectedPipeIndex].Points[_selectedPointIndex + 1] - _generator.Pipes[_selectedPipeIndex].Points[_selectedPointIndex]) / 2;
            else
                position = _generator.Pipes[_selectedPipeIndex].Points[_selectedPointIndex] + Vector3.one;
            _generator.Pipes[_selectedPipeIndex].Points.Insert(_selectedPointIndex + 1, position);
            _generator.UpdateMesh();
            _selectedPointIndex = _selectedPointIndex + 1;
            Repaint();
        }
        GUI.enabled = true;
    }

    private void EditPathGUI()
    {
        EditorGUI.BeginChangeCheck();

        var pathGridSize = EditorGUILayout.FloatField("Grid Size", _generator.PathCreator.GridSize);
        var pathHeight = EditorGUILayout.FloatField("Height", _generator.PathCreator.Height);
        var chaos = EditorGUILayout.FloatField("Chaos", _generator.PathCreator.Chaos);
        var straightPriority = EditorGUILayout.FloatField("Straight Proirity", _generator.PathCreator.StraightPathPriority);
        var nearObstaclePriority = EditorGUILayout.FloatField("Near Obstacle Proirity", _generator.PathCreator.NearObstaclesPriority);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_generator, "Set Field Value");

            _generator.PathCreator.GridSize = Mathf.Clamp(pathGridSize, 0, Mathf.Infinity);
            _generator.PathCreator.Height = Mathf.Clamp(pathHeight, pathGridSize, Mathf.Infinity);
            _generator.PathCreator.StraightPathPriority = straightPriority;
            _generator.PathCreator.NearObstaclesPriority = nearObstaclePriority;
            _generator.PathCreator.Chaos = chaos;

            RegeneratePath();
        }

        if (GUILayout.Button("Regenerate"))
        {
            RegeneratePath();
        }

        if (!_generator.PathCreator.LastPathSuccess)
        {
            EditorGUILayout.HelpBox("Last path build insuccessful", MessageType.Warning);
        }
    }

    private void RegeneratePath()
    {
        var pipesCopy = new List<Pipe>(_generator.Pipes);
        _generator.Pipes = new();
        _generator.UpdateMesh();
        foreach (var pipe in pipesCopy)
        {
            _generator.AddPipe(_generator.PathCreator.Create(pipe.Points[0], pipe.Points[1] - pipe.Points[0], pipe.Points[^1], pipe.Points[^2] - pipe.Points[^1]));
            _generator.UpdateMesh();
        }
    }

    private void OnSceneGUI()
    {
        if (_editingMode == 0)
            HandlePathInput(Event.current);
        else if (_editingMode == 1)
            HandleEdit(Event.current);
    }

    private void HandleEdit(Event evt)
    {
        Handles.matrix = _generator.transform.localToWorldMatrix;

        for (int i = 0; i < _generator.Pipes.Count; i++)
        {
            for (int j = 0; j < _generator.Pipes[i].Points.Count; j++)
            {
                if (i == _selectedPipeIndex && j == _selectedPointIndex)
                {
                    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

                    EditorGUI.BeginChangeCheck();
                    var position = Handles.PositionHandle(_generator.Pipes[i].Points[j], Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_generator, "Moved a point");
                        _generator.Pipes[i].Points[j] = position;
                        _generator.UpdateMesh();
                    }
                }
                else
                {
                    if (j != 0)
                    {
                        Handles.DrawLine(_generator.Pipes[i].Points[j], _generator.Pipes[i].Points[j - 1]);
                    }
                    if (Handles.Button(_generator.Pipes[i].Points[j], Quaternion.identity, 0.8f, 2f, Handles.SphereHandleCap))
                    {
                        _selectedPipeIndex = i;
                        _selectedPointIndex = j;
                        Repaint();
                    }
                }
            }
        }
    }

    private void HandlePathInput(Event evt)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
        if (Physics.Raycast(ray, out var hit, 1000))
            _mouseHit = hit;

        if (_isDragging)
            Handles.DrawDottedLine(_startDragPoint, _mouseHit.point, 4);

        if (evt.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            return;
        }
        if (evt.type == EventType.MouseDown && evt.button == 0 && evt.modifiers == EventModifiers.None)
        {
            _isDragging = true;
            _startDragNormal = _mouseHit.normal;
            _startDragPoint = _mouseHit.point;
        }

        if (_isDragging && evt.type == EventType.MouseUp && evt.button == 0 && evt.modifiers == EventModifiers.None)
        {
            _isDragging = false;
            if (Vector3.Distance(_mouseHit.point, _startDragPoint) > 3)
            {
                Undo.RecordObject(_generator, "Add Pipe");
                _generator.AddPipe(_generator.PathCreator.Create(_startDragPoint, _startDragNormal, _mouseHit.point, _mouseHit.normal));
                _generator.UpdateMesh();
            }
        }
        Repaint();
    }
}
