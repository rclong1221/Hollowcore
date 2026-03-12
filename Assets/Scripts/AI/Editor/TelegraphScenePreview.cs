#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using DIG.AI.Authoring;
using DIG.AI.Components;

namespace DIG.AI.Editor
{
    /// <summary>
    /// EPIC 15.32: Scene view telegraph wireframe preview.
    /// When an AbilityDefinitionSO with a telegraph shape is selected in the Inspector,
    /// draws a wireframe preview at the selected GameObject's position in the Scene view.
    /// </summary>
    [InitializeOnLoad]
    public static class TelegraphScenePreview
    {
        private static AbilityDefinitionSO _cachedAbility;

        static TelegraphScenePreview()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
        }

        private static void OnSelectionChanged()
        {
            _cachedAbility = Selection.activeObject as AbilityDefinitionSO;
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (_cachedAbility == null) return;
            if (_cachedAbility.TelegraphShape == TelegraphShape.None) return;

            // Use the selected GameObject as the origin, or scene view center
            Vector3 origin;
            Quaternion rotation;

            var selectedGO = Selection.activeGameObject;
            if (selectedGO != null)
            {
                origin = selectedGO.transform.position;
                rotation = selectedGO.transform.rotation;
            }
            else
            {
                origin = view.pivot;
                rotation = Quaternion.identity;
            }

            // Semi-transparent red for telegraph area
            var fillColor = new Color(1f, 0.2f, 0.1f, 0.15f);
            var wireColor = new Color(1f, 0.3f, 0.1f, 0.8f);

            Handles.color = wireColor;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            switch (_cachedAbility.TelegraphShape)
            {
                case TelegraphShape.Circle:
                    DrawCircleTelegraph(origin, _cachedAbility.Radius, fillColor, wireColor);
                    break;
                case TelegraphShape.Cone:
                    DrawConeTelegraph(origin, rotation, _cachedAbility.Radius, _cachedAbility.Angle, fillColor, wireColor);
                    break;
                case TelegraphShape.Line:
                    DrawLineTelegraph(origin, rotation, _cachedAbility.Range, _cachedAbility.Radius, fillColor, wireColor);
                    break;
                case TelegraphShape.Ring:
                    DrawRingTelegraph(origin, _cachedAbility.Radius, _cachedAbility.Radius * 0.5f, fillColor, wireColor);
                    break;
                case TelegraphShape.Cross:
                    DrawCrossTelegraph(origin, rotation, _cachedAbility.Range, _cachedAbility.Radius, fillColor, wireColor);
                    break;
            }

            // Label
            Handles.color = Color.white;
            Handles.Label(origin + Vector3.up * 2.5f,
                $"{_cachedAbility.AbilityName}\n{_cachedAbility.TelegraphShape} R:{_cachedAbility.Radius:F1}m",
                EditorStyles.whiteBoldLabel);
        }

        private static void DrawCircleTelegraph(Vector3 center, float radius, Color fill, Color wire)
        {
            Handles.color = fill;
            Handles.DrawSolidDisc(center + Vector3.up * 0.05f, Vector3.up, radius);
            Handles.color = wire;
            Handles.DrawWireDisc(center + Vector3.up * 0.05f, Vector3.up, radius);

            // Range indicator
            Handles.color = new Color(1, 1, 1, 0.3f);
            DrawGroundGrid(center, radius);
        }

        private static void DrawConeTelegraph(Vector3 origin, Quaternion rotation, float radius, float angle, Color fill, Color wire)
        {
            Vector3 forward = rotation * Vector3.forward;
            float halfAngle = angle * 0.5f;

            // Cone edges
            Handles.color = wire;
            var leftDir = Quaternion.Euler(0, -halfAngle, 0) * forward;
            var rightDir = Quaternion.Euler(0, halfAngle, 0) * forward;

            Vector3 leftEnd = origin + leftDir * radius;
            Vector3 rightEnd = origin + rightDir * radius;

            Handles.DrawLine(origin, leftEnd);
            Handles.DrawLine(origin, rightEnd);

            // Arc
            Handles.DrawWireArc(origin + Vector3.up * 0.05f, Vector3.up, leftDir, angle, radius);

            // Filled arc
            Handles.color = fill;
            Handles.DrawSolidArc(origin + Vector3.up * 0.05f, Vector3.up, leftDir, angle, radius);
        }

        private static void DrawLineTelegraph(Vector3 origin, Quaternion rotation, float length, float width, Color fill, Color wire)
        {
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            float halfWidth = width * 0.5f;

            Vector3[] corners =
            {
                origin + right * halfWidth,
                origin - right * halfWidth,
                origin - right * halfWidth + forward * length,
                origin + right * halfWidth + forward * length
            };

            // Lift slightly off ground
            for (int i = 0; i < corners.Length; i++)
                corners[i].y += 0.05f;

            // Fill
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(corners);

            // Wire
            Handles.color = wire;
            Handles.DrawLine(corners[0], corners[1]);
            Handles.DrawLine(corners[1], corners[2]);
            Handles.DrawLine(corners[2], corners[3]);
            Handles.DrawLine(corners[3], corners[0]);
        }

        private static void DrawRingTelegraph(Vector3 center, float outerRadius, float innerRadius, Color fill, Color wire)
        {
            // Outer circle
            Handles.color = fill;
            Handles.DrawSolidDisc(center + Vector3.up * 0.05f, Vector3.up, outerRadius);

            // Inner circle (cut out via darker fill)
            Handles.color = new Color(0.15f, 0.15f, 0.15f, 0.3f);
            Handles.DrawSolidDisc(center + Vector3.up * 0.06f, Vector3.up, innerRadius);

            // Wire outlines
            Handles.color = wire;
            Handles.DrawWireDisc(center + Vector3.up * 0.05f, Vector3.up, outerRadius);
            Handles.DrawWireDisc(center + Vector3.up * 0.05f, Vector3.up, innerRadius);
        }

        private static void DrawCrossTelegraph(Vector3 center, Quaternion rotation, float length, float width, Color fill, Color wire)
        {
            // Two perpendicular lines through center
            DrawLineTelegraph(center - rotation * Vector3.forward * length * 0.5f,
                rotation, length, width, fill, wire);

            var rot90 = rotation * Quaternion.Euler(0, 90, 0);
            DrawLineTelegraph(center - rot90 * Vector3.forward * length * 0.5f,
                rot90, length, width, fill, wire);
        }

        private static void DrawGroundGrid(Vector3 center, float radius)
        {
            // Simple distance rings for reference
            Handles.color = new Color(1, 1, 1, 0.1f);
            float step = Mathf.Max(1f, Mathf.Floor(radius / 3f));
            for (float r = step; r < radius; r += step)
            {
                Handles.DrawWireDisc(center + Vector3.up * 0.02f, Vector3.up, r);
            }
        }
    }
}
#endif
