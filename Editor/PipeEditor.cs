using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace InstantPipes
{
    [CustomEditor(typeof(PipeGenerator))]
    public class PipeEditor : Editor
    {
        private PipeGenerator _generator;

        private static bool _autoRegenerate = true;
        private static bool _previewPath = false;
        private static bool _settingsFoldoutActive = true;
        private static int _editingMode = 0;

        private Vector3 _startDragPoint;
        private Vector3 _startDragNormal;
        private bool _isDragging = false;
        private bool _lastBuildFailed = false;

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
            if (_generator.transform.position != Vector3.zero)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("The Generator position should be set to (0,0,0)!", MessageType.Warning);
                var buttonRect = GUILayoutUtility.GetLastRect();
                buttonRect.x = buttonRect.width + buttonRect.x - 37;
                buttonRect.width = 30;
                buttonRect.y = buttonRect.y + 7;
                buttonRect.height = 24;
                if (GUI.Button(buttonRect, "Fix")) _generator.transform.position = Vector3.zero;
            }

            EditorGUILayout.Space(5);
            _editingMode = GUILayout.Toolbar(_editingMode, new string[] { "Create", "Edit" });
            EditorGUILayout.Space(5);

            if (_editingMode == 0) PathGUI();
            if (_editingMode == 1) EditGUI();

            EditorGUILayout.Space(5);

            _settingsFoldoutActive = EditorGUILayout.BeginFoldoutHeaderGroup(_settingsFoldoutActive, "Settings");
            if (_settingsFoldoutActive) SettingsGUI();
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void SettingsGUI()
        {
            EditorGUI.BeginChangeCheck();
            var radius = EditorGUILayout.FloatField("Radius", _generator.Radius);
            var curvature = EditorGUILayout.Slider("Curvature", _generator.Curvature, 0.01f, _generator.MaxCurvature);
            var material = (Material)EditorGUILayout.ObjectField("Material", _generator.Material, typeof(Material), false);

            GUILayout.BeginHorizontal();
            var isSeparateRingsMaterial = EditorGUILayout.ToggleLeft("Ring Material", _generator.IsSeparateRingsMaterial, GUILayout.Width(EditorGUIUtility.labelWidth));
            EditorGUI.BeginDisabledGroup(!isSeparateRingsMaterial);
            var ringMaterial = (Material)EditorGUILayout.ObjectField(_generator.RingMaterial, typeof(Material), false);
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();
            var ringsUVScale = EditorGUILayout.FloatField("Rings UV Scale", _generator.RingsUVScale);
            EditorGUILayout.Space(10);

            var hasRings = EditorGUILayout.ToggleLeft("Rings", _generator.HasRings, EditorStyles.boldLabel);
            float ringRadius = _generator.RingRadius, ringThickness = _generator.RingThickness;
            bool hasExtrusion = _generator.HasExtrusion;
            if (_generator.HasRings)
            {
                hasExtrusion = EditorGUILayout.Toggle("Extrusion", _generator.HasExtrusion);
                ringRadius = EditorGUILayout.Slider("Radius", _generator.RingRadius, 0, radius);
                EditorGUI.BeginDisabledGroup(hasExtrusion);
                ringThickness = EditorGUILayout.Slider("Thickness", _generator.RingThickness, 0, radius);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.Space(10);
            }

            var hasCaps = EditorGUILayout.ToggleLeft("End Caps", _generator.HasCaps, EditorStyles.boldLabel);
            var maxEndCapOffset = _generator.PathCreator.Height - _generator.CapThickness - _generator.Radius;
            float capRadius = _generator.CapRadius, capThickness = _generator.CapThickness, capOffset = _generator.CapOffset;
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

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_generator, "Set Field Value");

                _generator.Material = material;
                _generator.IsSeparateRingsMaterial = isSeparateRingsMaterial;
                _generator.RingMaterial = ringMaterial;
                _generator.RingsUVScale = Mathf.Max(0.01f, ringsUVScale);
                _generator.Radius = Mathf.Max(0.01f, radius);
                _generator.Curvature = Mathf.Clamp(curvature, 0.01f, _generator.MaxCurvature);
                _generator.HasRings = hasRings;
                _generator.HasExtrusion = hasExtrusion;
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

            if (GUILayout.Button("Erase Everything"))
            {
                Undo.RecordObject(_generator, "Erased all Pipes");
                _generator.Pipes.Clear();
                _generator.UpdateMesh();
            }
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
            _previewPath = EditorGUILayout.Toggle("Preview Path", _previewPath);
            _generator.PipesAmount = EditorGUILayout.IntSlider("Amount", _generator.PipesAmount, 1, 10);

            EditorGUI.BeginChangeCheck();

            var maxIterations = EditorGUILayout.IntField("Max Iterations", _generator.PathCreator.MaxIterations);
            var pathGridSize = EditorGUILayout.FloatField("Grid Size", _generator.PathCreator.GridSize);
            var gridRotation = EditorGUILayout.FloatField("Grid Y Angle", _generator.PathCreator.GridRotationY);
            var pathHeight = EditorGUILayout.FloatField("Height", _generator.PathCreator.Height);
            var chaos = EditorGUILayout.Slider("Chaos", _generator.PathCreator.Chaos, 0, 100);
            var straightPriority = EditorGUILayout.Slider("Straight Priority", _generator.PathCreator.StraightPathPriority, 0, 100);
            var nearObstaclePriority = EditorGUILayout.Slider("Near Obstacle Priority", _generator.PathCreator.NearObstaclesPriority, 0, 100);

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
            if (_editingMode == 1)
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

                if (evt.keyCode == KeyCode.A && !evt.control && Tools.viewTool != ViewTool.FPS)
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
                Handles.color = (canStartAtPoint && canEndAtPoint) ? Color.blue : Color.red;

                if (_previewPath)
                {
                    var vectorLength = _generator.PipesAmount * _generator.Radius + (_generator.PipesAmount - 1) * _generator.PathCreator.GridSize;
                    var startVector = Vector3.Cross(_startDragNormal, mouseHit.point - _startDragPoint).normalized * vectorLength;
                    var endVector = Vector3.Cross(mouseHit.normal, mouseHit.point - _startDragPoint).normalized * vectorLength;
                    var stepSize = startVector.magnitude / (_generator.PipesAmount);
                    for (int i = 0; i < _generator.PipesAmount; i++)
                    {
                        var start = startVector.normalized * (stepSize * (i - _generator.PipesAmount / 2f + 0.5f));
                        var end = endVector.normalized * (stepSize * (i - _generator.PipesAmount / 2f + 0.5f));
                        var points = _generator.PathCreator.Create(_startDragPoint + start, _startDragNormal, mouseHit.point + end, mouseHit.normal);
                        for (int j = 1; j < points.Count; j++) Handles.DrawLine(points[j], points[j - 1], 10);
                    }
                }
                else
                {
                    Handles.DrawLine(_startDragPoint, mouseHit.point, 2);
                }
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