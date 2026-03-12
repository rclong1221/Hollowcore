#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;

namespace DIG.ProceduralMotion.Editor
{
    /// <summary>
    /// EPIC 15.25 Phase 6: Custom inspector for ProceduralMotionProfile.
    /// Shows a spring response curve preview and organizes the many parameters
    /// into foldable sections.
    /// </summary>
    [CustomEditor(typeof(ProceduralMotionProfile))]
    public class ProceduralMotionProfileEditor : UnityEditor.Editor
    {
        private bool _showSpringPreview = true;
        private bool _showStateOverrides = false;
        private bool _showParadigmWeights = false;
        private float _previewFrequency = 8f;
        private float _previewDamping = 0.7f;

        private static readonly string[] StateNames = {
            "Idle", "Walk", "Sprint", "ADS", "Slide",
            "Vault", "Swim", "Airborne", "Crouch", "Climb", "Staggered"
        };

        private static readonly string[] ParadigmNames = {
            "Shooter", "MMO", "ARPG", "MOBA", "TwinStick", "SideScroller2D"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw default inspector for main parameters
            DrawPropertiesExcluding(serializedObject, "StateOverrides", "ParadigmWeights");

            EditorGUILayout.Space(10);

            // Spring response curve preview
            _showSpringPreview = EditorGUILayout.Foldout(_showSpringPreview, "Spring Response Preview", true);
            if (_showSpringPreview)
            {
                DrawSpringPreview();
            }

            EditorGUILayout.Space(5);

            // State overrides
            _showStateOverrides = EditorGUILayout.Foldout(_showStateOverrides, "Per-State Overrides", true);
            if (_showStateOverrides)
            {
                var stateOverrides = serializedObject.FindProperty("StateOverrides");
                if (stateOverrides != null)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < stateOverrides.arraySize && i < StateNames.Length; i++)
                    {
                        EditorGUILayout.PropertyField(stateOverrides.GetArrayElementAtIndex(i),
                            new GUIContent(StateNames[i]), true);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(5);

            // Paradigm weights
            _showParadigmWeights = EditorGUILayout.Foldout(_showParadigmWeights, "Per-Paradigm Weights", true);
            if (_showParadigmWeights)
            {
                var paradigmWeights = serializedObject.FindProperty("ParadigmWeights");
                if (paradigmWeights != null)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < paradigmWeights.arraySize && i < ParadigmNames.Length; i++)
                    {
                        EditorGUILayout.PropertyField(paradigmWeights.GetArrayElementAtIndex(i),
                            new GUIContent(ParadigmNames[i]), true);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSpringPreview()
        {
            EditorGUI.indentLevel++;
            _previewFrequency = EditorGUILayout.Slider("Preview Frequency (Hz)", _previewFrequency, 1f, 20f);
            _previewDamping = EditorGUILayout.Slider("Preview Damping Ratio", _previewDamping, 0f, 2f);

            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(100));
            if (Event.current.type == EventType.Repaint)
            {
                DrawSpringCurve(rect, _previewFrequency, _previewDamping);
            }
            EditorGUI.indentLevel--;
        }

        private void DrawSpringCurve(Rect rect, float freq, float zeta)
        {
            // Background
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

            // Zero line
            float midY = rect.y + rect.height * 0.3f; // Offset to show overshoot
            EditorGUI.DrawRect(new Rect(rect.x, midY, rect.width, 1), new Color(0.4f, 0.4f, 0.4f));

            // Simulate spring response to unit impulse
            float value = 0f;
            float velocity = 1f; // Unit impulse
            float totalTime = 2f;
            int steps = (int)rect.width;
            float stepDt = totalTime / steps;

            Vector3 prevPoint = Vector3.zero;
            float omega = 2f * Mathf.PI * freq;

            for (int i = 0; i < steps; i++)
            {
                float t_step = stepDt;
                // Analytical solve
                if (zeta < 0.999f)
                {
                    float zc = Mathf.Max(zeta, 0.001f);
                    float omega_d = omega * Mathf.Sqrt(1f - zc * zc);
                    float e = Mathf.Exp(-zc * omega * t_step);
                    float c = Mathf.Cos(omega_d * t_step);
                    float s = Mathf.Sin(omega_d * t_step);
                    float nv = e * (value * c + (velocity + zc * omega * value) / omega_d * s);
                    float nvel = e * (velocity * (c - zc * omega / omega_d * s) - value * omega * omega / omega_d * s);
                    value = nv;
                    velocity = nvel;
                }
                else if (zeta < 1.001f)
                {
                    float e = Mathf.Exp(-omega * t_step);
                    float nv = e * (value * (1f + omega * t_step) + velocity * t_step);
                    float nvel = e * (velocity * (1f - omega * t_step) - value * omega * omega * t_step);
                    value = nv;
                    velocity = nvel;
                }
                else
                {
                    float sq = Mathf.Sqrt(zeta * zeta - 1f);
                    float r1 = -omega * (zeta - sq);
                    float r2 = -omega * (zeta + sq);
                    float denom = r1 - r2;
                    if (Mathf.Abs(denom) < 0.0001f) denom = 0.0001f;
                    float c1 = (velocity - r2 * value) / denom;
                    float c2 = value - c1;
                    float nv = c1 * Mathf.Exp(r1 * t_step) + c2 * Mathf.Exp(r2 * t_step);
                    float nvel = c1 * r1 * Mathf.Exp(r1 * t_step) + c2 * r2 * Mathf.Exp(r2 * t_step);
                    value = nv;
                    velocity = nvel;
                }

                float x = rect.x + i;
                float y = midY - value * rect.height * 0.4f;
                y = Mathf.Clamp(y, rect.y, rect.yMax);

                var point = new Vector3(x, y, 0);
                if (i > 0)
                {
                    Handles.color = new Color(0.3f, 0.8f, 1f);
                    Handles.DrawLine(prevPoint, point);
                }
                prevPoint = point;
            }

            // Label
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } };
            GUI.Label(new Rect(rect.x + 4, rect.y + 2, 200, 16),
                $"f={freq:F1}Hz  z={zeta:F2}  {(zeta < 1 ? "Underdamped" : zeta < 1.01f ? "Critical" : "Overdamped")}",
                style);
        }
    }
}
#endif
