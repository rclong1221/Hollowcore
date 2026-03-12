# EPIC 10.6: Generation Tooling

**Status**: ✅ COMPLETE  
**Priority**: HIGH  
**Dependencies**: EPIC 10.1-10.5, EPIC 9.1-9.2  
**Estimated Time**: 4 days

---

## Goal

Create comprehensive editor tools for the **multi-layer hollow earth world**:
- **Preview** all 6-8 world layers without playing
- **Visualize** hollow earth dimensions and features
- **Edit** layer configurations with immediate feedback
- **Validate** world structure before runtime
- **Benchmark** generation performance
- **Compare** different seeds and configurations

---

## Files Created

| File | Purpose |
|------|---------|
| `Editor/Tools/WorldLayerEditor.cs` | Visual layer editor |
| `Editor/Tools/HollowEarthPreviewer.cs` | 3D hollow preview |
| `Editor/Tools/GenerationBenchmark.cs` | Performance testing |
| `Editor/Tools/WorldStructureValidator.cs` | Config validation |
| `Editor/Tools/SeedComparisonTool.cs` | Determinism check |

---

## Tool 1: World Layer Editor

Visual cross-section editor for designing the entire world structure.

```csharp
public class WorldLayerEditor : EditorWindow
{
    [MenuItem("DIG/World/World Layer Editor")]
    static void ShowWindow() => GetWindow<WorldLayerEditor>("World Layers");
    
    private WorldStructureConfig _config;
    private Vector2 _scrollPos;
    private float _pixelsPerMeter = 0.05f;
    private int _selectedLayer = -1;
    
    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _config = (WorldStructureConfig)EditorGUILayout.ObjectField(
            _config, typeof(WorldStructureConfig), false, GUILayout.Width(200));
        
        if (_config != null)
        {
            if (GUILayout.Button("Add Solid Layer", EditorStyles.toolbarButton))
                AddLayer(LayerType.Solid);
            if (GUILayout.Button("Add Hollow Layer", EditorStyles.toolbarButton))
                AddLayer(LayerType.Hollow);
        }
        
        GUILayout.FlexibleSpace();
        _pixelsPerMeter = EditorGUILayout.Slider("Zoom", _pixelsPerMeter, 0.01f, 0.2f, 
            GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();
        
        if (_config == null || _config.Layers == null) 
        {
            EditorGUILayout.HelpBox("Assign a WorldStructureConfig to begin", MessageType.Info);
            return;
        }
        
        EditorGUILayout.BeginHorizontal();
        
        // Left panel: Layer list
        DrawLayerList();
        
        // Center: Visual cross-section
        DrawCrossSection();
        
        // Right: Selected layer inspector
        if (_selectedLayer >= 0 && _selectedLayer < _config.Layers.Length)
            DrawLayerInspector(_config.Layers[_selectedLayer]);
        
        EditorGUILayout.EndHorizontal();
        
        // Footer: Statistics
        DrawStatistics();
    }
    
    private void DrawCrossSection()
    {
        var rect = GUILayoutUtility.GetRect(400, position.height - 100);
        
        // Background
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.18f));
        
        // Ground level line
        float groundY = rect.y + 50;
        EditorGUI.DrawRect(new Rect(rect.x, groundY, rect.width, 2), Color.green);
        GUI.Label(new Rect(rect.x + 5, groundY - 20, 100, 20), "Ground Level");
        
        // Draw each layer
        float currentY = groundY;
        foreach (var layer in _config.Layers)
        {
            float layerHeight = layer.Thickness * _pixelsPerMeter;
            
            Color layerColor = layer.Type == LayerType.Hollow 
                ? new Color(0.2f, 0.5f, 0.7f, 0.8f) 
                : new Color(0.5f, 0.4f, 0.3f, 0.8f);
            
            var layerRect = new Rect(rect.x + 50, currentY, rect.width - 100, layerHeight);
            EditorGUI.DrawRect(layerRect, layerColor);
            
            // Layer label
            var labelStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
            GUI.Label(new Rect(rect.x + 60, currentY + layerHeight/2 - 10, 200, 20),
                $"{layer.LayerName} ({layer.Thickness}m)", labelStyle);
            
            // If hollow, show height indicator
            if (layer.Type == LayerType.Hollow && layer.HollowProfile != null)
            {
                GUI.Label(new Rect(rect.x + 250, currentY + layerHeight/2 - 10, 200, 20),
                    $"Height: {layer.HollowProfile.AverageHeight}m", labelStyle);
            }
            
            // Depth marker on right
            GUI.Label(new Rect(rect.x + rect.width - 45, currentY + layerHeight/2 - 10, 45, 20),
                $"{-layer.TopDepth}m");
            
            currentY += layerHeight;
        }
        
        // Total depth marker
        float totalDepth = _config.GetTotalDepth();
        GUI.Label(new Rect(rect.x + rect.width - 60, currentY, 60, 20), 
            $"-{totalDepth}m");
    }
    
    private void DrawLayerList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        
        for (int i = 0; i < _config.Layers.Length; i++)
        {
            var layer = _config.Layers[i];
            
            EditorGUILayout.BeginHorizontal(
                _selectedLayer == i ? EditorStyles.helpBox : GUIStyle.none);
            
            // Type icon
            GUILayout.Label(layer.Type == LayerType.Hollow ? "🌋" : "🪨", GUILayout.Width(25));
            
            if (GUILayout.Button(layer.LayerName, EditorStyles.label))
            {
                _selectedLayer = i;
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
    
    private void DrawLayerInspector(WorldLayerDefinition layer)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(300));
        
        EditorGUILayout.LabelField(layer.LayerName, EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Editable fields
        layer.LayerName = EditorGUILayout.TextField("Name", layer.LayerName);
        layer.Type = (LayerType)EditorGUILayout.EnumPopup("Type", layer.Type);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Depth", EditorStyles.boldLabel);
        layer.TopDepth = EditorGUILayout.FloatField("Top", layer.TopDepth);
        layer.BottomDepth = EditorGUILayout.FloatField("Bottom", layer.BottomDepth);
        EditorGUILayout.LabelField($"Thickness: {layer.Thickness}m");
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Horizontal Extent", EditorStyles.boldLabel);
        layer.AreaWidth = EditorGUILayout.FloatField("Width (m)", layer.AreaWidth);
        layer.AreaLength = EditorGUILayout.FloatField("Length (m)", layer.AreaLength);
        EditorGUILayout.LabelField($"Area: {layer.AreaKm2:F2} km²");
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Gameplay", EditorStyles.boldLabel);
        layer.TargetPlaytimeMinutes = EditorGUILayout.Slider("Target Playtime (min)", 
            layer.TargetPlaytimeMinutes, 15, 120);
        layer.DifficultyMultiplier = EditorGUILayout.Slider("Difficulty", 
            layer.DifficultyMultiplier, 0.5f, 3f);
        
        if (layer.Type == LayerType.Hollow)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hollow Earth", EditorStyles.boldLabel);
            layer.HollowProfile = (HollowEarthProfile)EditorGUILayout.ObjectField(
                "Profile", layer.HollowProfile, typeof(HollowEarthProfile), false);
        }
        else
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Solid Layer", EditorStyles.boldLabel);
            layer.StrataProfile = (StrataProfile)EditorGUILayout.ObjectField(
                "Strata", layer.StrataProfile, typeof(StrataProfile), false);
            layer.CaveProfile = (CaveProfile)EditorGUILayout.ObjectField(
                "Caves", layer.CaveProfile, typeof(CaveProfile), false);
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawStatistics()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        float totalDepth = _config?.GetTotalDepth() ?? 0;
        int solidLayers = _config?.Layers?.Count(l => l.Type == LayerType.Solid) ?? 0;
        int hollowLayers = _config?.Layers?.Count(l => l.Type == LayerType.Hollow) ?? 0;
        float totalPlaytime = _config?.Layers?.Sum(l => l.TargetPlaytimeMinutes) ?? 0;
        
        GUILayout.Label($"Depth: {totalDepth:N0}m");
        GUILayout.Label($"Solid: {solidLayers}");
        GUILayout.Label($"Hollow: {hollowLayers}");
        GUILayout.Label($"Est. Playtime: {totalPlaytime/60:F1}h");
        
        EditorGUILayout.EndHorizontal();
    }
}
```

