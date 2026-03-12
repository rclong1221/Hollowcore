using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.SceneManagement.Editor.Modules
{
    /// <summary>
    /// EPIC 18.6: Visual state machine editor for GameFlowDefinitionSO.
    /// States rendered as boxes, transitions as arrows, click to inspect.
    /// </summary>
    public class FlowGraphModule : ISceneModule
    {
        private GameFlowDefinitionSO _flowDef;
        private Vector2 _scrollPos;
        private Vector2 _panOffset;
        private int _selectedStateIndex = -1;
        private bool _dragging;
        private Vector2 _dragStart;

        // Layout
        private const float NodeWidth = 140f;
        private const float NodeHeight = 60f;
        private const float NodeSpacingX = 200f;
        private const float NodeSpacingY = 120f;

        private static readonly Color ColorDefault = new(0.25f, 0.25f, 0.3f, 1f);
        private static readonly Color ColorSelected = new(0.3f, 0.5f, 0.8f, 1f);
        private static readonly Color ColorNetwork = new(0.6f, 0.3f, 0.3f, 1f);
        private static readonly Color ColorInitial = new(0.2f, 0.6f, 0.3f, 1f);

        private readonly Dictionary<string, int> _stateIndexMap = new();
        private int _stateIndexMapVersion = -1;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Flow Graph", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _flowDef = (GameFlowDefinitionSO)EditorGUILayout.ObjectField(
                "Flow Definition", _flowDef, typeof(GameFlowDefinitionSO), false);

            if (_flowDef == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a GameFlowDefinitionSO to visualize the state machine.\n" +
                    "Create one via: Create > DIG > Scene Management > Game Flow Definition",
                    MessageType.Info);
                return;
            }

            if (_flowDef.States == null || _flowDef.States.Length == 0)
            {
                EditorGUILayout.HelpBox("No states defined in this flow definition.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4);

            // Canvas area
            var canvasRect = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (canvasRect.width < 10 || canvasRect.height < 10) return;

            GUI.Box(canvasRect, GUIContent.none, EditorStyles.helpBox);

            // Handle pan input
            HandleCanvasInput(canvasRect);

            // Draw transitions (arrows) first
            DrawTransitions(canvasRect);

            // Draw state nodes
            DrawNodes(canvasRect);

            // Selected state info
            if (_selectedStateIndex >= 0 && _selectedStateIndex < _flowDef.States.Length)
            {
                EditorGUILayout.Space(8);
                DrawStateInspector(_flowDef.States[_selectedStateIndex]);
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void HandleCanvasInput(Rect canvasRect)
        {
            var e = Event.current;
            if (!canvasRect.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDown && e.button == 2) // Middle click to pan
            {
                _dragging = true;
                _dragStart = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _dragging)
            {
                _panOffset += e.mousePosition - _dragStart;
                _dragStart = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 2)
            {
                _dragging = false;
                e.Use();
            }
        }

        private Rect GetNodeRect(int index, Rect canvas)
        {
            int cols = Mathf.Max(1, (int)(canvas.width / NodeSpacingX));
            int row = index / cols;
            int col = index % cols;

            float x = canvas.x + 20 + col * NodeSpacingX + _panOffset.x;
            float y = canvas.y + 20 + row * NodeSpacingY + _panOffset.y;

            return new Rect(x, y, NodeWidth, NodeHeight);
        }

        private void DrawNodes(Rect canvas)
        {
            for (int i = 0; i < _flowDef.States.Length; i++)
            {
                var state = _flowDef.States[i];
                var rect = GetNodeRect(i, canvas);

                // Determine node color
                Color nodeColor = ColorDefault;
                if (i == _selectedStateIndex)
                    nodeColor = ColorSelected;
                else if (state.StateId == _flowDef.InitialState)
                    nodeColor = ColorInitial;
                else if (state.RequiresNetwork)
                    nodeColor = ColorNetwork;

                EditorGUI.DrawRect(rect, nodeColor);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), Color.white);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), Color.white);
                EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), Color.white);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), Color.white);

                // State label
                var labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                GUI.Label(rect, state.StateId ?? "(unnamed)", labelStyle);

                // Subtitle
                string subtitle = state.RequiresNetwork ? "[Network]" : state.Scene?.SceneId ?? "";
                var subStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.LowerCenter,
                    normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 0.7f) }
                };
                GUI.Label(new Rect(rect.x, rect.y + 8, rect.width, rect.height), subtitle, subStyle);

                // Click to select
                if (Event.current.type == EventType.MouseDown &&
                    Event.current.button == 0 &&
                    rect.Contains(Event.current.mousePosition))
                {
                    _selectedStateIndex = i;
                    Event.current.Use();
                }
            }
        }

        private void DrawTransitions(Rect canvas)
        {
            if (_flowDef.Transitions == null) return;

            int version = (_flowDef.States?.Length ?? 0) * 31 + (_flowDef.Transitions?.Length ?? 0);
            if (_stateIndexMapVersion != version)
            {
                _stateIndexMapVersion = version;
                _stateIndexMap.Clear();
                if (_flowDef.States != null)
                {
                    for (int i = 0; i < _flowDef.States.Length; i++)
                        _stateIndexMap[_flowDef.States[i].StateId] = i;
                }
            }

            Handles.BeginGUI();
            foreach (var t in _flowDef.Transitions)
            {
                if (!_stateIndexMap.TryGetValue(t.FromState, out int fromIdx)) continue;
                if (!_stateIndexMap.TryGetValue(t.ToState, out int toIdx)) continue;

                var fromRect = GetNodeRect(fromIdx, canvas);
                var toRect = GetNodeRect(toIdx, canvas);

                var start = new Vector3(fromRect.center.x, fromRect.yMax, 0);
                var end = new Vector3(toRect.center.x, toRect.y, 0);

                Handles.color = new Color(0.7f, 0.7f, 0.3f, 0.8f);
                Handles.DrawLine(start, end);

                // Arrow head
                var dir = (end - start).normalized;
                var arrowSize = 8f;
                var arrowBase = end - dir * arrowSize;
                var perp = new Vector3(-dir.y, dir.x, 0) * arrowSize * 0.5f;
                Handles.DrawAAConvexPolygon(end, arrowBase + perp, arrowBase - perp);
            }
            Handles.EndGUI();
        }

        private void DrawStateInspector(GameFlowState state)
        {
            EditorGUILayout.LabelField("Selected State", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("State ID", state.StateId ?? "(null)");
            EditorGUILayout.LabelField("Scene", state.Scene != null ? state.Scene.SceneId : "(none)");
            EditorGUILayout.LabelField("Requires Network", state.RequiresNetwork.ToString());
            EditorGUILayout.LabelField("Input Context", state.InputContext.ToString());
            EditorGUILayout.LabelField("Additive Scenes",
                state.AdditiveScenes != null ? state.AdditiveScenes.Length.ToString() : "0");
            EditorGUILayout.LabelField("Loading Screen",
                state.LoadingScreen != null ? state.LoadingScreen.name : "(default)");
            EditorGUILayout.LabelField("OnEnter Event", state.OnEnterEvent ?? "(none)");
            EditorGUILayout.LabelField("OnExit Event", state.OnExitEvent ?? "(none)");

            EditorGUI.indentLevel--;
        }
    }
}
