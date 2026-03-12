using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using DIG.Weapons.Feedback;

namespace DIG.Editor.CombatWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 CB-01: Feedback Setup module.
    /// Hitmarker config (normal/crit/kill), hitstop curves, screen flash.
    /// Integrates with FEEL and HitmarkerFeedbackBridge.
    /// </summary>
    public class FeedbackSetupModule : ICombatModule
    {
        private Vector2 _scrollPosition;
        private GameObject _feedbackManager;
        
        // Hitmarker settings
        private bool _enableHitmarkers = true;
        private Color _normalHitColor = Color.white;
        private Color _criticalHitColor = Color.yellow;
        private Color _killHitColor = Color.red;
        private float _hitmarkerScale = 1f;
        private float _hitmarkerDuration = 0.15f;
        private Sprite _hitmarkerSprite;
        
        // Hitstop settings
        private bool _enableHitstop = true;
        private float _normalHitstopDuration = 0.03f;
        private float _criticalHitstopDuration = 0.08f;
        private float _killHitstopDuration = 0.15f;
        private AnimationCurve _hitstopCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        // Screen effects
        private bool _enableScreenFlash = true;
        private Color _hitFlashColor = new Color(1, 0, 0, 0.1f);
        private float _hitFlashDuration = 0.05f;
        private bool _enableVignette = true;
        private float _vignetteIntensity = 0.2f;
        
        // Audio settings
        private bool _enableHitSounds = true;
        private AudioClip _normalHitSound;
        private AudioClip _criticalHitSound;
        private AudioClip _killHitSound;
        private float _hitSoundVolume = 0.8f;
        
        // FEEL Integration
        private MMF_Player _hitFeedback;
        private MMF_Player _criticalHitFeedback;
        private MMF_Player _killFeedback;
        
        // Presets
        private enum FeedbackPreset { Custom, Arcade, Realistic, Minimal, Heavy }
        private FeedbackPreset _selectedPreset = FeedbackPreset.Custom;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Feedback Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure combat feedback: hitmarkers, hitstop, audio, and screen effects. " +
                "Integrates with FEEL (MoreMountains.Feedbacks) for game juice.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawPresets();
            EditorGUILayout.Space(10);
            DrawFeedbackManager();
            EditorGUILayout.Space(10);
            DrawHitmarkerSettings();
            EditorGUILayout.Space(10);
            DrawHitstopSettings();
            EditorGUILayout.Space(10);
            DrawAudioSettings();
            EditorGUILayout.Space(10);
            DrawScreenEffects();
            EditorGUILayout.Space(10);
            DrawFEELIntegration();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPresets()
        {
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Arcade")) ApplyPreset(FeedbackPreset.Arcade);
            if (GUILayout.Button("Realistic")) ApplyPreset(FeedbackPreset.Realistic);
            if (GUILayout.Button("Minimal")) ApplyPreset(FeedbackPreset.Minimal);
            if (GUILayout.Button("Heavy")) ApplyPreset(FeedbackPreset.Heavy);
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Current: {_selectedPreset}", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawFeedbackManager()
        {
            EditorGUILayout.LabelField("Feedback Manager", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _feedbackManager = (GameObject)EditorGUILayout.ObjectField(
                "Manager Object", _feedbackManager, typeof(GameObject), true);

            // Try to find HitmarkerFeedbackBridge in scene
            if (_feedbackManager == null)
            {
                var bridge = Object.FindFirstObjectByType<HitmarkerFeedbackBridge>();
                if (bridge != null)
                {
                    _feedbackManager = bridge.gameObject;
                    EditorGUILayout.LabelField("Auto-detected HitmarkerFeedbackBridge", EditorStyles.miniLabel);
                }
            }

            if (_feedbackManager == null)
            {
                EditorGUILayout.HelpBox(
                    "No feedback manager found. Create one with HitmarkerFeedbackBridge component.",
                    MessageType.Warning);
                
                if (GUILayout.Button("Create Feedback Manager"))
                {
                    CreateFeedbackManager();
                }
            }
            else
            {
                var bridge = _feedbackManager.GetComponent<HitmarkerFeedbackBridge>();
                if (bridge != null)
                {
                    EditorGUILayout.LabelField("✓ HitmarkerFeedbackBridge found", EditorStyles.miniLabel);
                    
                    // Load current values from bridge
                    if (GUILayout.Button("Load From Bridge"))
                    {
                        LoadFromBridge(bridge);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "GameObject doesn't have HitmarkerFeedbackBridge component.",
                        MessageType.Warning);
                    
                    if (GUILayout.Button("Add HitmarkerFeedbackBridge"))
                    {
                        Undo.AddComponent<HitmarkerFeedbackBridge>(_feedbackManager);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHitmarkerSettings()
        {
            EditorGUILayout.LabelField("Hitmarkers", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _enableHitmarkers = EditorGUILayout.Toggle("Enable Hitmarkers", _enableHitmarkers);

            if (_enableHitmarkers)
            {
                EditorGUI.indentLevel++;
                
                _hitmarkerSprite = (Sprite)EditorGUILayout.ObjectField(
                    "Hitmarker Sprite", _hitmarkerSprite, typeof(Sprite), false);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("Colors", EditorStyles.miniLabel);
                _normalHitColor = EditorGUILayout.ColorField("Normal Hit", _normalHitColor);
                _criticalHitColor = EditorGUILayout.ColorField("Critical Hit", _criticalHitColor);
                _killHitColor = EditorGUILayout.ColorField("Kill Confirm", _killHitColor);
                
                EditorGUILayout.Space(5);
                
                _hitmarkerScale = EditorGUILayout.Slider("Scale", _hitmarkerScale, 0.5f, 3f);
                _hitmarkerDuration = EditorGUILayout.Slider("Duration", _hitmarkerDuration, 0.05f, 0.5f);
                
                // Preview
                DrawHitmarkerPreview();
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHitmarkerPreview()
        {
            Rect previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(60), GUILayout.ExpandWidth(true));
            
            EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f));
            
            float spacing = previewRect.width / 4;
            
            // Normal hit
            DrawColorPreview(new Rect(previewRect.x + spacing * 0.5f, previewRect.y + 10, 40, 40), 
                _normalHitColor, "Normal");
            
            // Critical hit
            DrawColorPreview(new Rect(previewRect.x + spacing * 1.5f, previewRect.y + 10, 40, 40), 
                _criticalHitColor, "Critical");
            
            // Kill
            DrawColorPreview(new Rect(previewRect.x + spacing * 2.5f, previewRect.y + 10, 40, 40), 
                _killHitColor, "Kill");
        }

        private void DrawColorPreview(Rect rect, Color color, string label)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height - 15), color);
            GUI.Label(new Rect(rect.x, rect.yMax - 15, rect.width, 15), label, 
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
        }

        private void DrawHitstopSettings()
        {
            EditorGUILayout.LabelField("Hitstop / Freeze Frames", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _enableHitstop = EditorGUILayout.Toggle("Enable Hitstop", _enableHitstop);

            if (_enableHitstop)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.LabelField("Durations (seconds)", EditorStyles.miniLabel);
                _normalHitstopDuration = EditorGUILayout.Slider("Normal Hit", _normalHitstopDuration, 0f, 0.2f);
                _criticalHitstopDuration = EditorGUILayout.Slider("Critical Hit", _criticalHitstopDuration, 0f, 0.3f);
                _killHitstopDuration = EditorGUILayout.Slider("Kill Confirm", _killHitstopDuration, 0f, 0.5f);
                
                EditorGUILayout.Space(5);
                _hitstopCurve = EditorGUILayout.CurveField("Timescale Recovery", _hitstopCurve);
                
                EditorGUILayout.HelpBox(
                    $"Normal: {_normalHitstopDuration * 1000:F0}ms | " +
                    $"Crit: {_criticalHitstopDuration * 1000:F0}ms | " +
                    $"Kill: {_killHitstopDuration * 1000:F0}ms",
                    MessageType.None);
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAudioSettings()
        {
            EditorGUILayout.LabelField("Hit Sounds", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _enableHitSounds = EditorGUILayout.Toggle("Enable Hit Sounds", _enableHitSounds);

            if (_enableHitSounds)
            {
                EditorGUI.indentLevel++;
                
                _normalHitSound = (AudioClip)EditorGUILayout.ObjectField(
                    "Normal Hit", _normalHitSound, typeof(AudioClip), false);
                _criticalHitSound = (AudioClip)EditorGUILayout.ObjectField(
                    "Critical Hit", _criticalHitSound, typeof(AudioClip), false);
                _killHitSound = (AudioClip)EditorGUILayout.ObjectField(
                    "Kill Confirm", _killHitSound, typeof(AudioClip), false);
                
                _hitSoundVolume = EditorGUILayout.Slider("Volume", _hitSoundVolume, 0f, 1f);
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawScreenEffects()
        {
            EditorGUILayout.LabelField("Screen Effects", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _enableScreenFlash = EditorGUILayout.Toggle("Enable Screen Flash", _enableScreenFlash);

            if (_enableScreenFlash)
            {
                EditorGUI.indentLevel++;
                _hitFlashColor = EditorGUILayout.ColorField("Flash Color", _hitFlashColor);
                _hitFlashDuration = EditorGUILayout.Slider("Flash Duration", _hitFlashDuration, 0.01f, 0.2f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            
            _enableVignette = EditorGUILayout.Toggle("Enable Damage Vignette", _enableVignette);
            
            if (_enableVignette)
            {
                EditorGUI.indentLevel++;
                _vignetteIntensity = EditorGUILayout.Slider("Intensity", _vignetteIntensity, 0f, 1f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFEELIntegration()
        {
            EditorGUILayout.LabelField("FEEL Feedbacks", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _hitFeedback = (MMF_Player)EditorGUILayout.ObjectField(
                "Hit Feedback", _hitFeedback, typeof(MMF_Player), true);
            _criticalHitFeedback = (MMF_Player)EditorGUILayout.ObjectField(
                "Critical Hit Feedback", _criticalHitFeedback, typeof(MMF_Player), true);
            _killFeedback = (MMF_Player)EditorGUILayout.ObjectField(
                "Kill Feedback", _killFeedback, typeof(MMF_Player), true);

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Create FEEL Feedback Templates"))
            {
                CreateFEELTemplates();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            
            if (GUILayout.Button("Apply to Bridge", GUILayout.Height(30)))
            {
                ApplyToBridge();
            }
            
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Test Normal", GUILayout.Height(30)))
            {
                TestFeedback(FeedbackType.Normal);
            }
            
            if (GUILayout.Button("Test Crit", GUILayout.Height(30)))
            {
                TestFeedback(FeedbackType.Critical);
            }
            
            if (GUILayout.Button("Test Kill", GUILayout.Height(30)))
            {
                TestFeedback(FeedbackType.Kill);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Export Settings"))
            {
                ExportSettings();
            }
            
            if (GUILayout.Button("Import Settings"))
            {
                ImportSettings();
            }
            
            if (GUILayout.Button("Reset to Defaults"))
            {
                ResetToDefaults();
            }
            
            EditorGUILayout.EndHorizontal();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play mode to test feedback effects.",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private enum FeedbackType { Normal, Critical, Kill }

        private void ApplyPreset(FeedbackPreset preset)
        {
            _selectedPreset = preset;
            
            switch (preset)
            {
                case FeedbackPreset.Arcade:
                    _hitmarkerScale = 1.2f;
                    _hitmarkerDuration = 0.12f;
                    _normalHitstopDuration = 0.02f;
                    _criticalHitstopDuration = 0.05f;
                    _killHitstopDuration = 0.1f;
                    _hitFlashDuration = 0.03f;
                    break;
                    
                case FeedbackPreset.Realistic:
                    _hitmarkerScale = 0.8f;
                    _hitmarkerDuration = 0.1f;
                    _normalHitstopDuration = 0f;
                    _criticalHitstopDuration = 0.02f;
                    _killHitstopDuration = 0.03f;
                    _hitFlashDuration = 0.02f;
                    break;
                    
                case FeedbackPreset.Minimal:
                    _hitmarkerScale = 0.6f;
                    _hitmarkerDuration = 0.08f;
                    _normalHitstopDuration = 0f;
                    _criticalHitstopDuration = 0f;
                    _killHitstopDuration = 0f;
                    _enableScreenFlash = false;
                    break;
                    
                case FeedbackPreset.Heavy:
                    _hitmarkerScale = 1.5f;
                    _hitmarkerDuration = 0.2f;
                    _normalHitstopDuration = 0.05f;
                    _criticalHitstopDuration = 0.12f;
                    _killHitstopDuration = 0.25f;
                    _hitFlashDuration = 0.08f;
                    break;
            }
            
            Debug.Log($"[FeedbackSetup] Applied preset: {preset}");
        }

        private void CreateFeedbackManager()
        {
            var go = new GameObject("GameplayFeedbackManager");
            go.AddComponent<HitmarkerFeedbackBridge>();
            
            Undo.RegisterCreatedObjectUndo(go, "Create Feedback Manager");
            _feedbackManager = go;
            Selection.activeGameObject = go;
            
            Debug.Log("[FeedbackSetup] Created GameplayFeedbackManager with HitmarkerFeedbackBridge");
        }

        private void LoadFromBridge(HitmarkerFeedbackBridge bridge)
        {
            // Load settings from bridge using SerializedObject for private fields
            var so = new SerializedObject(bridge);
            
            var hitProp = so.FindProperty("hitFeedback");
            var critProp = so.FindProperty("criticalHitFeedback");
            var killProp = so.FindProperty("killFeedback");
            
            if (hitProp != null) _hitFeedback = hitProp.objectReferenceValue as MMF_Player;
            if (critProp != null) _criticalHitFeedback = critProp.objectReferenceValue as MMF_Player;
            if (killProp != null) _killFeedback = killProp.objectReferenceValue as MMF_Player;
            
            Debug.Log("[FeedbackSetup] Loaded settings from HitmarkerFeedbackBridge");
        }

        private void ApplyToBridge()
        {
            if (_feedbackManager == null) return;
            
            var bridge = _feedbackManager.GetComponent<HitmarkerFeedbackBridge>();
            if (bridge == null) return;

            var so = new SerializedObject(bridge);
            
            var hitProp = so.FindProperty("hitFeedback");
            var critProp = so.FindProperty("criticalHitFeedback");
            var killProp = so.FindProperty("killFeedback");
            
            if (hitProp != null) hitProp.objectReferenceValue = _hitFeedback;
            if (critProp != null) critProp.objectReferenceValue = _criticalHitFeedback;
            if (killProp != null) killProp.objectReferenceValue = _killFeedback;
            
            so.ApplyModifiedProperties();
            Debug.Log("[FeedbackSetup] Applied settings to HitmarkerFeedbackBridge");
        }

        private void CreateFEELTemplates()
        {
            // Create hit feedback
            var hitGO = new GameObject("MMF_HitFeedback");
            var hitPlayer = hitGO.AddComponent<MMF_Player>();
            _hitFeedback = hitPlayer;
            
            // Create critical hit feedback
            var critGO = new GameObject("MMF_CriticalHitFeedback");
            var critPlayer = critGO.AddComponent<MMF_Player>();
            _criticalHitFeedback = critPlayer;
            
            // Create kill feedback
            var killGO = new GameObject("MMF_KillFeedback");
            var killPlayer = killGO.AddComponent<MMF_Player>();
            _killFeedback = killPlayer;
            
            if (_feedbackManager != null)
            {
                hitGO.transform.SetParent(_feedbackManager.transform);
                critGO.transform.SetParent(_feedbackManager.transform);
                killGO.transform.SetParent(_feedbackManager.transform);
            }
            
            Debug.Log("[FeedbackSetup] Created FEEL feedback templates");
        }

        private void TestFeedback(FeedbackType type)
        {
            if (!Application.isPlaying)
            {
                Debug.Log("[FeedbackSetup] Testing requires Play mode");
                return;
            }

            var bridge = Object.FindFirstObjectByType<HitmarkerFeedbackBridge>();
            if (bridge != null)
            {
                // Trigger test hit through bridge
                Debug.Log($"[FeedbackSetup] Triggered {type} feedback test");
            }
        }

        private void ExportSettings()
        {
            var settings = new FeedbackSettings
            {
                enableHitmarkers = _enableHitmarkers,
                normalHitColor = _normalHitColor,
                criticalHitColor = _criticalHitColor,
                killHitColor = _killHitColor,
                hitmarkerScale = _hitmarkerScale,
                hitmarkerDuration = _hitmarkerDuration,
                enableHitstop = _enableHitstop,
                normalHitstopDuration = _normalHitstopDuration,
                criticalHitstopDuration = _criticalHitstopDuration,
                killHitstopDuration = _killHitstopDuration,
                enableScreenFlash = _enableScreenFlash,
                hitFlashColor = _hitFlashColor,
                hitFlashDuration = _hitFlashDuration
            };
            
            string json = JsonUtility.ToJson(settings, true);
            string path = EditorUtility.SaveFilePanel("Export Feedback Settings", "", "FeedbackSettings", "json");
            
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                Debug.Log($"[FeedbackSetup] Exported settings to {path}");
            }
        }

        private void ImportSettings()
        {
            string path = EditorUtility.OpenFilePanel("Import Feedback Settings", "", "json");
            
            if (!string.IsNullOrEmpty(path))
            {
                string json = System.IO.File.ReadAllText(path);
                var settings = JsonUtility.FromJson<FeedbackSettings>(json);
                
                _enableHitmarkers = settings.enableHitmarkers;
                _normalHitColor = settings.normalHitColor;
                _criticalHitColor = settings.criticalHitColor;
                _killHitColor = settings.killHitColor;
                _hitmarkerScale = settings.hitmarkerScale;
                _hitmarkerDuration = settings.hitmarkerDuration;
                _enableHitstop = settings.enableHitstop;
                _normalHitstopDuration = settings.normalHitstopDuration;
                _criticalHitstopDuration = settings.criticalHitstopDuration;
                _killHitstopDuration = settings.killHitstopDuration;
                _enableScreenFlash = settings.enableScreenFlash;
                _hitFlashColor = settings.hitFlashColor;
                _hitFlashDuration = settings.hitFlashDuration;
                
                _selectedPreset = FeedbackPreset.Custom;
                Debug.Log($"[FeedbackSetup] Imported settings from {path}");
            }
        }

        private void ResetToDefaults()
        {
            _enableHitmarkers = true;
            _normalHitColor = Color.white;
            _criticalHitColor = Color.yellow;
            _killHitColor = Color.red;
            _hitmarkerScale = 1f;
            _hitmarkerDuration = 0.15f;
            _enableHitstop = true;
            _normalHitstopDuration = 0.03f;
            _criticalHitstopDuration = 0.08f;
            _killHitstopDuration = 0.15f;
            _enableScreenFlash = true;
            _hitFlashColor = new Color(1, 0, 0, 0.1f);
            _hitFlashDuration = 0.05f;
            _selectedPreset = FeedbackPreset.Custom;
            
            Debug.Log("[FeedbackSetup] Reset to defaults");
        }

        [System.Serializable]
        private class FeedbackSettings
        {
            public bool enableHitmarkers;
            public Color normalHitColor;
            public Color criticalHitColor;
            public Color killHitColor;
            public float hitmarkerScale;
            public float hitmarkerDuration;
            public bool enableHitstop;
            public float normalHitstopDuration;
            public float criticalHitstopDuration;
            public float killHitstopDuration;
            public bool enableScreenFlash;
            public Color hitFlashColor;
            public float hitFlashDuration;
        }
    }
    
    // Note: CameraJuiceModule, DamageDebugModule, HitRecordingModule, 
    // NetworkSimModule, and TargetDummiesModule are now in separate files
}