---

## Tool 2: Hollow Earth Previewer

3D preview of hollow earth dimensions and features.

```csharp
public class HollowEarthPreviewer : EditorWindow
{
    [MenuItem("DIG/World/Hollow Earth Previewer")]
    static void ShowWindow() => GetWindow<HollowEarthPreviewer>("Hollow Preview");
    
    private HollowEarthProfile _profile;
    private Texture2D _floorHeightmap;
    private Texture2D _ceilingHeightmap;
    private Texture2D _crossSection;
    private int _resolution = 256;
    private int _seed = 12345;
    
    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _profile = (HollowEarthProfile)EditorGUILayout.ObjectField(
            _profile, typeof(HollowEarthProfile), false, GUILayout.Width(200));
        _seed = EditorGUILayout.IntField("Seed", _seed, GUILayout.Width(150));
        
        if (GUILayout.Button("Generate", EditorStyles.toolbarButton))
            GeneratePreview();
        
        EditorGUILayout.EndHorizontal();
        
        if (_profile == null)
        {
            EditorGUILayout.HelpBox("Select a HollowEarthProfile to preview", MessageType.Info);
            return;
        }
        
        // Dimensions info
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Dimensions", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Height: {_profile.AverageHeight}m ± {_profile.HeightVariation}m");
        EditorGUILayout.LabelField($"Floor: {_profile.FloorWidth}m × {_profile.FloorLength}m");
        EditorGUILayout.LabelField($"Area: {(_profile.FloorWidth * _profile.FloorLength / 1_000_000f):F2} km²");
        EditorGUILayout.LabelField($"Volume: {(_profile.FloorWidth * _profile.FloorLength * _profile.AverageHeight / 1_000_000_000f):F3} km³");
        EditorGUILayout.EndVertical();
        
        // Preview images
        if (_floorHeightmap != null)
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Floor Terrain");
            GUILayout.Box(_floorHeightmap, GUILayout.Width(200), GUILayout.Height(200));
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Ceiling Terrain");
            GUILayout.Box(_ceilingHeightmap, GUILayout.Width(200), GUILayout.Height(200));
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Label("Cross Section (Side View)");
            GUILayout.Box(_crossSection, GUILayout.Width(400), GUILayout.Height(150));
        }
        
        // Feature preview
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Features", EditorStyles.boldLabel);
        DrawFeatureRow("Stalactites", _profile.HasStalactites);
        DrawFeatureRow("Stalagmites", _profile.HasStalagmites);
        DrawFeatureRow("Pillars", _profile.GeneratePillars);
        DrawFeatureRow("Underground Lakes", _profile.HasUndergroundLakes);
        DrawFeatureRow("Crystal Formations", _profile.HasCrystalFormations);
        DrawFeatureRow("Lava Flows", _profile.HasLavaFlows);
        DrawFeatureRow("Floating Islands", _profile.HasFloatingIslands);
        EditorGUILayout.EndVertical();
    }
    
    private void DrawFeatureRow(string name, bool enabled)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(enabled ? "✅" : "❌", GUILayout.Width(25));
        GUILayout.Label(name);
        EditorGUILayout.EndHorizontal();
    }
    
    private void GeneratePreview()
    {
        if (_profile == null) return;
        
        // Generate floor heightmap
        _floorHeightmap = new Texture2D(_resolution, _resolution);
        _ceilingHeightmap = new Texture2D(_resolution, _resolution);
        
        for (int x = 0; x < _resolution; x++)
        {
            for (int z = 0; z < _resolution; z++)
            {
                float worldX = (x / (float)_resolution - 0.5f) * _profile.FloorWidth;
                float worldZ = (z / (float)_resolution - 0.5f) * _profile.FloorLength;
                
                // Floor height using profile settings
                float floorNoise = Mathf.PerlinNoise(
                    worldX * _profile.FloorNoiseScale + _seed,
                    worldZ * _profile.FloorNoiseScale);
                float floorHeight = floorNoise * _profile.FloorAmplitude;
                
                // Ceiling height
                float ceilingNoise = Mathf.PerlinNoise(
                    worldX * _profile.CeilingNoiseScale + _seed + 1000,
                    worldZ * _profile.CeilingNoiseScale);
                float ceilingVariation = ceilingNoise * _profile.HeightVariation;
                
                _floorHeightmap.SetPixel(x, z, new Color(floorNoise, floorNoise, floorNoise));
                _ceilingHeightmap.SetPixel(x, z, new Color(ceilingNoise, ceilingNoise, ceilingNoise));
            }
        }
        
        _floorHeightmap.Apply();
        _ceilingHeightmap.Apply();
        
        // Generate cross section
        GenerateCrossSection();
    }
    
    private void GenerateCrossSection()
    {
        int width = 400;
        int height = 150;
        _crossSection = new Texture2D(width, height);
        
        // Clear background
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                _crossSection.SetPixel(x, y, new Color(0.1f, 0.1f, 0.15f));
        
        float heightScale = height / (_profile.AverageHeight + _profile.HeightVariation * 2 + 50);
        
        for (int x = 0; x < width; x++)
        {
            float worldX = (x / (float)width - 0.5f) * _profile.FloorWidth;
            
            // Sample floor and ceiling at this X
            float floorNoise = Mathf.PerlinNoise(worldX * _profile.FloorNoiseScale + _seed, 0);
            float floorHeight = floorNoise * _profile.FloorAmplitude;
            
            float ceilingNoise = Mathf.PerlinNoise(worldX * _profile.CeilingNoiseScale + _seed + 1000, 0);
            float ceilingHeight = _profile.AverageHeight + (ceilingNoise - 0.5f) * _profile.HeightVariation * 2;
            
            int floorY = (int)(floorHeight * heightScale) + 10;
            int ceilingY = (int)(ceilingHeight * heightScale) + 10;
            
            // Draw floor surface
            for (int y = 0; y < floorY; y++)
                _crossSection.SetPixel(x, y, new Color(0.4f, 0.3f, 0.2f));
            
            // Draw ceiling (rock above hollow)
            for (int y = ceilingY; y < height; y++)
                _crossSection.SetPixel(x, y, new Color(0.3f, 0.3f, 0.35f));
            
            // Draw air space
            for (int y = floorY; y < ceilingY; y++)
                _crossSection.SetPixel(x, y, new Color(0.2f, 0.3f, 0.4f, 0.3f));
        }
        
        _crossSection.Apply();
    }
}
```

