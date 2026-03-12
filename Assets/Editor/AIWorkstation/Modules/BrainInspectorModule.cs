using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.AI.Components;
using DIG.Aggro.Components;
using DIG.Combat.Resources;

namespace DIG.Editor.AIWorkstation.Modules
{
    /// <summary>
    /// Brain Inspector: Unified per-entity debug view showing HFSM state,
    /// threat table, ability cooldowns, target info, and leash gauge.
    /// </summary>
    public class BrainInspectorModule : IAIWorkstationModule
    {
        private Entity _entity = Entity.Null;
        private EntityManager _em;

        public void OnEntityChanged(Entity entity, EntityManager entityManager)
        {
            _entity = entity;
            _em = entityManager;
        }

        public void OnSceneGUI(SceneView sceneView) { }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Brain Inspector", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode and select an AI entity to inspect.", MessageType.Info);
                return;
            }

            var world = AIWorkstationWindow.GetAIWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            _em = world.EntityManager;

            if (_entity == Entity.Null || !_em.Exists(_entity))
            {
                EditorGUILayout.HelpBox("No entity selected. Use the entity selector above or click 'Pick Entity' and click an enemy in the Scene view.", MessageType.Info);
                return;
            }

            if (!_em.HasComponent<AIState>(_entity))
            {
                EditorGUILayout.HelpBox($"Entity {_entity.Index} has no AIState component.", MessageType.Warning);
                return;
            }

            DrawHFSMState();
            DrawResourcePool();
            DrawThreatTable();
            DrawAbilityCooldowns();
            DrawTargetInfo();
            DrawLeashGauge();
            DrawConfigSummary();
        }

        private void DrawHFSMState()
        {
            AIWorkstationStyles.DrawSectionHeader("HFSM State");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var aiState = _em.GetComponentData<AIState>(_entity);
            var stateColor = AIWorkstationStyles.GetStateColor(aiState.CurrentState);

            // State + sub-state
            EditorGUILayout.BeginHorizontal();
            AIWorkstationStyles.DrawColoredLabel(aiState.CurrentState.ToString(), stateColor);
            if (aiState.CurrentState == AIBehaviorState.Combat)
            {
                EditorGUILayout.LabelField($"Sub: {aiState.SubState}", GUILayout.Width(120));
            }
            EditorGUILayout.EndHorizontal();

            // Timers
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"State Timer: {aiState.StateTimer:F1}s", GUILayout.Width(140));
            if (aiState.CurrentState == AIBehaviorState.Combat)
            {
                EditorGUILayout.LabelField($"SubState Timer: {aiState.SubStateTimer:F1}s", GUILayout.Width(160));
            }
            EditorGUILayout.EndHorizontal();

            // Ability guard
            if (_em.HasComponent<AbilityExecutionState>(_entity))
            {
                var execState = _em.GetComponentData<AbilityExecutionState>(_entity);
                if (execState.Phase != AbilityCastPhase.Idle)
                {
                    var phaseColor = AIWorkstationStyles.GetPhaseColor(execState.Phase);
                    var prevColor = GUI.color;
                    GUI.color = phaseColor;
                    EditorGUILayout.LabelField($"Transition BLOCKED: Ability {execState.Phase} (timer: {execState.PhaseTimer:F2}s)", EditorStyles.boldLabel);
                    GUI.color = prevColor;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawResourcePool()
        {
            if (!_em.HasComponent<ResourcePool>(_entity)) return;

            AIWorkstationStyles.DrawSectionHeader("Resource Pool");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var pool = _em.GetComponentData<ResourcePool>(_entity);

            DrawResourceSlot(pool.Slot0);
            DrawResourceSlot(pool.Slot1);

            if (pool.Slot0.Type == ResourceType.None && pool.Slot1.Type == ResourceType.None)
            {
                EditorGUILayout.LabelField("No active resources", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawResourceSlot(ResourceSlot slot)
        {
            if (slot.Type == ResourceType.None) return;

            Color slotColor = slot.Type switch
            {
                ResourceType.Stamina => Color.green,
                ResourceType.Mana    => new Color(0.3f, 0.5f, 1f),
                ResourceType.Energy  => Color.yellow,
                ResourceType.Rage    => Color.red,
                ResourceType.Combo   => new Color(0.7f, 0.3f, 1f),
                _ => Color.white
            };

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(slot.Type.ToString(), GUILayout.Width(60));

            var rect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
            string label = (slot.Flags & ResourceFlags.IsInteger) != 0
                ? $"{(int)slot.Current}/{(int)slot.Max}"
                : $"{slot.Current:F0}/{slot.Max:F0}";
            AIWorkstationStyles.DrawProgressBar(rect, slot.Current, slot.Max, slotColor, label);

            // Regen/decay indicator
            if ((slot.Flags & ResourceFlags.DecaysWhenIdle) != 0)
                EditorGUILayout.LabelField("DECAY", GUILayout.Width(45));
            else if (slot.RegenRate > 0f)
                EditorGUILayout.LabelField($"+{slot.RegenRate:F1}/s", GUILayout.Width(55));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawThreatTable()
        {
            if (!_em.HasBuffer<ThreatEntry>(_entity)) return;
            if (!_em.HasComponent<AggroState>(_entity)) return;

            AIWorkstationStyles.DrawSectionHeader("Threat Table");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var aggroState = _em.GetComponentData<AggroState>(_entity);
            var buffer = _em.GetBuffer<ThreatEntry>(_entity, true);

            // Aggro status line
            EditorGUILayout.BeginHorizontal();
            var statusColor = aggroState.IsAggroed ? Color.red : Color.grey;
            AIWorkstationStyles.DrawColoredLabel(
                aggroState.IsAggroed ? "AGGROED" : "PASSIVE",
                statusColor);
            EditorGUILayout.LabelField($"Entries: {buffer.Length}", GUILayout.Width(80));
            if (!aggroState.IsAggroed && aggroState.TimeSinceLastValidTarget > 0)
            {
                EditorGUILayout.LabelField($"Idle for: {aggroState.TimeSinceLastValidTarget:F1}s", GUILayout.Width(100));
            }
            EditorGUILayout.EndHorizontal();

            if (buffer.Length == 0)
            {
                EditorGUILayout.LabelField("No threat entries", EditorStyles.centeredGreyMiniLabel);
            }

            // Find max threat for bar scaling
            float maxThreat = 1f;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].ThreatValue > maxThreat) maxThreat = buffer[i].ThreatValue;
            }

            // Draw each threat entry as a bar
            for (int i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i];
                bool isLeader = entry.SourceEntity == aggroState.CurrentThreatLeader;
                Color barColor = isLeader ? AIWorkstationStyles.ThreatLeaderColor
                    : entry.IsCurrentlyVisible ? AIWorkstationStyles.ThreatVisibleColor
                    : AIWorkstationStyles.ThreatHiddenColor;

                EditorGUILayout.BeginHorizontal();

                // Entity label
                string visIcon = entry.IsCurrentlyVisible ? "V" : "H";
                string leaderIcon = isLeader ? " *" : "";
                EditorGUILayout.LabelField($"[{visIcon}] E:{entry.SourceEntity.Index}{leaderIcon}", GUILayout.Width(90));

                // Threat bar
                var rect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
                AIWorkstationStyles.DrawProgressBar(rect, entry.ThreatValue, maxThreat, barColor,
                    $"{entry.ThreatValue:F1}");

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAbilityCooldowns()
        {
            if (!_em.HasBuffer<AbilityCooldownState>(_entity)) return;
            if (!_em.HasBuffer<AbilityDefinition>(_entity)) return;

            AIWorkstationStyles.DrawSectionHeader("Ability Cooldowns");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var cooldowns = _em.GetBuffer<AbilityCooldownState>(_entity, true);
            var abilities = _em.GetBuffer<AbilityDefinition>(_entity, true);

            // Active cast info
            if (_em.HasComponent<AbilityExecutionState>(_entity))
            {
                var execState = _em.GetComponentData<AbilityExecutionState>(_entity);
                if (execState.Phase != AbilityCastPhase.Idle)
                {
                    var phaseColor = AIWorkstationStyles.GetPhaseColor(execState.Phase);
                    EditorGUILayout.BeginHorizontal();
                    var prevColor = GUI.color;
                    GUI.color = phaseColor;

                    string abilityName = execState.SelectedAbilityIndex >= 0 && execState.SelectedAbilityIndex < abilities.Length
                        ? $"Ability #{execState.SelectedAbilityIndex} (ID:{abilities[execState.SelectedAbilityIndex].AbilityId})"
                        : $"Ability #{execState.SelectedAbilityIndex}";

                    EditorGUILayout.LabelField($"CASTING: {abilityName} [{execState.Phase}] {execState.PhaseTimer:F2}s", EditorStyles.boldLabel);
                    GUI.color = prevColor;
                    EditorGUILayout.EndHorizontal();
                }
            }

            // Per-ability cooldown bars
            int count = math.min(cooldowns.Length, abilities.Length);
            for (int i = 0; i < count; i++)
            {
                var cd = cooldowns[i];
                var ability = abilities[i];

                EditorGUILayout.BeginHorizontal();

                // Label
                EditorGUILayout.LabelField($"#{i} (ID:{ability.AbilityId})", GUILayout.Width(80));

                // Cooldown bar
                if (cd.CooldownRemaining > 0f)
                {
                    var rect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
                    AIWorkstationStyles.DrawProgressBar(rect, cd.CooldownRemaining, ability.Cooldown,
                        new Color(0.8f, 0.3f, 0.3f), $"CD: {cd.CooldownRemaining:F1}s");
                }
                else
                {
                    var prevColor = GUI.color;
                    GUI.color = Color.green;
                    EditorGUILayout.LabelField("READY", EditorStyles.miniBoldLabel);
                    GUI.color = prevColor;
                }

                // Charges
                if (ability.MaxCharges > 0)
                {
                    EditorGUILayout.LabelField($"[{cd.ChargesRemaining}/{cd.MaxCharges}]", GUILayout.Width(50));
                }

                EditorGUILayout.EndHorizontal();
            }

            // GCD
            if (count > 0 && cooldowns[0].GlobalCooldownRemaining > 0f)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("GCD:", GUILayout.Width(80));
                var rect = GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true));
                AIWorkstationStyles.DrawProgressBar(rect, cooldowns[0].GlobalCooldownRemaining, 1.5f,
                    new Color(0.6f, 0.6f, 0.2f), $"{cooldowns[0].GlobalCooldownRemaining:F2}s");
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTargetInfo()
        {
            if (!_em.HasComponent<AggroState>(_entity)) return;

            AIWorkstationStyles.DrawSectionHeader("Target Info");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var aggroState = _em.GetComponentData<AggroState>(_entity);

            if (aggroState.CurrentThreatLeader == Entity.Null)
            {
                EditorGUILayout.LabelField("No target", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"Target: Entity {aggroState.CurrentThreatLeader.Index}");
                EditorGUILayout.LabelField($"Threat: {aggroState.CurrentLeaderThreat:F1}");

                // Distance
                if (_em.HasComponent<LocalTransform>(_entity) &&
                    _em.Exists(aggroState.CurrentThreatLeader) &&
                    _em.HasComponent<LocalTransform>(aggroState.CurrentThreatLeader))
                {
                    var myPos = _em.GetComponentData<LocalTransform>(_entity).Position;
                    var targetPos = _em.GetComponentData<LocalTransform>(aggroState.CurrentThreatLeader).Position;
                    float dist = math.distance(myPos, targetPos);
                    EditorGUILayout.LabelField($"Distance: {dist:F1}m");

                    // Show melee range context
                    if (_em.HasComponent<AIBrain>(_entity))
                    {
                        float meleeRange = _em.GetComponentData<AIBrain>(_entity).MeleeRange;
                        bool inRange = dist <= meleeRange;
                        var prevColor = GUI.color;
                        GUI.color = inRange ? Color.green : Color.yellow;
                        EditorGUILayout.LabelField(inRange ? "IN MELEE RANGE" : $"Out of range ({dist - meleeRange:F1}m away)", EditorStyles.miniBoldLabel);
                        GUI.color = prevColor;
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLeashGauge()
        {
            if (!_em.HasComponent<SpawnPosition>(_entity)) return;
            if (!_em.HasComponent<AggroConfig>(_entity)) return;
            if (!_em.HasComponent<LocalTransform>(_entity)) return;

            var spawnPos = _em.GetComponentData<SpawnPosition>(_entity);
            if (!spawnPos.IsInitialized) return;

            var config = _em.GetComponentData<AggroConfig>(_entity);
            if (config.LeashDistance <= 0f) return; // No leash

            AIWorkstationStyles.DrawSectionHeader("Leash");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var currentPos = _em.GetComponentData<LocalTransform>(_entity).Position;
            float distFromSpawn = math.distance(currentPos, spawnPos.Position);
            float leashMax = config.LeashDistance;
            float ratio = distFromSpawn / leashMax;

            // Color: green → yellow → red as approaching leash limit
            Color barColor = ratio < 0.6f ? Color.green : ratio < 0.85f ? Color.yellow : Color.red;

            var rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            AIWorkstationStyles.DrawProgressBar(rect, distFromSpawn, leashMax, barColor,
                $"{distFromSpawn:F1}m / {leashMax:F0}m");

            if (ratio > 0.85f)
            {
                EditorGUILayout.LabelField("WARNING: Near leash limit!", EditorStyles.miniBoldLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawConfigSummary()
        {
            if (!_em.HasComponent<AIBrain>(_entity)) return;

            AIWorkstationStyles.DrawSectionHeader("Config (Read-Only)");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var brain = _em.GetComponentData<AIBrain>(_entity);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Archetype: {brain.Archetype}", GUILayout.Width(140));
            EditorGUILayout.LabelField($"DamageType: {brain.DamageType}", GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Melee: {brain.MeleeRange:F1}m", GUILayout.Width(90));
            EditorGUILayout.LabelField($"Chase: {brain.ChaseSpeed:F1}", GUILayout.Width(90));
            EditorGUILayout.LabelField($"Patrol: {brain.PatrolSpeed:F1}", GUILayout.Width(90));
            EditorGUILayout.LabelField($"Radius: {brain.PatrolRadius:F0}m", GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Dmg: {brain.BaseDamage:F0}+/-{brain.DamageVariance:F0}", GUILayout.Width(140));
            EditorGUILayout.LabelField($"CD: {brain.AttackCooldown:F1}s", GUILayout.Width(80));
            EditorGUILayout.LabelField($"WindUp: {brain.AttackWindUp:F2}s", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }
}
