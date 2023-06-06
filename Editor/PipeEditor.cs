#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace InstantPipes
{
    [CustomEditor(typeof(PipeGenerator))]
    public class PipeEditor : Editor
    {
        private PipeGenerator _generator;

        private Vector3 _startDragPoint;
        private Vector3 _startDragNormal;

        private bool _isDragging = false;
        private bool _autoRegenerate = true;
        private bool _lastBuildFailed = false;
        private int _editingMode = 0;

        private int _selectedPipeIndex = -1;
        private List<int> _selectedPointsIndexes = new();
        private Vector3 _positionHandle;

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
            SetHandlePosition();
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            GUILayout.Label("Pipes", EditorStyles.boldLabel);
            var radius = EditorGUILayout.FloatField("Radius", _generator.Radius);
            var curvature = EditorGUILayout.Slider("Curvature", _generator.Curvature, 0.01f, _generator.MaxCurvature);
            var material = (Material)EditorGUILayout.ObjectField("Material", _generator.Material, typeof(Material), false);
            var ringsUVScale = EditorGUILayout.FloatField("Rings UV Scale", _generator.RingsUVScale);
            var maxEndCapOffset = Mathf.Min(_generator.PathCreator.Height, _generator.PathCreator.GridSize) - _generator.CapThickness;
            EditorGUILayout.Space(10);

            var hasRings = EditorGUILayout.ToggleLeft("Rings", _generator.HasRings, EditorStyles.boldLabel);
            float ringRadius = _generator.RingRadius, ringThickness = _generator.RingThickness;
            if (_generator.HasRings)
            {
                ringRadius = EditorGUILayout.Slider("Radius", _generator.RingRadius, 0, radius);
                ringThickness = EditorGUILayout.Slider("Thickness", _generator.RingThickness, 0, radius);
                EditorGUILayout.Space(10);
            }

            var hasCaps = EditorGUILayout.ToggleLeft("End Caps", _generator.HasCaps, EditorStyles.boldLabel);
            float capRadius = _generator.CapRadius, capThickness = _generator.CapThickness;
            float capOffset = _generator.CapOffset;
            if (_generator.HasCaps)
            {
                capRadius = EditorGUILayout.Slider("Radius", _generator.CapRadius, 0, radius);
                capThickness = EditorGUILayout.Slider("Thickness", _generator.CapThickness, 0, radius);
                capOffset = EditorGUILayout.Slider("Offset", _generator.CapOffset, 0, maxEndCapOffset);
            }
            EditorGUILayout.Space(10);

            GUILayout.Label("Quality", EditorStyles.boldLabel);
            var edgeCount = EditorGUILayout.IntSlider("Edges", _generator.EdgeCount, 3, 40);
            var segmentCount = EditorGUILayout.IntSlider("Segments", _generator.CurvedSegmentCount, 1, 40);
            EditorGUILayout.Space(10);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_generator, "Set Field Value");

                _generator.Material = material;
                _generator.RingsUVScale = Mathf.Max(0.01f, ringsUVScale);
                _generator.Radius = Mathf.Max(0.01f, radius);
                _generator.Curvature = Mathf.Clamp(curvature, 0.01f, _generator.MaxCurvature);
                _generator.HasRings = hasRings;
                _generator.RingRadius = Mathf.Clamp(ringRadius, 0, radius);
                _generator.RingThickness = Mathf.Clamp(ringThickness, 0, radius);
                _generator.HasCaps = hasCaps;
                _generator.CapRadius = Mathf.Clamp(capRadius, 0, radius);
                _generator.CapThickness = Mathf.Clamp(capThickness, 0, radius);
                _generator.CapOffset = Mathf.Clamp(capOffset, 0, maxEndCapOffset);
                _generator.EdgeCount = edgeCount;
                _generator.CurvedSegmentCount = segmentCount;

                _generator.PathCreator.Radius = radius + (hasRings ? ringRadius : 0);
                _generator.UpdateMesh();
            }

            _editingMode = GUILayout.Toolbar(_editingMode, new string[] { "Create", "Edit" });
            EditorGUILayout.Space(10);

            if (_editingMode == 0)
            {
                PathGUI();
            }

            if (_editingMode == 1)
            {
                EditGUI();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Erase"))
            {
                Undo.RecordObject(_generator, "Erased all Pipes");
                _generator.Pipes.Clear();
                _generator.UpdateMesh();
            }
        }

        private void EditGUI()
        {
            _selectedPointsIndexes.Sort();
            if (_selectedPipeIndex != -1 && _selectedPointsIndexes.Count != 0)
            {
                EditorGUI.BeginChangeCheck();
                var positions = new Vector3[_generator.Pipes[_selectedPipeIndex].Points.Count];
                foreach (var index in _selectedPointsIndexes)
                {
                    positions[index] = DrawVectorField($"{index}", _generator.Pipes[_selectedPipeIndex].Points[index]);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_generator, "Moved a point");
                    foreach (var index in _selectedPointsIndexes)
                    {
                        _generator.Pipes[_selectedPipeIndex].Points[index] = positions[index];
                    }
                    _generator.UpdateMesh();
                    SetHandlePosition();
                }
                GUILayout.Space(10);
            }

            GUI.enabled = _selectedPipeIndex != -1;
            if (GUILayout.Button("Delete Selected Pipe"))
            {
                Undo.RecordObject(_generator, "Deleted a Pipe");
                _generator.RemovePipe(_selectedPipeIndex);
                _selectedPipeIndex = -1;
            }
            GUI.enabled = true;

            GUI.enabled = _selectedPipeIndex != -1 && _selectedPointsIndexes.Count != 0 && _generator.Pipes[_selectedPipeIndex].Points.Count - _selectedPointsIndexes.Count >= 2;
            if (GUILayout.Button(_selectedPointsIndexes.Count == 1 ? "Delete Selected Point" : "Delete Selected Points"))
            {
                Undo.RecordObject(_generator, "Deleted a point");
                _selectedPointsIndexes.Reverse();
                _selectedPointsIndexes.ForEach(pointIndex => _generator.RemovePoint(_selectedPipeIndex, pointIndex));
                _selectedPipeIndex = -1;
                _selectedPointsIndexes.Clear();
            }
            GUI.enabled = true;

            GUI.enabled = _selectedPipeIndex != -1 && _selectedPointsIndexes.Count == 1;
            if (GUILayout.Button("Insert a point"))
            {
                Undo.RecordObject(_generator, "Inserted a point");
                _generator.InsertPoint(_selectedPipeIndex, _selectedPointsIndexes[0]);
                _selectedPointsIndexes[0]++;
                Repaint();
            }
            GUI.enabled = true;
        }

        private Vector3 DrawVectorField(string label, Vector3 value)
        {
            var defaultLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 12;
            var vector = new Vector3();

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(30));
            GUILayout.Space(10);
            vector.x = EditorGUILayout.FloatField("X", value.x);
            vector.y = EditorGUILayout.FloatField("Y", value.y);
            vector.z = EditorGUILayout.FloatField("Z", value.z);
            GUILayout.EndHorizontal();

            EditorGUIUtility.labelWidth = defaultLabelWidth;
            return vector;
        }

        private void PathGUI()
        {
            _generator.PipesAmount = EditorGUILayout.IntSlider("Amount", _generator.PipesAmount, 1, 10);

            EditorGUI.BeginChangeCheck();

            var maxIterations = EditorGUILayout.IntField("Max Iterations", _generator.PathCreator.MaxIterations);
            var gridRotation = EditorGUILayout.FloatField("Grid Y Angle", _generator.PathCreator.GridRotationY);
            var pathGridSize = EditorGUILayout.FloatField("Grid Size", _generator.PathCreator.GridSize);
            var pathHeight = EditorGUILayout.FloatField("Height", _generator.PathCreator.Height);
            var chaos = EditorGUILayout.Slider("Chaos", _generator.PathCreator.Chaos, 0, 100);
            var straightPriority = EditorGUILayout.Slider("Straight Proirity", _generator.PathCreator.StraightPathPriority, 0, 100);
            var nearObstaclePriority = EditorGUILayout.Slider("Near Obstacle Proirity", _generator.PathCreator.NearObstaclesPriority, 0, 100);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_generator, "Set Field Value");

                _generator.PathCreator.GridSize = Mathf.Clamp(pathGridSize, _generator.PathCreator.Radius, Mathf.Infinity);
                _generator.PathCreator.GridRotationY = gridRotation;
                _generator.PathCreator.Height = Mathf.Clamp(pathHeight, pathGridSize, Mathf.Infinity);
                _generator.PathCreator.StraightPathPriority = straightPriority;
                _generator.PathCreator.NearObstaclesPriority = nearObstaclePriority;
                _generator.PathCreator.Chaos = chaos;
                _generator.PathCreator.MaxIterations = maxIterations;

                if (_autoRegenerate) RegeneratePaths();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Regenerate")) RegeneratePaths();
            _autoRegenerate = GUILayout.Toggle(_autoRegenerate, "Auto", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            if (_lastBuildFailed)
            {
                EditorGUILayout.HelpBox("Some paths weren't found!", MessageType.Warning);
            }
        }

        private void RegeneratePaths()
        {
            _lastBuildFailed = !_generator.RegeneratePaths();
        }

        private void OnSceneGUI()
        {
            if (_editingMode == 0)
                HandlePathInput(Event.current);
            else if (_editingMode == 1)
                HandleEdit(Event.current);
        }

        private void SetHandlePosition()
        {
            if (_selectedPointsIndexes.Count == 0) return;
            var sum = Vector3.zero;
            _selectedPointsIndexes.ForEach(index => sum += _generator.Pipes[_selectedPipeIndex].Points[index]);
            _positionHandle = sum / _selectedPointsIndexes.Count;
        }

        private void HandleEdit(Event evt)
        {
            Handles.matrix = _generator.transform.localToWorldMatrix;

            for (int i = 0; i < _generator.Pipes.Count; i++)
            {
                for (int j = 0; j < _generator.Pipes[i].Points.Count; j++)
                {
                    Handles.color = (_selectedPipeIndex == i) ? Color.white : new Color(1, 1, 1, 0.4f);
                    if (j != 0)
                    {
                        Handles.DrawLine(_generator.Pipes[i].Points[j], _generator.Pipes[i].Points[j - 1]);
                    }

                    if (_selectedPipeIndex == i && _selectedPointsIndexes.Contains(j)) Handles.color = Color.yellow;
                    if (Handles.Button(_generator.Pipes[i].Points[j], Quaternion.identity, _generator.Radius * 1.5f, _generator.Radius * 2f, Handles.SphereHandleCap))
                    {
                        if (evt.shift && _selectedPipeIndex == i)
                        {
                            if (!_selectedPointsIndexes.Contains(j))
                                _selectedPointsIndexes.Add(j);
                            else
                                _selectedPointsIndexes.Remove(j);
                        }
                        else
                        {
                            _selectedPointsIndexes = new() { j };
                        }

                        _selectedPipeIndex = i;
                        SetHandlePosition();
                        Repaint();
                    }
                }
            }

            if (_selectedPointsIndexes.Count != 0)
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

                EditorGUI.BeginChangeCheck();
                var position = Handles.PositionHandle(_positionHandle, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_generator, "Moved a point");
                    foreach (var index in _selectedPointsIndexes)
                    {
                        _generator.Pipes[_selectedPipeIndex].Points[index] += position - _positionHandle;
                    }
                    _positionHandle = position;
                    _generator.UpdateMesh();
                }

                if (evt.keyCode == KeyCode.A && !evt.control)
                {
                    _selectedPointsIndexes.Clear();
                    for (int i = 0; i < _generator.Pipes[_selectedPipeIndex].Points.Count; i++)
                    {
                        _selectedPointsIndexes.Add(i);
                        Repaint();
                    }
                }
            }
        }

        private void HandlePathInput(Event evt)
        {
            var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            var mouseHit = new RaycastHit();

            if (Physics.Raycast(ray, out var hit, 1000))
            {
                mouseHit = hit;
            }

            if (_isDragging)
            {
                bool canStartAtPoint = !Physics.SphereCast(new Ray(_startDragPoint, _startDragNormal), _generator.PathCreator.Radius, _generator.PathCreator.Height);
                bool canEndAtPoint = !Physics.SphereCast(new Ray(mouseHit.point, mouseHit.normal), _generator.PathCreator.Radius, _generator.PathCreator.Height);
                Handles.color = (canStartAtPoint && canEndAtPoint) ? Color.white : Color.red;
                Handles.DrawDottedLine(_startDragPoint, mouseHit.point, 4);
            }

            if (evt.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && evt.modifiers == EventModifiers.None)
            {
                _isDragging = true;
                _startDragNormal = mouseHit.normal;
                _startDragPoint = mouseHit.point;
            }

            if (_isDragging && evt.type == EventType.MouseUp && evt.button == 0 && evt.modifiers == EventModifiers.None)
            {
                _isDragging = false;

                if (Vector3.Distance(mouseHit.point, _startDragPoint) > _generator.PathCreator.GridSize)
                {
                    Undo.RecordObject(_generator, "Add Pipe");
                    _lastBuildFailed = !_generator.AddPipe(_startDragPoint, _startDragNormal, mouseHit.point, mouseHit.normal);
                }
            }
            Repaint();
        }
    }
}

#endif