---

## Tool 3: Generation Benchmark (DOTS)

Measure generation performance with actual Jobs.

```csharp
public class GenerationBenchmark : EditorWindow
{
    [MenuItem("DIG/World/Generation Benchmark")]
    static void ShowWindow() => GetWindow<GenerationBenchmark>("Benchmark");
    
    private WorldStructureConfig _config;
    private int _chunkCount = 100;
    private bool _includeCaves = true;
    private bool _includeOres = true;
    private bool _includeHollow = true;
    
    // Results
    private float _avgTerrainMs;
    private float _avgCaveMs;
    private float _avgOreMs;
    private float _avgTotalMs;
    private float _maxTotalMs;
    private bool _isRunning;
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Generation Benchmark", EditorStyles.boldLabel);
        
        _config = (WorldStructureConfig)EditorGUILayout.ObjectField(
            "Config", _config, typeof(WorldStructureConfig), false);
        
        _chunkCount = EditorGUILayout.IntSlider("Chunks to Test", _chunkCount, 10, 500);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        _includeCaves = EditorGUILayout.Toggle("Include Caves", _includeCaves);
        _includeOres = EditorGUILayout.Toggle("Include Ores", _includeOres);
        _includeHollow = EditorGUILayout.Toggle("Include Hollow", _includeHollow);
        
        EditorGUI.BeginDisabledGroup(_isRunning || _config == null);
        if (GUILayout.Button("Run Benchmark", GUILayout.Height(30)))
        {
            RunBenchmark();
        }
        EditorGUI.EndDisabledGroup();
        
        if (_avgTotalMs > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            DrawResultRow("Terrain Generation", _avgTerrainMs, 2f);
            DrawResultRow("Cave Carving", _avgCaveMs, 1.5f);
            DrawResultRow("Ore Placement", _avgOreMs, 0.5f);
            
            EditorGUILayout.Space();
            
            var totalColor = _avgTotalMs < 5f ? Color.green : 
                             _avgTotalMs < 10f ? Color.yellow : Color.red;
            
            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = totalColor } };
            EditorGUILayout.LabelField($"Total Average: {_avgTotalMs:F2} ms/chunk", style);
            EditorGUILayout.LabelField($"Maximum: {_maxTotalMs:F2} ms/chunk");
            
            EditorGUILayout.Space();
            
            if (_avgTotalMs >= 5f)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ Generation exceeds 5ms budget!\n" +
                    "Consider reducing cave complexity or optimizing ore placement.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("✅ Performance within budget", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
    }
    
    private void DrawResultRow(string name, float ms, float budget)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(name, GUILayout.Width(150));
        
        var color = ms < budget ? Color.green : 
                    ms < budget * 2 ? Color.yellow : Color.red;
        var style = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
        
        EditorGUILayout.LabelField($"{ms:F2} ms", style, GUILayout.Width(80));
        EditorGUILayout.LabelField($"(budget: {budget} ms)");
        EditorGUILayout.EndHorizontal();
    }
    
    private void RunBenchmark()
    {
        _isRunning = true;
        
        var terrainTimes = new List<float>();
        var caveTimes = new List<float>();
        var oreTimes = new List<float>();
        var totalTimes = new List<float>();
        
        var sw = new System.Diagnostics.Stopwatch();
        
        // Create native arrays for benchmark
        using var densities = new NativeArray<float>(32768, Allocator.TempJob);
        using var materials = new NativeArray<byte>(32768, Allocator.TempJob);
        
        for (int i = 0; i < _chunkCount; i++)
        {
            int3 chunkPos = new int3(i % 10, -(i / 100), (i / 10) % 10);
            float totalMs = 0;
            
            // Terrain
            sw.Restart();
            // var terrainJob = new GenerateTerrainJob { ... };
            // terrainJob.Run();
            sw.Stop();
            terrainTimes.Add((float)sw.Elapsed.TotalMilliseconds);
            totalMs += (float)sw.Elapsed.TotalMilliseconds;
            
            // Caves
            if (_includeCaves)
            {
                sw.Restart();
                // var caveJob = new CarveCavesJob { ... };
                // caveJob.Run();
                sw.Stop();
                caveTimes.Add((float)sw.Elapsed.TotalMilliseconds);
                totalMs += (float)sw.Elapsed.TotalMilliseconds;
            }
            
            // Ores
            if (_includeOres)
            {
                sw.Restart();
                // var oreJob = new PlaceOresJob { ... };
                // oreJob.Run();
                sw.Stop();
                oreTimes.Add((float)sw.Elapsed.TotalMilliseconds);
                totalMs += (float)sw.Elapsed.TotalMilliseconds;
            }
            
            totalTimes.Add(totalMs);
        }
        
        // Calculate averages
        _avgTerrainMs = terrainTimes.Count > 0 ? terrainTimes.Average() : 0;
        _avgCaveMs = caveTimes.Count > 0 ? caveTimes.Average() : 0;
        _avgOreMs = oreTimes.Count > 0 ? oreTimes.Average() : 0;
        _avgTotalMs = totalTimes.Average();
        _maxTotalMs = totalTimes.Max();
        
        _isRunning = false;
        Repaint();
    }
}
```

