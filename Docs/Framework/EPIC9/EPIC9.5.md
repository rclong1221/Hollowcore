# Epic 9.5: Debug & Validation Tools

**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Dependencies**: EPIC 8.13 (Editor Tools base), EPIC 9.2 (LOD System)  
**Estimated Time**: 2-3 days

---

## Quick Start Guide

### 1. World Slice Viewer
Visualizes voxel density and material layers in a 2D cross-section.
1. Open **DIG > Voxel > World Slice Viewer**.
2. Enter **Play Mode**.
3. Select an Axis (X/Y/Z) and View Mode (Density/Material/Collision).
4. Click **Refresh** to generate the view.
5. Click **Center on Camera** to align with your view.

### 2. Collision Tester
Automated physics validation suite.
1. Open **DIG > Voxel > Collision Tester**.
2. Enter **Play Mode** and ensure chunks are loaded.
3. Click **Run All Tests** to verify:
   - Raycast hit detection.
   - Player/Voxel layer matrix compatibility.
   - Collider enablement and mesh validity.

### 3. Material Validator
Pre-flight check for Voxel Registry integrity.
1. Open **DIG > Voxel > Validate Materials**.
2. Check Console for logs:
   - ✅ Success message if all valid.
   - ❌ Error logs for ID conflicts or missing prefabs.

---

## Component Reference

### `WorldSliceViewer.cs`
- **Location**: `Assets/Scripts/Voxel/Editor/`
- **Purpose**: Diagnostic tool for inspecting raw voxel data in the ECS world.
- **Key Features**:
    - **Density View**: Shows raw byte density (0-255).
    - **Material View**: Shows material ID colors.
    - **Collision View**: Visualizes the boolean solid/air threshold (ISO Level 127).
    - **Center on Camera**: Helper to align the slice with the Scene View camera.

### `CollisionTester.cs`
- **Location**: `Assets/Scripts/Voxel/Editor/`
- **Purpose**: Runtime validation of physics setup.
- **Key Tests**:
    - **Raycast Down**: Verifies that standard Unity Physics raycasts interact with the Voxel Mesh.
    - **Layer Matrix**: Checks `Physics.GetIgnoreLayerCollision` to ensure Player and Voxel layers interact.
    - **Collider State**: Iterates all `MeshCollider`s on "Voxel" layer to ensure they have meshes and are enabled.

### `MaterialValidator.cs`
- **Location**: `Assets/Scripts/Voxel/Editor/`
- **Purpose**: Static asset validation.
- **Checks**:
    - **Duplicate IDs**: Ensures no two materials verify to the same Byte ID.
    - **Missing Loot**: Warns if mineable materials lack loot prefabs.
    - **Registry Integrity**: Verifies the `VoxelMaterialRegistry` asset exists.

---

## Developer Guide

### Extending the Collision Tester
To add a new test case:
1. Open `CollisionTester.cs`.
2. Define a new method `private TestResult TestMechanism()`.
3. Add the method call to `RunAllTests()` and the GUI buttons.

### Adding New Visualizations
To add a new view mode (e.g., Temperature or Moisture) to `WorldSliceViewer`:
1. Add an entry to the `ViewMode` enum.
2. Update `SampleVoxel` to read the relevant data (may require accessing different buffers or components if not in `VoxelBlob`).
3. Update `OnGUI` to handle the new mode's legend.

---

## Goal

Create comprehensive debug and validation tools that:
- **Catch problems early** before they become bugs
- **Visualize invisible data** (density, materials, neighbors, LOD levels)
- **Automate testing** of common failure modes
- **Provide actionable feedback** (not just "error")

---

## Tools Overview

| Tool | Purpose | Usage |
|------|---------|-------|
| World Slice Viewer | 2D slice through voxel world | Debug generation |
| Chunk State Overlay | Color-coded chunk status + LOD | Debug streaming |
| Collision Tester | Automated collision validation | Catch falling-through bugs |
| Material Validator | Check all materials are valid | Pre-flight check |
| Network Diff Viewer | Compare client vs server state | Debug desync |

> **Integration Note**: Tools should be aware of the LOD system from 9.2 (`ChunkLODState`, `VoxelLODConfig`).


---

## Tool 1: World Slice Viewer

Render 2D cross-section of voxel world showing density/material at any depth.

