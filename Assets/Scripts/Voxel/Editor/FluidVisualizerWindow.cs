using UnityEngine;
using UnityEditor;
using DIG.Voxel.Fluids;
using System.Linq;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Editor window for visualizing and testing fluid configurations.
    /// Shows fluid properties, damage preview, and layer fluid settings.
    /// </summary>
    public class FluidVisualizerWindow : EditorWindow
    {
        private FluidDefinition[] _fluidDefinitions;
        private Vector2 _scrollPos;
        private int _selectedFluidIndex = -1;
        private float _testSubmersionDepth = 1f;
        private float _testDuration = 5f;
        
        private GUIStyle _headerStyle;
        private GUIStyle _dangerStyle;
        private bool _stylesInitialized;
        
        [MenuItem("DIG/World/Fluid Visualizer")]
        public static void ShowWindow()
        {
            var window = GetWindow<FluidVisualizerWindow>("Fluid Visualizer");
            window.minSize = new Vector2(600, 400);
            window.RefreshFluidList();
        }
        
        private void InitStyles()
        {
            if (_stylesInitialized) return;
            
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };
            
            _dangerStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(1f, 0.3f, 0.3f) }
            };
            
            _stylesInitialized = true;
        }
        
        private void RefreshFluidList()
        {
            var guids = AssetDatabase.FindAssets("t:FluidDefinition");
            _fluidDefinitions = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<FluidDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(f => f != null)
                .OrderBy(f => f.FluidID)
                .ToArray();
        }
        
        private void OnGUI()
        {
            InitStyles();
            
            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                RefreshFluidList();
            }
            if (GUILayout.Button("Create Fluids", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                FluidQuickSetup.CreateFluidDefinitions();
                RefreshFluidList();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            if (_fluidDefinitions == null || _fluidDefinitions.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No Fluid Definitions found.\n\n" +
                    "Click 'Create Fluids' above or go to:\n" +
                    "DIG → Quick Setup → Generation → Create Fluid Definitions",
                    MessageType.Info);
                return;
            }
            
            EditorGUILayout.BeginHorizontal();
            
            // Left: Fluid list
            DrawFluidList();
            
            // Right: Fluid inspector
            DrawFluidInspector();
            
            EditorGUILayout.EndHorizontal();
            
            // Bottom: Damage calculator
            DrawDamageCalculator();
        }
        
        private void DrawFluidList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("Fluids", _headerStyle);
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(250));
            
            for (int i = 0; i < _fluidDefinitions.Length; i++)
            {
                var fluid = _fluidDefinitions[i];
                bool isSelected = _selectedFluidIndex == i;
                
                EditorGUILayout.BeginHorizontal(isSelected ? EditorStyles.helpBox : GUIStyle.none);
                
                // Color indicator
                var colorRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
                EditorGUI.DrawRect(colorRect, fluid.FluidColor);
                
                // Name button
                if (GUILayout.Button($"[{fluid.FluidID}] {fluid.FluidName}", EditorStyles.label))
                {
                    _selectedFluidIndex = i;
                }
                
                // Hazard indicator
                if (fluid.DamagePerSecond > 0)
                {
                    GUILayout.Label("⚠", GUILayout.Width(20));
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        
        private void DrawFluidInspector()
        {
            EditorGUILayout.BeginVertical();
            
            if (_selectedFluidIndex < 0 || _selectedFluidIndex >= _fluidDefinitions.Length)
            {
                EditorGUILayout.HelpBox("Select a fluid to inspect", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }
            
            var fluid = _fluidDefinitions[_selectedFluidIndex];
            
            // Header with color
            EditorGUILayout.BeginHorizontal();
            var headerRect = GUILayoutUtility.GetRect(30, 30, GUILayout.Width(30));
            EditorGUI.DrawRect(headerRect, fluid.FluidColor);
            EditorGUILayout.LabelField(fluid.FluidName, _headerStyle);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Properties
            EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Type: {fluid.Type}");
            EditorGUILayout.LabelField($"ID: {fluid.FluidID}");
            EditorGUILayout.LabelField($"Viscosity: {fluid.Viscosity:F2}");
            EditorGUILayout.LabelField($"Density: {fluid.Density:F2}");
            EditorGUILayout.LabelField($"Spread Rate: {fluid.SpreadRate}");
            
            EditorGUILayout.Space(5);
            
            // Hazards
            EditorGUILayout.LabelField("Hazards", EditorStyles.boldLabel);
            if (fluid.DamagePerSecond > 0)
            {
                EditorGUILayout.LabelField($"Damage Type: {fluid.DamageType}", _dangerStyle);
                EditorGUILayout.LabelField($"DPS: {fluid.DamagePerSecond}", _dangerStyle);
                EditorGUILayout.LabelField($"Start Depth: {fluid.DamageStartDepth}m");
            }
            else
            {
                EditorGUILayout.LabelField("No direct damage", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.Space(5);
            
            // Special properties
            EditorGUILayout.LabelField("Special", EditorStyles.boldLabel);
            if (fluid.IsPressurized)
                EditorGUILayout.LabelField($"⚡ Pressurized (Level {fluid.PressureLevel})");
            if (fluid.IsFlammable)
                EditorGUILayout.LabelField("🔥 Flammable");
            if (fluid.IsToxic)
                EditorGUILayout.LabelField("☠ Toxic");
            if (fluid.CoolsToSolid)
                EditorGUILayout.LabelField($"❄ Cools to Material #{fluid.CooledMaterialID}");
            if (fluid.IsEmissive)
                EditorGUILayout.LabelField("💡 Emissive");
            
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Select in Inspector"))
            {
                Selection.activeObject = fluid;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawDamageCalculator()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Damage Calculator", _headerStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Submersion:", GUILayout.Width(80));
            _testSubmersionDepth = EditorGUILayout.Slider(_testSubmersionDepth, 0f, 10f);
            EditorGUILayout.LabelField("meters", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Duration:", GUILayout.Width(80));
            _testDuration = EditorGUILayout.Slider(_testDuration, 0f, 30f);
            EditorGUILayout.LabelField("seconds", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            // Show damage for each fluid
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            foreach (var fluid in _fluidDefinitions)
            {
                if (fluid.DamagePerSecond <= 0) continue;
                
                float damage = CalculateDamage(fluid, _testSubmersionDepth, _testDuration);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(100));
                EditorGUILayout.LabelField(fluid.FluidName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{damage:F0} HP", _dangerStyle);
                EditorGUILayout.LabelField($"({_testDuration}s)", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private float CalculateDamage(FluidDefinition fluid, float depth, float duration)
        {
            if (depth < fluid.DamageStartDepth)
                return 0f;
            
            // Water drowning scales with depth
            if (fluid.Type == FluidType.Water)
            {
                float depthFactor = Mathf.Clamp01((depth - fluid.DamageStartDepth) / 2f);
                return fluid.DamagePerSecond * depthFactor * duration;
            }
            
            // Other fluids deal full damage
            return fluid.DamagePerSecond * duration;
        }
    }
}