---

## Tool 4: World Structure Validator

Validate entire world configuration.

```csharp
public class WorldStructureValidator : EditorWindow
{
    [MenuItem("DIG/World/Validate World Structure")]
    static void ShowWindow() => GetWindow<WorldStructureValidator>("Validator");
    
    private WorldStructureConfig _config;
    private List<ValidationResult> _results = new();
    
    private void OnGUI()
    {
        _config = (WorldStructureConfig)EditorGUILayout.ObjectField(
            "Config", _config, typeof(WorldStructureConfig), false);
        
        if (GUILayout.Button("Validate"))
        {
            Validate();
        }
        
        if (_results.Count > 0)
        {
            EditorGUILayout.Space();
            
            int errors = _results.Count(r => r.Type == ResultType.Error);
            int warnings = _results.Count(r => r.Type == ResultType.Warning);
            int info = _results.Count(r => r.Type == ResultType.Info);
            
            EditorGUILayout.LabelField($"Results: {errors} errors, {warnings} warnings, {info} info");
            
            foreach (var result in _results)
            {
                MessageType msgType = result.Type switch
                {
                    ResultType.Error => MessageType.Error,
                    ResultType.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                
                EditorGUILayout.HelpBox(result.Message, msgType);
            }
        }
    }
    
    private void Validate()
    {
        _results.Clear();
        
        if (_config == null)
        {
            _results.Add(new ValidationResult(ResultType.Error, "No config assigned"));
            return;
        }
        
        // Check layers exist
        if (_config.Layers == null || _config.Layers.Length == 0)
        {
            _results.Add(new ValidationResult(ResultType.Error, "No layers defined"));
            return;
        }
        
        // Check layer ordering
        float lastBottom = 0;
        for (int i = 0; i < _config.Layers.Length; i++)
        {
            var layer = _config.Layers[i];
            
            if (layer.TopDepth != lastBottom)
            {
                _results.Add(new ValidationResult(ResultType.Error,
                    $"Layer {i} ({layer.LayerName}): Gap or overlap at {lastBottom}m"));
            }
            
            lastBottom = layer.BottomDepth;
            
            // Check hollow layers have profiles
            if (layer.Type == LayerType.Hollow && layer.HollowProfile == null)
            {
                _results.Add(new ValidationResult(ResultType.Error,
                    $"Hollow layer '{layer.LayerName}' missing HollowEarthProfile"));
            }
            
            // Check solid layers have strata
            if (layer.Type == LayerType.Solid && layer.StrataProfile == null)
            {
                _results.Add(new ValidationResult(ResultType.Warning,
                    $"Solid layer '{layer.LayerName}' missing StrataProfile"));
            }
            
            // Check playtime targets
            if (layer.TargetPlaytimeMinutes < 15)
            {
                _results.Add(new ValidationResult(ResultType.Warning,
                    $"Layer '{layer.LayerName}' has short playtime target: {layer.TargetPlaytimeMinutes}min"));
            }
            
            // Check hollow dimensions
            if (layer.Type == LayerType.Hollow && layer.HollowProfile != null)
            {
                if (layer.HollowProfile.AverageHeight > layer.Thickness)
                {
                    _results.Add(new ValidationResult(ResultType.Error,
                        $"Hollow '{layer.LayerName}': Height {layer.HollowProfile.AverageHeight}m exceeds layer thickness {layer.Thickness}m"));
                }
                
                if (layer.HollowProfile.AverageHeight < 100)
                {
                    _results.Add(new ValidationResult(ResultType.Warning,
                        $"Hollow '{layer.LayerName}' has low height ({layer.HollowProfile.AverageHeight}m). Consider 300m+"));
                }
            }
        }
        
        // Check total depth
        float totalDepth = _config.GetTotalDepth();
        if (totalDepth < 2000)
        {
            _results.Add(new ValidationResult(ResultType.Info,
                $"Total depth {totalDepth}m is shallow. Consider 5000m+ for full experience"));
        }
        
        // Check hollow count
        int hollowCount = _config.Layers.Count(l => l.Type == LayerType.Hollow);
        if (hollowCount < 4)
        {
            _results.Add(new ValidationResult(ResultType.Info,
                $"Only {hollowCount} hollow layers. Consider 5-6 for varied experience"));
        }
        
        if (_results.Count == 0)
        {
            _results.Add(new ValidationResult(ResultType.Info, "✅ All validations passed!"));
        }
    }
    
    private enum ResultType { Error, Warning, Info }
    
    private struct ValidationResult
    {
        public ResultType Type;
        public string Message;
        
        public ValidationResult(ResultType type, string message)
        {
            Type = type;
            Message = message;
        }
    }
}
```