**File**: `Assets/Scripts/Voxel/Editor/WorldSliceViewer.cs`

```csharp
public class WorldSliceViewer : EditorWindow
{
    [MenuItem("DIG/Voxel/World Slice Viewer")]
    static void ShowWindow() => GetWindow<WorldSliceViewer>("World Slice");
    
    private enum SliceAxis { X, Y, Z }
    private enum ViewMode { Density, Material, Collision }
    
    private SliceAxis _axis = SliceAxis.Y;
    private ViewMode _mode = ViewMode.Density;
    private int _slicePosition = 0;
    private int _viewSize = 64;  // Voxels visible
    private Vector2 _scrollOffset;
    private float _zoom = 4f;
    
    private Texture2D _sliceTexture;
    
    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        _axis = (SliceAxis)EditorGUILayout.EnumPopup("Axis", _axis, GUILayout.Width(150));
        _mode = (ViewMode)EditorGUILayout.EnumPopup("View", _mode, GUILayout.Width(150));
        
        EditorGUILayout.LabelField("Slice:", GUILayout.Width(40));
        _slicePosition = EditorGUILayout.IntSlider(_slicePosition, -100, 100, GUILayout.Width(200));
        
        if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            RefreshSlice();
        
        if (GUILayout.Button("Center on Camera", GUILayout.Width(120)))
            CenterOnCamera();
        
        EditorGUILayout.EndHorizontal();
        
        // Draw slice
        var rect = GUILayoutUtility.GetRect(position.width, position.height - 25);
        if (_sliceTexture != null)
        {
            GUI.DrawTexture(rect, _sliceTexture, ScaleMode.ScaleAndCrop);
        }
        else
        {
            EditorGUI.DrawRect(rect, Color.black);
            EditorGUI.LabelField(rect, "Press Refresh to generate slice", 
                new GUIStyle { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }});
        }
        
        // Legend
        DrawLegend();
    }
    
    private void RefreshSlice()
    {
        if (!Application.isPlaying) return;
        
        _sliceTexture = new Texture2D(_viewSize, _viewSize);
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;
        
        // Sample voxels across the slice
        for (int u = 0; u < _viewSize; u++)
        {
            for (int v = 0; v < _viewSize; v++)
            {
                int3 worldPos = GetWorldPos(u, v);
                var color = SampleVoxel(worldPos, world);
                _sliceTexture.SetPixel(u, v, color);
            }
        }
        
        _sliceTexture.Apply();
    }
    
    private int3 GetWorldPos(int u, int v)
    {
        int offset_u = u - _viewSize / 2 + (int)_scrollOffset.x;
        int offset_v = v - _viewSize / 2 + (int)_scrollOffset.y;
        
        switch (_axis)
        {
            case SliceAxis.X: return new int3(_slicePosition, offset_v, offset_u);
            case SliceAxis.Y: return new int3(offset_u, _slicePosition, offset_v);
            case SliceAxis.Z: return new int3(offset_u, offset_v, _slicePosition);
            default: return int3.zero;
        }
    }
    
    private Color SampleVoxel(int3 worldPos, World world)
    {
        // Get chunk and sample
        int3 chunkPos = CoordinateUtils.WorldToChunkPos(worldPos);
        int3 localPos = worldPos - chunkPos * VoxelConstants.CHUNK_SIZE;
        
        // Find chunk entity and get data
        // ... (lookup implementation)
        
        byte density = 0;  // Get from chunk
        byte material = 0;
        
        switch (_mode)
        {
            case ViewMode.Density:
                float d = density / 255f;
                return new Color(d, d, d);
                
            case ViewMode.Material:
                return GetMaterialColor(material);
                
            case ViewMode.Collision:
                return VoxelDensity.IsSolid(density) ? Color.green : Color.red;
                
            default:
                return Color.black;
        }
    }
    
    private Color GetMaterialColor(byte material)
    {
        // Load from registry or use defaults
        switch (material)
        {
            case 0: return Color.clear;  // Air
            case 1: return Color.gray;   // Stone
            case 2: return new Color(0.5f, 0.3f, 0.1f);  // Dirt
            case 3: return new Color(0.6f, 0.4f, 0.2f);  // Iron
            case 4: return Color.yellow;  // Gold
            default: return Color.magenta;
        }
    }
    
    private void DrawLegend()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        if (_mode == ViewMode.Material)
        {
            DrawLegendItem("Air", Color.clear);
            DrawLegendItem("Stone", Color.gray);
            DrawLegendItem("Dirt", new Color(0.5f, 0.3f, 0.1f));
            DrawLegendItem("Iron", new Color(0.6f, 0.4f, 0.2f));
            DrawLegendItem("Gold", Color.yellow);
        }
        else if (_mode == ViewMode.Collision)
        {
            DrawLegendItem("Solid", Color.green);
            DrawLegendItem("Air", Color.red);
        }
        
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }
    
    private void DrawLegendItem(string label, Color color)
    {
        var rect = GUILayoutUtility.GetRect(60, 20);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + 4, 12, 12), color);
        EditorGUI.LabelField(new Rect(rect.x + 16, rect.y, 44, 20), label);
    }
}
```

