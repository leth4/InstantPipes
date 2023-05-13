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

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_generator, "Set Field Value");

            _generator.Material = material;
            _generator.Radius = radius;
            _generator.Curvature = Mathf.Abs(curvature);

            _generator.HasRings = hasRings;
            _generator.RingRadius = ringRadius;
            _generator.RingThickness = ringThickness;

            _generator.HasCaps = hasCaps;
            _generator.CapRadius = capRadius;
            _generator.CapThickness = capThickness;

            _generator.EdgeCount = edgeCount;
            _generator.CurvedSegmentCount = segmentCount;
            _generator.UpdateMesh();
        }

        EditorGUILayout.Space(10);
        _editingMode = GUILayout.Toolbar(_editingMode, new string[] { "Edit path", "Edit by hand" });
        EditorGUILayout.Space(10);

        if (_editingMode == 0)
        {
            EditorGUI.BeginChangeCheck();
            var pathHeight = EditorGUILayout.FloatField("Height", PathCreator.Height);
            var pathGridSize = EditorGUILayout.FloatField("Grid Size", PathCreator.GridSize);
            var chaos = EditorGUILayout.FloatField("Chaos", PathCreator.Chaos);
            var straightPriority = EditorGUILayout.FloatField("Straight Proirity", PathCreator.StraightPathPriority);
            var nearObstaclePriority = EditorGUILayout.FloatField("Near Obstacle Proirity", PathCreator.NearObstaclesPriority);

            if (GUILayout.Button("Regenerate"))
            {
                RegeneratePath();
            }

            if (!PathCreator.LastPathSuccess)
            {
                EditorGUILayout.HelpBox("Last path build insuccessful", MessageType.Warning);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_generator, "Set Field Value");

                PathCreator.Radius = radius + (hasRings ? ringRadius : 0);
                PathCreator.Height = pathHeight;
                PathCreator.GridSize = pathGridSize;
                PathCreator.StraightPathPriority = straightPriority;
                PathCreator.NearObstaclesPriority = nearObstaclePriority;
                PathCreator.Chaos = chaos;

                RegeneratePath();
                _generator.UpdateMesh();
            }
        }

        if (_editingMode == 1)
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
                Undo.RecordObject(_generator, "Deleted a Pipe");
                _generator.Pipes[_selectedPipeIndex].RemoveAt(_selectedPointIndex);
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
                if (_selectedPointIndex != _generator.Pipes[_selectedPipeIndex].Count - 1)
                    position = _generator.Pipes[_selectedPipeIndex][_selectedPointIndex] + (_generator.Pipes[_selectedPipeIndex][_selectedPointIndex + 1] - _generator.Pipes[_selectedPipeIndex][_selectedPointIndex]) / 2;
                else
                    position = _generator.Pipes[_selectedPipeIndex][_selectedPointIndex] + Vector3.one;
                _generator.Pipes[_selectedPipeIndex].Insert(_selectedPointIndex + 1, position);
                _generator.UpdateMesh();
                _selectedPointIndex = _selectedPointIndex + 1;
                Repaint();
            }
            GUI.enabled = true;
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Erase"))
        {
            Undo.RecordObject(_generator, "Erased all Pipes");
            _generator.Pipes.Clear();
            _generator.UpdateMesh();
        }
    }

    private void RegeneratePath()
    {
        var pipesCopy = new List<List<Vector3>>(_generator.Pipes);
        _generator.Pipes = new();
        _generator.UpdateMesh();
        foreach (var pipe in pipesCopy)
        {
            _generator.Pipes.Add(PathCreator.Create(pipe[0], pipe[1] - pipe[0], pipe[^1], pipe[^2] - pipe[^1]));
            _generator.UpdateMesh();
        }
    }

    private void OnSceneGUI()
    {
        Event evt = Event.current;

        if (_editingMode == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000))
                _mouseHit = hit;

            if (_isDragging)
                Handles.DrawDottedLine(_startDragPoint, _mouseHit.point, 4);

            if (evt.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }
            else
            {
                HandlePathInput(evt);
            }
        }

        if (_editingMode == 1)
        {
            HandleEdit(evt);
            return;
        }
    }

    private void HandleEdit(Event evt)
    {
        EditorGUI.BeginChangeCheck();

        var pipesCopy = new List<List<Vector3>>();
        for (int i = 0; i < _generator.Pipes.Count; i++)
        {
            var pipePointsCopy = new List<Vector3>(_generator.Pipes[i]);
            for (int j = 0; j < pipePointsCopy.Count; j++)
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                if (i == _selectedPipeIndex && j == _selectedPointIndex)
                {
                    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                    pipePointsCopy[j] = Handles.PositionHandle(pipePointsCopy[j], Quaternion.identity);
                }
                else
                {
                    if (Handles.Button(pipePointsCopy[j], Quaternion.identity, _generator.Radius * 2.1f, _generator.Radius, Handles.SphereHandleCap))
                    {
                        _selectedPipeIndex = i;
                        _selectedPointIndex = j;
                        Repaint();
                    }
                }
            }
            pipesCopy.Add(pipePointsCopy);
        }

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_generator, "Moved a point");
            _generator.Pipes.Clear();
            _generator.Pipes.AddRange(pipesCopy);
            _generator.UpdateMesh();
        }
    }

    private void HandlePathInput(Event evt)
    {
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
                _generator.AddPipe(PathCreator.Create(_startDragPoint, _startDragNormal, _mouseHit.point, _mouseHit.normal));
                _generator.UpdateMesh();
            }
        }
        Repaint();
    }
}