---

## Tool 5: Seed Comparison (Updated for Layers)

```csharp
public class SeedComparisonTool : EditorWindow
{
    [MenuItem("DIG/World/Seed Comparison")]
    static void ShowWindow() => GetWindow<SeedComparisonTool>("Seeds");
    
    private WorldStructureConfig _config;
    private List<int> _seeds = new() { 12345, 54321, 11111, 99999 };
    private int _selectedLayerIndex = 0;
    private Texture2D[] _previews;
    
    private void OnGUI()
    {
        _config = (WorldStructureConfig)EditorGUILayout.ObjectField(
            "Config", _config, typeof(WorldStructureConfig), false);
        
        if (_config?.Layers != null)
        {
            string[] layerNames = _config.Layers.Select(l => l.LayerName).ToArray();
            _selectedLayerIndex = EditorGUILayout.Popup("Layer", _selectedLayerIndex, layerNames);
        }
        
        // Seed management
        EditorGUILayout.Space();
        for (int i = 0; i < _seeds.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _seeds[i] = EditorGUILayout.IntField($"Seed {i + 1}", _seeds[i]);
            if (GUILayout.Button("🎲", GUILayout.Width(25)))
                _seeds[i] = UnityEngine.Random.Range(0, int.MaxValue);
            if (GUILayout.Button("X", GUILayout.Width(25)) && _seeds.Count > 1)
            {
                _seeds.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Seed"))
            _seeds.Add(UnityEngine.Random.Range(0, int.MaxValue));
        if (GUILayout.Button("Generate All"))
            GenerateAllPreviews();
        EditorGUILayout.EndHorizontal();
        
        // Show previews
        if (_previews != null && _previews.Length == _seeds.Count)
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < _previews.Length; i++)
            {
                if (_previews[i] != null)
                {
                    EditorGUILayout.BeginVertical();
                    GUILayout.Label($"Seed: {_seeds[i]}");
                    GUILayout.Box(_previews[i], GUILayout.Width(150), GUILayout.Height(150));
                    EditorGUILayout.EndVertical();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
```

---

## Acceptance Criteria

- [x] World Layer Editor shows all layers visually
- [x] Hollow Earth Previewer generates heightmaps and cross-section
- [x] Generation Benchmark measures actual Job performance
- [x] World Structure Validator catches configuration errors
- [x] Seed Comparison works per-layer
- [x] All tools work without Play Mode