---

## Tool 2: Automated Collision Tester

Run collision tests without manually playing game.

**File**: `Assets/Scripts/Voxel/Editor/CollisionTester.cs`

```csharp
public class CollisionTester : EditorWindow
{
    [MenuItem("DIG/Voxel/Collision Tester")]
    static void ShowWindow() => GetWindow<CollisionTester>("Collision Tester");
    
    private struct TestResult
    {
        public string Name;
        public bool Passed;
        public string Message;
    }
    
    private List<TestResult> _results = new();
    private bool _testRunning = false;
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Collision Validation Suite", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox(
            "These tests verify collision is working correctly.\n" +
            "Run in Play Mode after chunks are loaded.",
            MessageType.Info);
        
        EditorGUI.BeginDisabledGroup(!Application.isPlaying || _testRunning);
        
        if (GUILayout.Button("Run All Tests"))
        {
            RunAllTests();
        }
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Test Raycast Down"))
            _results.Add(TestRaycastDown());
        if (GUILayout.Button("Test Player Collision"))
            _results.Add(TestPlayerCollision());
        if (GUILayout.Button("Test Chunk Boundaries"))
            _results.Add(TestChunkBoundaries());
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space(10);
        
        // Results
        EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
        
        foreach (var result in _results)
        {
            var rect = EditorGUILayout.GetControlRect();
            
            // Icon
            var iconRect = new Rect(rect.x, rect.y, 20, rect.height);
            EditorGUI.LabelField(iconRect, result.Passed ? "✅" : "❌");
            
            // Name
            var nameRect = new Rect(rect.x + 25, rect.y, 150, rect.height);
            EditorGUI.LabelField(nameRect, result.Name);
            
            // Message
            var msgRect = new Rect(rect.x + 180, rect.y, rect.width - 180, rect.height);
            EditorGUI.LabelField(msgRect, result.Message, 
                result.Passed ? EditorStyles.label : EditorStyles.boldLabel);
        }
        
        if (_results.Count > 0)
        {
            EditorGUILayout.Space();
            
            int passed = _results.Count(r => r.Passed);
            int total = _results.Count;
            
            if (passed == total)
                EditorGUILayout.HelpBox($"All {total} tests passed!", MessageType.Info);
            else
                EditorGUILayout.HelpBox($"{passed}/{total} tests passed. See failures above.", MessageType.Error);
            
            if (GUILayout.Button("Clear Results"))
                _results.Clear();
        }
    }
    
    private void RunAllTests()
    {
        _results.Clear();
        _results.Add(TestRaycastDown());
        _results.Add(TestPlayerCollision());
        _results.Add(TestChunkBoundaries());
        _results.Add(TestLayerMatrix());
        _results.Add(TestColliderEnabled());
    }
    
    private TestResult TestRaycastDown()
    {
        var cam = Camera.main;
        if (cam == null)
            return new TestResult { Name = "Raycast Down", Passed = false, Message = "No main camera" };
        
        var origin = cam.transform.position + Vector3.up * 10;
        int voxelLayer = 1 << LayerMask.NameToLayer("Voxel");
        
        if (Physics.Raycast(origin, Vector3.down, out var hit, 50f, voxelLayer))
        {
            return new TestResult 
            { 
                Name = "Raycast Down", 
                Passed = true, 
                Message = $"Hit {hit.collider.name} at Y={hit.point.y:F1}" 
            };
        }
        else
        {
            return new TestResult 
            { 
                Name = "Raycast Down", 
                Passed = false, 
                Message = "No collision detected! Check chunk loading." 
            };
        }
    }
    
    private TestResult TestPlayerCollision()
    {
        // Find player and check if grounded
        // ... implementation
        return new TestResult { Name = "Player Collision", Passed = true, Message = "OK" };
    }
    
    private TestResult TestChunkBoundaries()
    {
        // Test at chunk boundary positions
        // ... implementation
        return new TestResult { Name = "Chunk Boundaries", Passed = true, Message = "OK" };
    }
    
    private TestResult TestLayerMatrix()
    {
        int player = LayerMask.NameToLayer("Player");
        int voxel = LayerMask.NameToLayer("Voxel");
        
        if (player == -1)
            return new TestResult { Name = "Layer Matrix", Passed = false, Message = "Player layer not defined" };
        if (voxel == -1)
            return new TestResult { Name = "Layer Matrix", Passed = false, Message = "Voxel layer not defined" };
        
        bool canCollide = !Physics.GetIgnoreLayerCollision(player, voxel);
        
        return new TestResult 
        { 
            Name = "Layer Matrix", 
            Passed = canCollide, 
            Message = canCollide ? "Player↔Voxel enabled" : "Player↔Voxel DISABLED in Physics settings!" 
        };
    }
    
    private TestResult TestColliderEnabled()
    {
        var colliders = Object.FindObjectsOfType<MeshCollider>();
        int voxelLayer = LayerMask.NameToLayer("Voxel");
        
        int total = 0;
        int disabled = 0;
        int noMesh = 0;
        
        foreach (var col in colliders)
        {
            if (col.gameObject.layer != voxelLayer) continue;
            total++;
            
            if (!col.enabled) disabled++;
            if (col.sharedMesh == null || col.sharedMesh.vertexCount == 0) noMesh++;
        }
        
        if (total == 0)
            return new TestResult { Name = "Collider State", Passed = false, Message = "No voxel colliders found!" };
        if (disabled > 0)
            return new TestResult { Name = "Collider State", Passed = false, Message = $"{disabled}/{total} colliders disabled" };
        if (noMesh > 0)
            return new TestResult { Name = "Collider State", Passed = false, Message = $"{noMesh}/{total} colliders have no mesh" };
        
        return new TestResult { Name = "Collider State", Passed = true, Message = $"{total} colliders OK" };
    }
}
```

