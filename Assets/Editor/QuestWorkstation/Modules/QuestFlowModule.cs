using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Quest.Editor.Modules
{
    /// <summary>
    /// EPIC 16.12: Visual prerequisite graph — shows quest dependency flow.
    /// Nodes as colored rects, connections via Handles.DrawBezier.
    /// </summary>
    public class QuestFlowModule : IQuestModule
    {
        private QuestDatabaseSO _database;
        private Vector2 _scrollOffset;
        private float _zoom = 1f;
        private Dictionary<int, Rect> _nodePositions = new();
        private const float NodeWidth = 160f;
        private const float NodeHeight = 50f;
        private const float HSpacing = 200f;
        private const float VSpacing = 80f;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Quest Flow Viewer", EditorStyles.boldLabel);

            _database = (QuestDatabaseSO)EditorGUILayout.ObjectField("Database", _database, typeof(QuestDatabaseSO), false);
            if (_database == null)
            {
                _database = Resources.Load<QuestDatabaseSO>("QuestDatabase");
                if (_database == null)
                {
                    EditorGUILayout.HelpBox("No QuestDatabaseSO found.", MessageType.Info);
                    return;
                }
            }

            EditorGUILayout.BeginHorizontal();
            _zoom = EditorGUILayout.Slider("Zoom", _zoom, 0.25f, 2f);
            if (GUILayout.Button("Reset View", GUILayout.Width(80)))
            {
                _scrollOffset = Vector2.zero;
                _zoom = 1f;
            }
            EditorGUILayout.EndHorizontal();

            // Build layout if needed
            if (_nodePositions.Count != _database.Quests.Count)
                BuildLayout();

            // Draw flow area
            var area = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (area.height < 100) area.height = 400;

            GUI.BeginGroup(area);

            // Draw connections first (behind nodes)
            foreach (var quest in _database.Quests)
            {
                if (quest == null) continue;
                if (quest.PrerequisiteQuestIds == null) continue;

                foreach (var prereqId in quest.PrerequisiteQuestIds)
                {
                    if (!_nodePositions.TryGetValue(prereqId, out var fromRect)) continue;
                    if (!_nodePositions.TryGetValue(quest.QuestId, out var toRect)) continue;

                    var from = new Vector3(
                        (fromRect.xMax + _scrollOffset.x) * _zoom,
                        (fromRect.center.y + _scrollOffset.y) * _zoom, 0);
                    var to = new Vector3(
                        (toRect.xMin + _scrollOffset.x) * _zoom,
                        (toRect.center.y + _scrollOffset.y) * _zoom, 0);

                    Handles.DrawBezier(from, to,
                        from + Vector3.right * 40 * _zoom, to + Vector3.left * 40 * _zoom,
                        Color.gray, null, 2f);
                }
            }

            // Draw nodes
            foreach (var quest in _database.Quests)
            {
                if (quest == null) continue;
                if (!_nodePositions.TryGetValue(quest.QuestId, out var rect)) continue;

                var scaledRect = new Rect(
                    (rect.x + _scrollOffset.x) * _zoom,
                    (rect.y + _scrollOffset.y) * _zoom,
                    rect.width * _zoom,
                    rect.height * _zoom);

                var color = GetCategoryColor(quest.Category);
                EditorGUI.DrawRect(scaledRect, color);
                EditorGUI.DrawRect(new Rect(scaledRect.x, scaledRect.y, scaledRect.width, 1), Color.black);
                EditorGUI.DrawRect(new Rect(scaledRect.x, scaledRect.yMax - 1, scaledRect.width, 1), Color.black);
                EditorGUI.DrawRect(new Rect(scaledRect.x, scaledRect.y, 1, scaledRect.height), Color.black);
                EditorGUI.DrawRect(new Rect(scaledRect.xMax - 1, scaledRect.y, 1, scaledRect.height), Color.black);

                var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                GUI.Label(scaledRect, $"[{quest.QuestId}]\n{quest.DisplayName}", style);
            }

            GUI.EndGroup();

            // Handle pan
            if (Event.current.type == EventType.MouseDrag && Event.current.button == 2)
            {
                _scrollOffset += Event.current.delta / _zoom;
                Event.current.Use();
            }
        }

        public void OnSceneGUI(UnityEditor.SceneView sceneView) { }

        private void BuildLayout()
        {
            _nodePositions.Clear();
            if (_database == null) return;

            // Topological sort by prerequisites (depth-first)
            var depths = new Dictionary<int, int>();
            foreach (var quest in _database.Quests)
            {
                if (quest == null) continue;
                ComputeDepth(quest.QuestId, depths, 0);
            }

            // Group by depth
            var columns = new Dictionary<int, List<int>>();
            foreach (var kvp in depths)
            {
                if (!columns.ContainsKey(kvp.Value))
                    columns[kvp.Value] = new List<int>();
                columns[kvp.Value].Add(kvp.Key);
            }

            // Position nodes
            foreach (var kvp in columns)
            {
                int col = kvp.Key;
                var ids = kvp.Value;
                for (int row = 0; row < ids.Count; row++)
                {
                    _nodePositions[ids[row]] = new Rect(
                        50 + col * HSpacing,
                        50 + row * VSpacing,
                        NodeWidth,
                        NodeHeight);
                }
            }
        }

        private int ComputeDepth(int questId, Dictionary<int, int> depths, int current)
        {
            if (depths.TryGetValue(questId, out var existing))
                return System.Math.Max(existing, current);

            depths[questId] = current;

            var def = _database.GetQuest(questId);
            if (def?.PrerequisiteQuestIds == null) return current;

            int maxPrereqDepth = 0;
            foreach (var prereqId in def.PrerequisiteQuestIds)
                maxPrereqDepth = System.Math.Max(maxPrereqDepth, ComputeDepth(prereqId, depths, 0) + 1);

            depths[questId] = System.Math.Max(current, maxPrereqDepth);
            return depths[questId];
        }

        private static Color GetCategoryColor(QuestCategory cat) => cat switch
        {
            QuestCategory.Main => new Color(1f, 0.85f, 0.4f, 0.8f),
            QuestCategory.Side => new Color(0.5f, 0.75f, 1f, 0.8f),
            QuestCategory.Daily => new Color(0.5f, 1f, 0.5f, 0.8f),
            QuestCategory.Event => new Color(1f, 0.5f, 0.5f, 0.8f),
            QuestCategory.Tutorial => new Color(0.85f, 0.85f, 0.85f, 0.8f),
            _ => new Color(0.8f, 0.8f, 0.8f, 0.8f)
        };
    }
}
