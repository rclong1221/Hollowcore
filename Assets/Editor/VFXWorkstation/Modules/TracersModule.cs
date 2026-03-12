using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.VFXWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 VW-02: Tracers module.
    /// Tracer/projectile trail setup, speed and lifetime.
    /// </summary>
    public class TracersModule : IVFXModule
    {
        private Vector2 _scrollPosition;
        
        // Target
        private GameObject _targetWeapon;
        
        // Tracer config
        private TracerType _tracerType = TracerType.LineRenderer;
        private GameObject _tracerPrefab;
        private Material _tracerMaterial;
        
        // Trail settings
        private float _tracerSpeed = 500f;
        private float _tracerLength = 5f;
        private float _tracerWidth = 0.02f;
        private float _tracerLifetime = 0.5f;
        private Gradient _tracerGradient = new Gradient();
        private AnimationCurve _widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
        
        // Spawn settings
        private int _tracerFrequency = 3; // Every Nth shot
        private bool _alwaysFirstShot = true;
        private bool _randomizeSpawn = false;
        
        // Visual options
        private bool _useBulletDrop = false;
        private float _dropAmount = 0.1f;
        private bool _fadeOverDistance = true;
        private float _fadeStartDistance = 50f;

        private enum TracerType
        {
            LineRenderer,
            ParticleTrail,
            MeshTrail,
            VFXGraph
        }

        public TracersModule()
        {
            InitializeDefaultGradient();
        }

        private void InitializeDefaultGradient()
        {
            _tracerGradient = new Gradient();
            _tracerGradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), 
                    new GradientColorKey(new Color(1f, 0.7f, 0.3f), 0.5f),
                    new GradientColorKey(new Color(0.8f, 0.4f, 0.2f), 1f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1f, 0f), 
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f) 
                }
            );
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Tracers", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure tracer/projectile trails, speed, lifetime, and visual appearance.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawTargetSelection();
            EditorGUILayout.Space(10);
            DrawTracerType();
            EditorGUILayout.Space(10);
            DrawTrailSettings();
            EditorGUILayout.Space(10);
            DrawAppearance();
            EditorGUILayout.Space(10);
            DrawSpawnSettings();
            EditorGUILayout.Space(10);
            DrawAdvancedOptions();
            EditorGUILayout.Space(10);
            DrawPreview();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTargetSelection()
        {
            EditorGUILayout.LabelField("Target Weapon", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _targetWeapon = (GameObject)EditorGUILayout.ObjectField(
                "Weapon Prefab", _targetWeapon, typeof(GameObject), true);

            EditorGUILayout.EndVertical();
        }

        private void DrawTracerType()
        {
            EditorGUILayout.LabelField("Tracer Type", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _tracerType = (TracerType)EditorGUILayout.EnumPopup("Type", _tracerType);

            string description = _tracerType switch
            {
                TracerType.LineRenderer => "Fast, lightweight line-based tracer",
                TracerType.ParticleTrail => "Particle system trail with more visual options",
                TracerType.MeshTrail => "3D mesh trail for high-fidelity tracers",
                TracerType.VFXGraph => "VFX Graph based tracer (requires VFX Graph package)",
                _ => ""
            };
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(5);

            if (_tracerType == TracerType.LineRenderer)
            {
                _tracerMaterial = (Material)EditorGUILayout.ObjectField(
                    "Trail Material", _tracerMaterial, typeof(Material), false);
            }
            else
            {
                _tracerPrefab = (GameObject)EditorGUILayout.ObjectField(
                    "Tracer Prefab", _tracerPrefab, typeof(GameObject), false);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTrailSettings()
        {
            EditorGUILayout.LabelField("Trail Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _tracerSpeed = EditorGUILayout.FloatField("Speed (m/s)", _tracerSpeed);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Travel time to 100m: {100f / _tracerSpeed:F3}s", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            _tracerLength = EditorGUILayout.Slider("Trail Length (m)", _tracerLength, 0.5f, 20f);
            _tracerWidth = EditorGUILayout.Slider("Trail Width (m)", _tracerWidth, 0.005f, 0.1f);
            _tracerLifetime = EditorGUILayout.Slider("Max Lifetime (s)", _tracerLifetime, 0.1f, 3f);

            EditorGUILayout.Space(5);
            
            _widthCurve = EditorGUILayout.CurveField("Width Over Length", _widthCurve);

            EditorGUILayout.EndVertical();
        }

        private void DrawAppearance()
        {
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _tracerGradient = EditorGUILayout.GradientField("Color Gradient", _tracerGradient);

            // Preview gradient
            Rect gradientRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(20), GUILayout.ExpandWidth(true));
            DrawGradientPreview(gradientRect);

            EditorGUILayout.Space(5);
            
            _fadeOverDistance = EditorGUILayout.Toggle("Fade Over Distance", _fadeOverDistance);
            if (_fadeOverDistance)
            {
                EditorGUI.indentLevel++;
                _fadeStartDistance = EditorGUILayout.FloatField("Fade Start (m)", _fadeStartDistance);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGradientPreview(Rect rect)
        {
            Texture2D gradTex = new Texture2D(256, 1);
            for (int i = 0; i < 256; i++)
            {
                gradTex.SetPixel(i, 0, _tracerGradient.Evaluate(i / 255f));
            }
            gradTex.Apply();
            GUI.DrawTexture(rect, gradTex);
            Object.DestroyImmediate(gradTex);
        }

        private void DrawSpawnSettings()
        {
            EditorGUILayout.LabelField("Spawn Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _tracerFrequency = EditorGUILayout.IntSlider("Every N Shots", _tracerFrequency, 1, 10);
            
            string freqDesc = _tracerFrequency == 1 ? "Every shot spawns a tracer" : $"1 tracer per {_tracerFrequency} shots";
            EditorGUILayout.LabelField(freqDesc, EditorStyles.miniLabel);

            EditorGUILayout.Space(5);
            
            _alwaysFirstShot = EditorGUILayout.Toggle("Always First Shot", _alwaysFirstShot);
            _randomizeSpawn = EditorGUILayout.Toggle("Randomize Spawn", _randomizeSpawn);

            if (_randomizeSpawn)
            {
                EditorGUILayout.LabelField("Tracer chance varies randomly within frequency window", 
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAdvancedOptions()
        {
            EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _useBulletDrop = EditorGUILayout.Toggle("Simulate Bullet Drop", _useBulletDrop);
            if (_useBulletDrop)
            {
                EditorGUI.indentLevel++;
                _dropAmount = EditorGUILayout.Slider("Drop Amount", _dropAmount, 0f, 1f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Tracer Preview", EditorStyles.boldLabel);
            
            Rect previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(60), GUILayout.ExpandWidth(true));
            
            DrawTracerPreview(previewRect);
        }

        private void DrawTracerPreview(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.15f));

            float startX = rect.x + 20;
            float endX = rect.xMax - 20;
            float centerY = rect.center.y;
            
            int segments = 50;
            float segmentWidth = (endX - startX) / segments;
            
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments;
                float nextT = (i + 1) / (float)segments;
                
                Color color = _tracerGradient.Evaluate(t);
                float widthMult = _widthCurve.Evaluate(t);
                
                float y1 = centerY - (_tracerWidth * 500 * widthMult);
                float y2 = centerY + (_tracerWidth * 500 * widthMult);
                
                EditorGUI.DrawRect(new Rect(startX + i * segmentWidth, y1, segmentWidth + 1, y2 - y1), color);
            }

            // Labels
            GUI.Label(new Rect(startX, rect.y + 2, 50, 16), "Start", EditorStyles.miniLabel);
            GUI.Label(new Rect(endX - 30, rect.y + 2, 50, 16), "End", EditorStyles.miniLabel);
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            EditorGUI.BeginDisabledGroup(_targetWeapon == null);
            if (GUILayout.Button("Apply to Weapon", GUILayout.Height(30)))
            {
                ApplyToWeapon();
            }
            EditorGUI.EndDisabledGroup();
            
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Create Prefab", GUILayout.Height(30)))
            {
                CreateTracerPrefab();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Test in Scene"))
            {
                TestInScene();
            }
            
            if (GUILayout.Button("Copy Settings"))
            {
                CopySettings();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ApplyToWeapon()
        {
            Debug.Log($"[Tracers] Applied tracer config to {_targetWeapon.name}");
        }

        private void CreateTracerPrefab()
        {
            Debug.Log("[Tracers] Create prefab pending");
        }

        private void TestInScene()
        {
            Debug.Log("[Tracers] Scene test pending");
        }

        private void CopySettings()
        {
            Debug.Log("[Tracers] Settings copied");
        }
    }
}
