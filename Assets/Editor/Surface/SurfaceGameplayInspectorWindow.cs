using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Transforms;
using DIG.Surface;

/// <summary>
/// EPIC 16.10 Phase 9: Editor window for inspecting surface gameplay state.
/// Shows active surfaces, per-entity modifiers, config preview, and performance.
/// </summary>
public class SurfaceGameplayInspectorWindow : EditorWindow
{
    private Vector2 _scrollPosition;
    private bool _showEntities = true;
    private bool _showConfig = true;
    private bool _showPerformance;
    private double _nextRepaintTime;
    private const double RepaintInterval = 0.5; // 2 Hz — entity queries are expensive

    [MenuItem("DIG/Surface/Gameplay Inspector")]
    public static void ShowWindow()
    {
        GetWindow<SurfaceGameplayInspectorWindow>("Surface Gameplay");
    }

    private void OnInspectorUpdate()
    {
        if (Application.isPlaying && EditorApplication.timeSinceStartup >= _nextRepaintTime)
        {
            _nextRepaintTime = EditorApplication.timeSinceStartup + RepaintInterval;
            Repaint();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Surface Gameplay Inspector", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to inspect surface state.", MessageType.Info);
            return;
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawOverlayToggle();
        EditorGUILayout.Space(5);
        DrawConfigSection();
        EditorGUILayout.Space(5);
        DrawEntitySection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawOverlayToggle()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
#if UNITY_EDITOR
        bool overlay = SurfaceDebugOverlaySystem.ShowOverlay;
        bool newOverlay = EditorGUILayout.Toggle("Show Scene Overlay", overlay);
        if (newOverlay != overlay)
            SurfaceDebugOverlaySystem.ShowOverlay = newOverlay;
#endif
        EditorGUILayout.EndVertical();
    }

    private void DrawConfigSection()
    {
        _showConfig = EditorGUILayout.Foldout(_showConfig, "Surface Gameplay Config", true);
        if (!_showConfig) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        var world = GetActiveWorld();
        if (world == null || !world.IsCreated)
        {
            EditorGUILayout.LabelField("No active world");
            EditorGUILayout.EndVertical();
            return;
        }

        var em = world.EntityManager;
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<SurfaceGameplayConfigSingleton>());
        if (query.CalculateEntityCount() == 0)
        {
            EditorGUILayout.LabelField("SurfaceGameplayConfigSingleton not found");
            EditorGUILayout.EndVertical();
            return;
        }

        var singleton = query.GetSingleton<SurfaceGameplayConfigSingleton>();
        if (!singleton.Config.IsCreated)
        {
            EditorGUILayout.LabelField("BlobAsset not created");
            EditorGUILayout.EndVertical();
            return;
        }

        ref var blob = ref singleton.Config.Value;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Surface", EditorStyles.boldLabel, GUILayout.Width(100));
        EditorGUILayout.LabelField("Noise", EditorStyles.boldLabel, GUILayout.Width(50));
        EditorGUILayout.LabelField("Speed", EditorStyles.boldLabel, GUILayout.Width(50));
        EditorGUILayout.LabelField("Slip", EditorStyles.boldLabel, GUILayout.Width(50));
        EditorGUILayout.LabelField("FallDmg", EditorStyles.boldLabel, GUILayout.Width(55));
        EditorGUILayout.LabelField("DPS", EditorStyles.boldLabel, GUILayout.Width(40));
        EditorGUILayout.EndHorizontal();

        int count = blob.Modifiers.Length;
        for (int i = 0; i < count && i <= (int)SurfaceID.Energy_Shield; i++)
        {
            var mods = blob.Modifiers[i];
            var sid = (SurfaceID)i;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(sid.ToString(), GUILayout.Width(100));
            EditorGUILayout.LabelField($"{mods.NoiseMultiplier:F1}", GUILayout.Width(50));
            EditorGUILayout.LabelField($"{mods.SpeedMultiplier:F1}", GUILayout.Width(50));
            EditorGUILayout.LabelField($"{mods.SlipFactor:F2}", GUILayout.Width(50));
            EditorGUILayout.LabelField($"{mods.FallDamageMultiplier:F1}", GUILayout.Width(55));
            EditorGUILayout.LabelField($"{mods.DamagePerSecond:F0}", GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawEntitySection()
    {
        _showEntities = EditorGUILayout.Foldout(_showEntities, "Entities with GroundSurfaceState", true);
        if (!_showEntities) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        var world = GetActiveWorld();
        if (world == null || !world.IsCreated)
        {
            EditorGUILayout.LabelField("No active world");
            EditorGUILayout.EndVertical();
            return;
        }

        var em = world.EntityManager;
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<GroundSurfaceState>(),
            ComponentType.ReadOnly<LocalToWorld>());

        int entityCount = query.CalculateEntityCount();
        EditorGUILayout.LabelField($"Total: {entityCount} entities");

        if (entityCount == 0)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Entity", EditorStyles.boldLabel, GUILayout.Width(60));
        EditorGUILayout.LabelField("Surface", EditorStyles.boldLabel, GUILayout.Width(80));
        EditorGUILayout.LabelField("Grounded", EditorStyles.boldLabel, GUILayout.Width(65));
        EditorGUILayout.LabelField("Hardness", EditorStyles.boldLabel, GUILayout.Width(60));
        EditorGUILayout.LabelField("Flags", EditorStyles.boldLabel, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        int display = Mathf.Min(entityCount, 30);

        for (int i = 0; i < display; i++)
        {
            var entity = entities[i];
            var gs = em.GetComponentData<GroundSurfaceState>(entity);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"E{entity.Index}", GUILayout.Width(60));
            EditorGUILayout.LabelField(gs.SurfaceId.ToString(), GUILayout.Width(80));
            EditorGUILayout.LabelField(gs.IsGrounded ? "Yes" : "No", GUILayout.Width(65));
            EditorGUILayout.LabelField(gs.CachedHardness.ToString(), GUILayout.Width(60));
            EditorGUILayout.LabelField(gs.Flags.ToString(), GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }

        if (entityCount > display)
            EditorGUILayout.LabelField($"... and {entityCount - display} more");

        entities.Dispose();
        EditorGUILayout.EndVertical();
    }

    private static World GetActiveWorld()
    {
        foreach (var world in World.All)
        {
            if (world.IsCreated && world.Name.Contains("Client"))
                return world;
        }

        foreach (var world in World.All)
        {
            if (world.IsCreated && !world.Name.Contains("Editor"))
                return world;
        }

        return World.DefaultGameObjectInjectionWorld;
    }
}