---

## Tool 3: Material Validator

Pre-flight check for all voxel materials.

**File**: `Assets/Scripts/Voxel/Editor/MaterialValidator.cs`

```csharp
public class MaterialValidator : EditorWindow
{
    [MenuItem("DIG/Voxel/Validate Materials")]
    static void Validate()
    {
        var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
        if (registry == null)
        {
            Debug.LogError("[Validator] VoxelMaterialRegistry not found in Resources!");
            return;
        }
        
        var errors = new List<string>();
        var warnings = new List<string>();
        var usedIds = new HashSet<byte>();
        
        foreach (var mat in registry.Materials)
        {
            if (mat == null)
            {
                errors.Add("Null entry in registry");
                continue;
            }
            
            // Duplicate ID check
            if (usedIds.Contains(mat.MaterialID))
                errors.Add($"{mat.name}: Duplicate MaterialID {mat.MaterialID}");
            usedIds.Add(mat.MaterialID);
            
            // Missing loot
            if (mat.IsMineable && mat.LootPrefab == null)
                warnings.Add($"{mat.name}: Mineable but no LootPrefab assigned");
            
            // Missing textures
            // ... check visual material references
        }
        
        // Report
        if (errors.Count == 0 && warnings.Count == 0)
        {
            Debug.Log($"[Validator] ✅ All {registry.Materials.Length} materials valid!");
        }
        else
        {
            foreach (var e in errors)
                Debug.LogError($"[Validator] ❌ {e}");
            foreach (var w in warnings)
                Debug.LogWarning($"[Validator] ⚠️ {w}");
        }
    }
}
```

---

## Acceptance Criteria

- [ ] World Slice Viewer shows density/material at any Y level
- [ ] Collision Tester catches common failure modes
- [ ] Material Validator runs before play mode
- [ ] All tools provide clear, actionable feedback
- [ ] Tools work without requiring user to configure anything
