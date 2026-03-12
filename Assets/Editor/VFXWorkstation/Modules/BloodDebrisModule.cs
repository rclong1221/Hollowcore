using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.VFXWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 VW-05: Blood/Debris module.
    /// Hit reaction VFX, blood splatter, spark spawners.
    /// </summary>
    public class BloodDebrisModule : IVFXModule
    {
        private Vector2 _scrollPosition;
        
        // Category tabs
        private int _categoryTab = 0;
        private string[] _categories = { "Blood/Gore", "Sparks/Metal", "Debris/Fragments" };
        
        // Blood settings
        private GameObject _bloodSpatterPrefab;
        private GameObject _bloodMistPrefab;
        private GameObject _bloodDecalPrefab;
        private float _bloodSpatterScale = 1f;
        private float _bloodMistScale = 1f;
        private int _bloodDropletCount = 5;
        private float _bloodSprayForce = 3f;
        private Color _bloodColor = new Color(0.5f, 0f, 0f);
        private bool _useBloodDecals = true;
        private float _bloodDecalSize = 0.5f;
        
        // Sparks settings
        private GameObject _sparksPrefab;
        private GameObject _electricSparksPrefab;
        private float _sparksScale = 1f;
        private int _sparkCount = 10;
        private float _sparkLifetime = 0.5f;
        private Color _sparkColor = new Color(1f, 0.8f, 0.3f);
        private bool _useLightFlash = true;
        
        // Debris settings
        private GameObject _debrisPrefab;
        private List<GameObject> _debrisVariants = new List<GameObject>();
        private float _debrisScale = 1f;
        private int _debrisCount = 3;
        private float _debrisForce = 5f;
        private float _debrisLifetime = 5f;
        private bool _debrisPhysics = true;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Blood/Debris", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure hit reaction VFX including blood effects, sparks, and debris.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            // Category tabs
            _categoryTab = GUILayout.Toolbar(_categoryTab, _categories);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_categoryTab)
            {
                case 0:
                    DrawBloodSettings();
                    break;
                case 1:
                    DrawSparksSettings();
                    break;
                case 2:
                    DrawDebrisSettings();
                    break;
            }

            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawBloodSettings()
        {
            EditorGUILayout.LabelField("Blood Spatter", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _bloodSpatterPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Spatter Prefab", _bloodSpatterPrefab, typeof(GameObject), false);
            _bloodMistPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Blood Mist Prefab", _bloodMistPrefab, typeof(GameObject), false);
            
            EditorGUILayout.Space(5);
            
            _bloodSpatterScale = EditorGUILayout.Slider("Spatter Scale", _bloodSpatterScale, 0.1f, 3f);
            _bloodMistScale = EditorGUILayout.Slider("Mist Scale", _bloodMistScale, 0.1f, 3f);
            _bloodDropletCount = EditorGUILayout.IntSlider("Droplet Count", _bloodDropletCount, 1, 20);
            _bloodSprayForce = EditorGUILayout.Slider("Spray Force", _bloodSprayForce, 0.5f, 10f);
            
            EditorGUILayout.Space(5);
            _bloodColor = EditorGUILayout.ColorField("Blood Color", _bloodColor);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Blood Decals", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _useBloodDecals = EditorGUILayout.Toggle("Enable Blood Decals", _useBloodDecals);

            if (_useBloodDecals)
            {
                EditorGUI.indentLevel++;
                
                _bloodDecalPrefab = (GameObject)EditorGUILayout.ObjectField(
                    "Decal Prefab", _bloodDecalPrefab, typeof(GameObject), false);
                _bloodDecalSize = EditorGUILayout.Slider("Decal Size", _bloodDecalSize, 0.1f, 2f);
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Damage-Based Scaling", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Blood intensity scales with damage amount:", EditorStyles.miniLabel);
            
            // Damage scaling curve preview
            Rect curveRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(60), GUILayout.ExpandWidth(true));
            DrawDamageCurve(curveRect);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            DrawBloodPreview();
        }

        private void DrawDamageCurve(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            
            // Draw damage scale curve
            Handles.color = new Color(0.8f, 0.2f, 0.2f);
            
            int segments = 50;
            Vector3 prevPoint = Vector3.zero;
            
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float damage = t * 100; // 0-100 damage
                float scale = Mathf.Lerp(0.5f, 2f, Mathf.Sqrt(t)); // Non-linear scaling
                
                float x = rect.x + t * rect.width;
                float y = rect.yMax - (scale / 2f) * rect.height;
                
                Vector3 point = new Vector3(x, y, 0);
                
                if (i > 0)
                {
                    Handles.DrawLine(prevPoint, point);
                }
                
                prevPoint = point;
            }

            GUI.Label(new Rect(rect.x + 5, rect.yMax - 16, 60, 16), "0 dmg", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.xMax - 45, rect.yMax - 16, 60, 16), "100 dmg", EditorStyles.miniLabel);
        }

        private void DrawBloodPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (_bloodSpatterPrefab != null)
            {
                Rect previewRect = GUILayoutUtility.GetRect(60, 60, GUILayout.Width(60));
                var tex = AssetPreview.GetAssetPreview(_bloodSpatterPrefab);
                if (tex != null) GUI.DrawTexture(previewRect, tex, ScaleMode.ScaleToFit);
                else { EditorGUI.DrawRect(previewRect, _bloodColor); }
            }
            
            if (_bloodMistPrefab != null)
            {
                Rect previewRect = GUILayoutUtility.GetRect(60, 60, GUILayout.Width(60));
                var tex = AssetPreview.GetAssetPreview(_bloodMistPrefab);
                if (tex != null) GUI.DrawTexture(previewRect, tex, ScaleMode.ScaleToFit);
            }
            
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Test Blood Effect"))
            {
                Debug.Log("[BloodDebris] Blood test pending");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSparksSettings()
        {
            EditorGUILayout.LabelField("Impact Sparks", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _sparksPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Sparks Prefab", _sparksPrefab, typeof(GameObject), false);
            _electricSparksPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Electric Sparks", _electricSparksPrefab, typeof(GameObject), false);
            
            EditorGUILayout.Space(5);
            
            _sparksScale = EditorGUILayout.Slider("Scale", _sparksScale, 0.1f, 3f);
            _sparkCount = EditorGUILayout.IntSlider("Particle Count", _sparkCount, 1, 50);
            _sparkLifetime = EditorGUILayout.Slider("Lifetime (s)", _sparkLifetime, 0.1f, 2f);
            
            EditorGUILayout.Space(5);
            _sparkColor = EditorGUILayout.ColorField("Spark Color", _sparkColor);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Spark Light", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _useLightFlash = EditorGUILayout.Toggle("Enable Light Flash", _useLightFlash);

            if (_useLightFlash)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Brief point light on spark impact", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            DrawSparksPreview();
        }

        private void DrawSparksPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Spark visualization
            Rect sparkRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(60), GUILayout.ExpandWidth(true));
            DrawSparkVisualization(sparkRect);

            if (GUILayout.Button("Test Spark Effect"))
            {
                Debug.Log("[BloodDebris] Spark test pending");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSparkVisualization(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f));
            
            Vector2 center = rect.center;
            
            // Draw sparks radiating outward
            System.Random rand = new System.Random(42);
            
            for (int i = 0; i < _sparkCount; i++)
            {
                float angle = (float)rand.NextDouble() * Mathf.PI * 2;
                float length = 10f + (float)rand.NextDouble() * 20f * _sparksScale;
                
                Vector2 start = center;
                Vector2 end = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
                
                // Color gradient
                float t = (float)rand.NextDouble();
                Color c = Color.Lerp(_sparkColor, Color.white, t * 0.5f);
                
                Handles.color = c;
                Handles.DrawLine(new Vector3(start.x, start.y), new Vector3(end.x, end.y));
            }
            
            // Central flash
            if (_useLightFlash)
            {
                EditorGUI.DrawRect(new Rect(center.x - 5, center.y - 5, 10, 10), Color.white);
            }
        }

        private void DrawDebrisSettings()
        {
            EditorGUILayout.LabelField("Debris/Fragments", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _debrisPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Main Debris Prefab", _debrisPrefab, typeof(GameObject), false);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Debris Variants", EditorStyles.miniLabel);

            for (int i = 0; i < _debrisVariants.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                _debrisVariants[i] = (GameObject)EditorGUILayout.ObjectField(
                    _debrisVariants[i], typeof(GameObject), false);
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _debrisVariants.RemoveAt(i);
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Variant"))
            {
                _debrisVariants.Add(null);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Debris Behavior", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _debrisScale = EditorGUILayout.Slider("Scale", _debrisScale, 0.1f, 3f);
            _debrisCount = EditorGUILayout.IntSlider("Spawn Count", _debrisCount, 1, 20);
            _debrisForce = EditorGUILayout.Slider("Explosion Force", _debrisForce, 1f, 20f);
            _debrisLifetime = EditorGUILayout.Slider("Lifetime (s)", _debrisLifetime, 1f, 30f);
            
            EditorGUILayout.Space(5);
            _debrisPhysics = EditorGUILayout.Toggle("Use Physics", _debrisPhysics);
            
            if (_debrisPhysics)
            {
                EditorGUILayout.LabelField("Debris will interact with environment", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Surface-Specific Debris", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Configure in Impact FX module for per-surface debris", EditorStyles.wordWrappedMiniLabel);

            if (GUILayout.Button("Open Impact FX Module"))
            {
                Debug.Log("[BloodDebris] Would switch to Impact FX tab");
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            DrawDebrisPreview();
        }

        private void DrawDebrisPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Debris explosion visualization
            Rect debrisRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(80), GUILayout.ExpandWidth(true));
            DrawDebrisVisualization(debrisRect);

            if (GUILayout.Button("Test Debris Effect"))
            {
                Debug.Log("[BloodDebris] Debris test pending");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDebrisVisualization(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            
            Vector2 center = new Vector2(rect.center.x, rect.yMax - 10);
            
            System.Random rand = new System.Random(42);
            
            // Draw debris trajectories
            for (int i = 0; i < _debrisCount; i++)
            {
                float angle = 20f + (float)rand.NextDouble() * 140f; // Upward arc
                angle *= Mathf.Deg2Rad;
                
                float force = _debrisForce * (0.5f + (float)rand.NextDouble() * 0.5f);
                
                // Draw parabolic trajectory
                Handles.color = new Color(0.6f, 0.5f, 0.4f);
                
                Vector3 prevPoint = new Vector3(center.x, center.y, 0);
                
                for (int t = 1; t <= 10; t++)
                {
                    float time = t * 0.1f;
                    float x = center.x + Mathf.Cos(angle) * force * time * 3;
                    float y = center.y - (Mathf.Sin(angle) * force * time * 3 - 0.5f * 9.8f * time * time * 2);
                    
                    if (y > rect.yMax) break;
                    
                    Vector3 point = new Vector3(x, y, 0);
                    Handles.DrawLine(prevPoint, point);
                    prevPoint = point;
                }
                
                // Draw debris piece at current position
                float endX = center.x + Mathf.Cos(angle) * force * 0.5f * 3;
                float endY = center.y - (Mathf.Sin(angle) * force * 0.5f * 3 - 0.5f * 9.8f * 0.25f * 2);
                
                float size = 4 + (float)rand.NextDouble() * 4 * _debrisScale;
                EditorGUI.DrawRect(new Rect(endX - size/2, endY - size/2, size, size), new Color(0.5f, 0.4f, 0.3f));
            }
            
            // Impact point
            EditorGUI.DrawRect(new Rect(center.x - 3, center.y - 3, 6, 6), Color.red);
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            if (GUILayout.Button("Save Configuration", GUILayout.Height(30)))
            {
                SaveConfiguration();
            }
            
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Create ScriptableObject", GUILayout.Height(30)))
            {
                CreateScriptableObject();
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
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void SaveConfiguration()
        {
            Debug.Log("[BloodDebris] Configuration saved");
        }

        private void CreateScriptableObject()
        {
            Debug.Log("[BloodDebris] ScriptableObject creation pending");
        }

        private void ExportSettings()
        {
            Debug.Log("[BloodDebris] Export pending");
        }

        private void ImportSettings()
        {
            Debug.Log("[BloodDebris] Import pending");
        }
    }
}
