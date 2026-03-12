#if UNITY_EDITOR
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEditor;

namespace DIG.Interaction.Editor
{
    /// <summary>
    /// EPIC 16.1 Phase 8: Live debugger for the interaction system.
    /// Shows all interactable entities, active interactions, proximity zones,
    /// and cooperative states in real-time during play mode.
    /// Menu: Window > DIG > Interaction Debugger.
    /// </summary>
    public class InteractionDebuggerWindow : EditorWindow
    {
        private enum DebugTab { Interactables, ActiveInteractions, Zones }

        private DebugTab _tab = DebugTab.Interactables;
        private Vector2 _scrollPos;
        private double _lastRefreshTime;
        private const double RefreshInterval = 0.1; // 10fps refresh

        private static readonly string[] TabNames = { "Interactables", "Active Interactions", "Zones & Coop" };

        [MenuItem("Window/DIG/Interaction Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<InteractionDebuggerWindow>("Interaction Debugger");
            window.minSize = new Vector2(500, 400);
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!Application.isPlaying) return;

            if (EditorApplication.timeSinceStartup - _lastRefreshTime > RefreshInterval)
            {
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnGUI()
        {
            // Tab toolbar
            _tab = (DebugTab)GUILayout.Toolbar((int)_tab, TabNames, EditorStyles.toolbarButton);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live ECS data.", MessageType.Info);
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_tab)
            {
                case DebugTab.Interactables:
                    DrawInteractables(world.EntityManager);
                    break;
                case DebugTab.ActiveInteractions:
                    DrawActiveInteractions(world.EntityManager);
                    break;
                case DebugTab.Zones:
                    DrawZonesAndCoop(world.EntityManager);
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────
        //  Tab 1: All Interactables
        // ─────────────────────────────────────────────────────

        private void DrawInteractables(EntityManager em)
        {
            EditorGUILayout.LabelField("All Interactable Entities", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var query = em.CreateEntityQuery(ComponentType.ReadOnly<Interactable>());
            var entities = query.ToEntityArray(Allocator.Temp);

            if (entities.Length == 0)
            {
                EditorGUILayout.LabelField("  No interactables found.", EditorStyles.miniLabel);
                entities.Dispose();
                return;
            }

            EditorGUILayout.LabelField($"  Count: {entities.Length}", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.Exists(entity)) continue;

                var interactable = em.GetComponentData<Interactable>(entity);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                string canInteract = interactable.CanInteract ? "Active" : "Disabled";
                EditorGUILayout.LabelField(
                    $"Entity {entity.Index}:{entity.Version}",
                    EditorStyles.boldLabel, GUILayout.Width(120));
                EditorGUILayout.LabelField(
                    $"Type: {interactable.Type}  |  {canInteract}  |  Radius: {interactable.InteractionRadius:F1}  |  Priority: {interactable.Priority}");

                EditorGUILayout.EndHorizontal();

                // Show addon components
                DrawAddonBadges(em, entity);

                EditorGUILayout.EndVertical();
            }

            entities.Dispose();
        }

        private void DrawAddonBadges(EntityManager em, Entity entity)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);

            if (em.HasComponent<InteractionSession>(entity))
                DrawBadge("Station", new Color(0.3f, 0.7f, 0.3f));
            if (em.HasComponent<MountPoint>(entity))
                DrawBadge("Mount", new Color(0.3f, 0.5f, 0.8f));
            if (em.HasComponent<InteractionPhaseState>(entity))
                DrawBadge("MultiPhase", new Color(0.8f, 0.5f, 0.3f));
            if (em.HasComponent<MinigameConfig>(entity))
                DrawBadge("Minigame", new Color(0.7f, 0.3f, 0.7f));
            if (em.HasComponent<CoopInteraction>(entity))
                DrawBadge("Coop", new Color(0.8f, 0.8f, 0.2f));
            if (em.HasComponent<RangedInteraction>(entity))
                DrawBadge("Ranged", new Color(0.6f, 0.4f, 0.2f));

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawBadge(string label, Color color)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(label, EditorStyles.miniButton, GUILayout.Width(70));
            GUI.backgroundColor = prevBg;
        }

        // ─────────────────────────────────────────────────────
        //  Tab 2: Active Interactions
        // ─────────────────────────────────────────────────────

        private void DrawActiveInteractions(EntityManager em)
        {
            EditorGUILayout.LabelField("Active Interactions", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // InteractableState.IsBeingInteracted
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<InteractableState>());
            var entities = query.ToEntityArray(Allocator.Temp);

            int activeCount = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.Exists(entity)) continue;

                var iState = em.GetComponentData<InteractableState>(entity);
                if (!iState.IsBeingInteracted) continue;
                activeCount++;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Header
                string typeName = "Unknown";
                if (em.HasComponent<Interactable>(entity))
                    typeName = em.GetComponentData<Interactable>(entity).Type.ToString();

                EditorGUILayout.LabelField(
                    $"Entity {entity.Index}:{entity.Version} — {typeName}",
                    EditorStyles.boldLabel);

                EditorGUILayout.LabelField(
                    $"  Progress: {iState.Progress:P0}  |  Performer: {iState.InteractingEntity.Index}:{iState.InteractingEntity.Version}",
                    EditorStyles.miniLabel);

                // Multi-phase details
                if (em.HasComponent<InteractionPhaseState>(entity))
                {
                    var phase = em.GetComponentData<InteractionPhaseState>(entity);
                    if (phase.IsActive)
                    {
                        EditorGUILayout.LabelField(
                            $"  Phase: {phase.CurrentPhase}  |  Phase Time: {phase.PhaseTimeElapsed:F1}s  |  Total: {phase.TotalTimeElapsed:F1}s",
                            EditorStyles.miniLabel);
                        if (phase.PhaseFailed)
                            EditorGUILayout.LabelField("  STATUS: PHASE FAILED", GetRedStyle());
                    }
                }

                // Station details
                if (em.HasComponent<InteractionSession>(entity))
                {
                    var session = em.GetComponentData<InteractionSession>(entity);
                    EditorGUILayout.LabelField(
                        $"  Station: {session.SessionType}  |  Occupied: {session.IsOccupied}  |  SessionID: {session.SessionID}",
                        EditorStyles.miniLabel);
                }

                // Mount details
                if (em.HasComponent<MountPoint>(entity))
                {
                    var mount = em.GetComponentData<MountPoint>(entity);
                    EditorGUILayout.LabelField(
                        $"  Mount: {mount.Type}  |  Occupied: {mount.IsOccupied}  |  Rider: {mount.OccupantEntity.Index}:{mount.OccupantEntity.Version}",
                        EditorStyles.miniLabel);
                }

                // Minigame details
                if (em.HasComponent<MinigameState>(entity))
                {
                    var mg = em.GetComponentData<MinigameState>(entity);
                    if (mg.IsActive)
                    {
                        EditorGUILayout.LabelField(
                            $"  Minigame: Active  |  Time Left: {mg.TimeRemaining:F1}s  |  Score: {mg.Score:F0}",
                            EditorStyles.miniLabel);
                    }
                    else if (mg.Succeeded)
                        EditorGUILayout.LabelField("  Minigame: SUCCEEDED", GetGreenStyle());
                    else if (mg.Failed)
                        EditorGUILayout.LabelField("  Minigame: FAILED", GetRedStyle());
                }

                EditorGUILayout.EndVertical();
            }

