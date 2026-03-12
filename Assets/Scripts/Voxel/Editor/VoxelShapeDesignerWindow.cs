using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using DIG.Voxel;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// EPIC 15.10: Editor window for designing and previewing voxel destruction shapes.
    /// </summary>
    public class VoxelShapeDesignerWindow : EditorWindow
    {
        // Shape settings
        private VoxelDamageShapeType _shapeType = VoxelDamageShapeType.Sphere;
        private float _param1 = 3f;  // Radius, angle, etc.
        private float _param2 = 2f;  // Height, length, etc.
        private float _param3 = 0.5f; // Tip radius, etc.
        private VoxelDamageFalloff _falloff = VoxelDamageFalloff.Linear;
        private float _edgeMult = 0.5f;
        private float _damage = 100f;
        private VoxelDamageType _damageType = VoxelDamageType.Mining;
        
        // Preview
        private Vector3 _previewPosition = Vector3.zero;
        private Quaternion _previewRotation = Quaternion.identity;
        private bool _showPreview = true;
        
        // Tool bit settings
        private ToolBitType _selectedBitType = ToolBitType.StandardBit;
        
        // UI state
        private Vector2 _scrollPos;
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "Shape", "Tool Bit", "Presets" };
        
        [MenuItem("DIG/Voxel/Shape Designer")]
        public static void ShowWindow()
        {
            var window = GetWindow<VoxelShapeDesignerWindow>("Shape Designer");
            window.minSize = new Vector2(350, 500);
        }
        
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }
        
        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Voxel Shape Designer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Design destruction shapes visually. Use Scene View to preview.", MessageType.Info);
            
            // Tabs
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            EditorGUILayout.Space(10);
            
            switch (_selectedTab)
            {
                case 0: DrawShapeTab(); break;
                case 1: DrawToolBitTab(); break;
                case 2: DrawPresetsTab(); break;
            }
            
            EditorGUILayout.Space(10);
            
            // Preview toggle
            _showPreview = EditorGUILayout.Toggle("Show Preview in Scene", _showPreview);
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawShapeTab()
        {
            EditorGUILayout.LabelField("Shape Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _shapeType = (VoxelDamageShapeType)EditorGUILayout.EnumPopup("Shape Type", _shapeType);
            
            // Shape-specific parameters
            switch (_shapeType)
            {
                case VoxelDamageShapeType.Point:
                    EditorGUILayout.LabelField("Point: Single voxel damage");
                    break;
                    
                case VoxelDamageShapeType.Sphere:
                    _param1 = EditorGUILayout.Slider("Radius", _param1, 0.5f, 20f);
                    break;
                    
                case VoxelDamageShapeType.Cylinder:
                    _param1 = EditorGUILayout.Slider("Radius", _param1, 0.5f, 10f);
                    _param2 = EditorGUILayout.Slider("Height", _param2, 0.5f, 20f);
                    break;
                    
                case VoxelDamageShapeType.Cone:
                    _param1 = EditorGUILayout.Slider("Angle (degrees)", _param1, 5f, 90f);
                    _param2 = EditorGUILayout.Slider("Length", _param2, 1f, 20f);
                    _param3 = EditorGUILayout.Slider("Tip Radius", _param3, 0f, 3f);
                    break;
                    
                case VoxelDamageShapeType.Capsule:
                    _param1 = EditorGUILayout.Slider("Radius", _param1, 0.5f, 5f);
                    _param2 = EditorGUILayout.Slider("Length", _param2, 1f, 30f);
                    break;
                    
                case VoxelDamageShapeType.Box:
                    EditorGUILayout.LabelField("Half Extents:");
                    _param1 = EditorGUILayout.Slider("X", _param1, 0.5f, 10f);
                    _param2 = EditorGUILayout.Slider("Y", _param2, 0.5f, 10f);
                    _param3 = EditorGUILayout.Slider("Z", _param3, 0.5f, 10f);
                    break;
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("Damage Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _damage = EditorGUILayout.Slider("Damage", _damage, 1f, 500f);
            _damageType = (VoxelDamageType)EditorGUILayout.EnumPopup("Damage Type", _damageType);
            _falloff = (VoxelDamageFalloff)EditorGUILayout.EnumPopup("Falloff", _falloff);
            _edgeMult = EditorGUILayout.Slider("Edge Multiplier", _edgeMult, 0f, 1f);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("Preview Position", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _previewPosition = EditorGUILayout.Vector3Field("Position", _previewPosition);
            
            Vector3 eulerAngles = _previewRotation.eulerAngles;
            eulerAngles = EditorGUILayout.Vector3Field("Rotation", eulerAngles);
            _previewRotation = Quaternion.Euler(eulerAngles);
            
            if (GUILayout.Button("Move to Scene Camera"))
            {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    _previewPosition = sceneView.camera.transform.position + sceneView.camera.transform.forward * 5f;
                    _previewRotation = sceneView.camera.transform.rotation;
                }
            }
            
            EditorGUILayout.EndVertical();
            
            // Code generation
            EditorGUILayout.Space(10);
            if (GUILayout.Button("Copy Code to Clipboard"))
            {
                CopyCodeToClipboard();
            }
        }
        
        private void DrawToolBitTab()
        {
            EditorGUILayout.LabelField("Tool Bit Selection", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _selectedBitType = (ToolBitType)EditorGUILayout.EnumPopup("Bit Type", _selectedBitType);
            
            var bit = ToolBit.GetPreset(_selectedBitType);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Bit Properties:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  Damage Multiplier: {bit.DamageMultiplier:F2}x");
            EditorGUILayout.LabelField($"  Shape Type: {bit.ShapeType}");
            EditorGUILayout.LabelField($"  Durability: {bit.MaxDurability:F0}");
            EditorGUILayout.LabelField($"  Resistance Bonus: {bit.MaterialResistanceBonus:+0%;-0%;0%}");
            
            EditorGUILayout.EndVertical();
            
            if (GUILayout.Button("Apply Bit Shape to Preview"))
            {
                _shapeType = bit.ShapeType;
                _param1 = bit.ShapeParam1;
                _param2 = bit.ShapeParam2;
                _param3 = bit.ShapeParam3;
                _damageType = bit.DamageType;
                SceneView.RepaintAll();
            }
        }
        
        private void DrawPresetsTab()
        {
            EditorGUILayout.LabelField("Common Presets", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Explosions:");
            
            if (GUILayout.Button("Small Explosion (r=3)"))
            {
                _shapeType = VoxelDamageShapeType.Sphere;
                _param1 = 3f;
                _damage = 100f;
                _falloff = VoxelDamageFalloff.Quadratic;
                _damageType = VoxelDamageType.Explosive;
            }
            
            if (GUILayout.Button("Large Explosion (r=8)"))
            {
                _shapeType = VoxelDamageShapeType.Sphere;
                _param1 = 8f;
                _damage = 200f;
                _falloff = VoxelDamageFalloff.Quadratic;
                _damageType = VoxelDamageType.Explosive;
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Drills:");
            
            if (GUILayout.Button("Drill Bit (cylinder)"))
            {
                _shapeType = VoxelDamageShapeType.Cylinder;
                _param1 = 1f;
                _param2 = 2f;
                _damage = 50f;
                _falloff = VoxelDamageFalloff.None;
                _damageType = VoxelDamageType.Mining;
            }
            
            if (GUILayout.Button("Tunnel Bore (large cylinder)"))
            {
                _shapeType = VoxelDamageShapeType.Cylinder;
                _param1 = 3f;
                _param2 = 6f;
                _damage = 100f;
                _falloff = VoxelDamageFalloff.None;
                _damageType = VoxelDamageType.Mining;
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Shaped Charges:");
            
            if (GUILayout.Button("Cutting Cone (30°)"))
            {
                _shapeType = VoxelDamageShapeType.Cone;
                _param1 = 30f;
                _param2 = 6f;
                _param3 = 0.3f;
                _damage = 150f;
                _falloff = VoxelDamageFalloff.Linear;
                _damageType = VoxelDamageType.Explosive;
            }
            
            if (GUILayout.Button("Laser Beam (capsule)"))
            {
                _shapeType = VoxelDamageShapeType.Capsule;
                _param1 = 0.3f;
                _param2 = 15f;
                _damage = 200f;
                _falloff = VoxelDamageFalloff.None;
                _damageType = VoxelDamageType.Laser;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void CopyCodeToClipboard()
        {
            string code = _shapeType switch
            {
                VoxelDamageShapeType.Point =>
                    $"VoxelDamageRequest.CreatePoint(sourcePos, source, targetPos, {_damage}f, VoxelDamageType.{_damageType})",
                    
                VoxelDamageShapeType.Sphere =>
                    $"VoxelDamageRequest.CreateSphere(sourcePos, source, targetPos, {_param1}f, {_damage}f, VoxelDamageFalloff.{_falloff}, {_edgeMult}f, VoxelDamageType.{_damageType})",
                    
                VoxelDamageShapeType.Cylinder =>
                    $"VoxelDamageRequest.CreateCylinder(sourcePos, source, targetPos, rotation, {_param1}f, {_param2}f, {_damage}f, VoxelDamageFalloff.{_falloff}, {_edgeMult}f, VoxelDamageType.{_damageType})",
                    
                VoxelDamageShapeType.Cone =>
                    $"VoxelDamageRequest.CreateCone(sourcePos, source, targetPos, rotation, {_param1}f, {_param2}f, {_param3}f, {_damage}f, VoxelDamageFalloff.{_falloff}, {_edgeMult}f, VoxelDamageType.{_damageType})",
                    
                VoxelDamageShapeType.Capsule =>
                    $"VoxelDamageRequest.CreateCapsule(sourcePos, source, targetPos, rotation, {_param1}f, {_param2}f, {_damage}f, VoxelDamageFalloff.{_falloff}, {_edgeMult}f, VoxelDamageType.{_damageType})",
                    
                VoxelDamageShapeType.Box =>
                    $"VoxelDamageRequest.CreateBox(sourcePos, source, targetPos, rotation, new float3({_param1}f, {_param2}f, {_param3}f), {_damage}f, VoxelDamageFalloff.{_falloff}, {_edgeMult}f, VoxelDamageType.{_damageType})",
                    
                _ => "// Invalid shape type"
            };
            
            GUIUtility.systemCopyBuffer = code;
            UnityEngine.Debug.Log("[ShapeDesigner] Code copied to clipboard!");
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_showPreview) return;
            
            VoxelShapeGizmos.DrawShape(
                _shapeType,
                _previewPosition,
                _previewRotation,
                _param1,
                _param2,
                _param3
            );
            
            // Draw label
            Handles.Label(_previewPosition + Vector3.up * (_param1 + 1f),
                $"{_shapeType}\nDamage: {_damage:F0}\nFalloff: {_falloff}",
                EditorStyles.whiteBoldLabel);
        }
    }
}