            if (activeCount == 0)
            {
                EditorGUILayout.LabelField("  No active interactions.", EditorStyles.miniLabel);
            }

            entities.Dispose();
        }

        // ─────────────────────────────────────────────────────
        //  Tab 3: Zones & Coop
        // ─────────────────────────────────────────────────────

        private void DrawZonesAndCoop(EntityManager em)
        {
            // --- Proximity Zones ---
            EditorGUILayout.LabelField("Proximity Zones", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var zoneQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ProximityZone>());
            var zoneEntities = zoneQuery.ToEntityArray(Allocator.Temp);

            if (zoneEntities.Length == 0)
            {
                EditorGUILayout.LabelField("  No proximity zones.", EditorStyles.miniLabel);
            }

            for (int i = 0; i < zoneEntities.Length; i++)
            {
                var entity = zoneEntities[i];
                if (!em.Exists(entity)) continue;

                var zone = em.GetComponentData<ProximityZone>(entity);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    $"Zone {entity.Index}:{entity.Version} — {zone.Effect}",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"  Radius: {zone.Radius:F1}  |  Occupants: {zone.CurrentOccupantCount}  |  Tick Ready: {zone.EffectTickReady}",
                    EditorStyles.miniLabel);

                // Show occupant buffer
                if (em.HasBuffer<ProximityZoneOccupant>(entity))
                {
                    var occupants = em.GetBuffer<ProximityZoneOccupant>(entity, true);
                    for (int j = 0; j < occupants.Length; j++)
                    {
                        EditorGUILayout.LabelField(
                            $"    Occupant {j}: Entity {occupants[j].OccupantEntity.Index}:{occupants[j].OccupantEntity.Version}  |  Time: {occupants[j].TimeInZone:F1}s",
                            EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.EndVertical();
            }
            zoneEntities.Dispose();

            EditorGUILayout.Space(12);

            // --- Cooperative Interactions ---
            EditorGUILayout.LabelField("Cooperative Interactions", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var coopQuery = em.CreateEntityQuery(ComponentType.ReadOnly<CoopInteraction>());
            var coopEntities = coopQuery.ToEntityArray(Allocator.Temp);

            if (coopEntities.Length == 0)
            {
                EditorGUILayout.LabelField("  No cooperative interactions.", EditorStyles.miniLabel);
            }

            for (int i = 0; i < coopEntities.Length; i++)
            {
                var entity = coopEntities[i];
                if (!em.Exists(entity)) continue;

                var coop = em.GetComponentData<CoopInteraction>(entity);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    $"Coop {entity.Index}:{entity.Version} — {coop.Mode}",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"  Players: {coop.CurrentPlayers}/{coop.RequiredPlayers}  |  All Ready: {coop.AllPlayersReady}",
                    EditorStyles.miniLabel);

                if (coop.Mode == CoopMode.Parallel || coop.Mode == CoopMode.Asymmetric)
                {
                    EditorGUI.ProgressBar(
                        EditorGUILayout.GetControlRect(false, 16),
                        coop.ChannelProgress,
                        $"Channel: {coop.ChannelProgress:P0}");
                }

                if (coop.Mode == CoopMode.Sequential)
                {
                    EditorGUILayout.LabelField(
                        $"  Sequence Slot: {coop.CurrentSequenceSlot}",
                        EditorStyles.miniLabel);
                }

                if (coop.CoopComplete)
                    EditorGUILayout.LabelField("  STATUS: COMPLETE", GetGreenStyle());
                else if (coop.CoopFailed)
                    EditorGUILayout.LabelField("  STATUS: FAILED", GetRedStyle());

                // Show slot buffer
                if (em.HasBuffer<CoopSlot>(entity))
                {
                    var slots = em.GetBuffer<CoopSlot>(entity, true);
                    for (int j = 0; j < slots.Length; j++)
                    {
                        var slot = slots[j];
                        string status = slot.IsOccupied
                            ? (slot.IsReady ? "READY" : "Waiting")
                            : "Empty";
                        string playerInfo = slot.IsOccupied
                            ? $"Player {slot.PlayerEntity.Index}:{slot.PlayerEntity.Version}"
                            : "---";
                        EditorGUILayout.LabelField(
                            $"    Slot {slot.SlotIndex}: {status}  |  {playerInfo}",
                            EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.EndVertical();
            }
            coopEntities.Dispose();
        }

        // ─────────────────────────────────────────────────────
        //  Styles
        // ─────────────────────────────────────────────────────

        private static GUIStyle _redStyle;
        private static GUIStyle _greenStyle;

        private static GUIStyle GetRedStyle()
        {
            if (_redStyle == null)
            {
                _redStyle = new GUIStyle(EditorStyles.miniLabel);
                _redStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
                _redStyle.fontStyle = FontStyle.Bold;
            }
            return _redStyle;
        }

        private static GUIStyle GetGreenStyle()
        {
            if (_greenStyle == null)
            {
                _greenStyle = new GUIStyle(EditorStyles.miniLabel);
                _greenStyle.normal.textColor = new Color(0.3f, 0.9f, 0.3f);
                _greenStyle.fontStyle = FontStyle.Bold;
            }
            return _greenStyle;
        }
    }
}
#endif
