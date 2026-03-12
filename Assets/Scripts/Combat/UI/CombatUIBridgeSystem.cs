using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using DIG.Combat.Systems;
using DIG.Combat.UI.Adapters;
using DIG.Targeting.Theming;
using DIG.Items;
using DIG.Party;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Combat.UI
{
    /// <summary>
    /// Bridge system that reads ECS combat events and forwards them to registered UI providers.
    /// This decouples the ECS combat systems from any specific UI implementation.
    /// EPIC 15.9: Extended with hitmarker, directional damage, combo, and kill feed integration.
    /// EPIC 15.22: Extended with ResultFlags passthrough, defensive feedback, and contextual events.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class CombatUIBridgeSystem : SystemBase
    {
        private EntityQuery _combatResultsQuery;
        private EntityQuery _deathEventsQuery;
        private ComponentLookup<ItemDefinition> _itemDefLookup;

        // Cache player entity for feedback distinction
        private Entity _playerEntity;
        private int _localPlayerNetworkId = -1;

        // EPIC 16.11: One-time diagnostic check — delayed to allow MonoBehaviour OnEnable + player spawn
        private int _diagnosticFrameCounter;
        private const int DiagnosticGraceFrames = 60; // ~1s at 60fps

        // Cache player position for directional damage
        private float3 _playerPosition;

        // Cache last weapon used by player (for kill feed)
        private Entity _lastPlayerWeaponEntity;
        private FixedString64Bytes _lastPlayerWeaponName;
        private string _lastPlayerWeaponNameStr = "Weapon";

        // Cached name strings to avoid per-frame ToString() GC allocations
        private static readonly string UnknownName = "Unknown";
        private static readonly string PlayerName = "Player";
        private static readonly string DefaultWeaponName = "Weapon";

        // Entity name cache — avoids FixedString.ToString() and string interpolation GC per event
        private readonly Dictionary<int, string> _entityNameCache = new(32);

        // EPIC 18.17 Phase 2: Client-side visibility support for all 5 modes
        private readonly HashSet<int> _partyMemberNetworkIds = new(8);
        private float _nearbyDistanceSq;
        private ComponentLookup<PartyLink> _partyLinkLookup;
        private ComponentLookup<GhostOwner> _ghostOwnerLookup;
        private BufferLookup<PartyMemberElement> _partyMemberLookup;

        protected override void OnCreate()
        {
            _combatResultsQuery = GetEntityQuery(ComponentType.ReadOnly<CombatResultEvent>());
            _deathEventsQuery = GetEntityQuery(ComponentType.ReadOnly<DeathEvent>());
            _itemDefLookup = GetComponentLookup<ItemDefinition>(true);
            _partyLinkLookup = GetComponentLookup<PartyLink>(true);
            _ghostOwnerLookup = GetComponentLookup<GhostOwner>(true);
            _partyMemberLookup = GetBufferLookup<PartyMemberElement>(true);
            _lastPlayerWeaponName = "Weapon";
        }

        protected override void OnUpdate()
        {
            // EPIC 16.11: One-time diagnostics after grace period for MonoBehaviour registration + player spawn
            if (_diagnosticFrameCounter < DiagnosticGraceFrames)
            {
                _diagnosticFrameCounter++;
                if (_diagnosticFrameCounter == DiagnosticGraceFrames)
                    RunStartupDiagnostics();
            }

            // Skip if no UI providers registered and no bootstrap
            bool hasProviders = CombatUIRegistry.HasDamageNumbers ||
                               CombatUIRegistry.HasFeedback ||
                               CombatUIRegistry.HasCombatLog ||
                               CombatUIRegistry.HasKillFeed;
            bool hasBootstrap = CombatUIBootstrap.Instance != null;

            if (!hasProviders && !hasBootstrap)
                return;

            _itemDefLookup.Update(this);

            // Process combat results from CombatResolutionSystem pipeline
            foreach (var result in SystemAPI.Query<RefRO<CombatResultEvent>>())
            {
                var combat = result.ValueRO;
                ProcessCombatResult(in combat);
            }

            // EPIC 15.30: Single visual path — all damage numbers flow through DamageVisualQueue
            // Hoist provider checks + adapter cast outside the loop to avoid per-event overhead
            var damageProvider = CombatUIRegistry.HasDamageNumbers ? CombatUIRegistry.DamageNumbers : null;
            var dotAdapter = damageProvider as DamageNumbersProAdapter;

            // Visibility filter: read once per frame (cached per-frame in DamageNumberVisibilitySettings)
            var visibility = DamageNumberVisibilitySettings.EffectiveVisibility;
            int localNetId = GetLocalPlayerNetworkId();

            // Refresh party + nearby caches when needed
            if (visibility == DamageNumberVisibility.Party)
                RefreshPartyMemberCache();
            if (visibility == DamageNumberVisibility.Nearby)
                RefreshNearbyDistance();

            while (DamageVisualQueue.TryDequeue(out var visualData))
            {
                if (damageProvider == null)
                    continue;

                bool show = ShouldShowDamageNumber(visibility, in visualData, localNetId);

                if (!show)
                    continue;

                if (visualData.Damage <= 0f)
                {
                    damageProvider.ShowMiss(visualData.HitPosition);
                    continue;
                }

                if (visualData.IsDOT)
                {
                    if (dotAdapter != null)
                        dotAdapter.ShowDOTTick(visualData.HitPosition, visualData.Damage, visualData.DamageType);
                    else
                        damageProvider.ShowDamageNumber(
                            visualData.Damage, visualData.HitPosition,
                            visualData.HitType, visualData.DamageType, visualData.Flags);
                }
                else
                {
                    damageProvider.ShowDamageNumber(
                        visualData.Damage, visualData.HitPosition,
                        visualData.HitType, visualData.DamageType, visualData.Flags);
                }
            }

            // EPIC 15.30: Process status effect application events
            var floatingText = CombatUIRegistry.HasFloatingText ? CombatUIRegistry.FloatingText : null;

            while (StatusVisualQueue.TryDequeue(out var status))
            {
                if (floatingText == null) continue;
                var uiType = StatusEffectTypeConverter.ToUI(status.Type);
                if (uiType != DIG.Combat.UI.StatusEffectType.None)
                    floatingText.ShowStatusApplied(uiType, status.Position);
            }

            // Process death events
            foreach (var death in SystemAPI.Query<RefRO<DeathEvent>>())
            {
                var deathEvent = death.ValueRO;
                ProcessDeathEvent(in deathEvent);
            }
        }

        private void ProcessCombatResult(in CombatResultEvent combat)
        {
            bool isPlayerAttacker = combat.AttackerEntity == _playerEntity;
            bool isPlayerTarget = combat.TargetEntity == _playerEntity;
            var bootstrap = CombatUIBootstrap.Instance;

            // Track last weapon used by player for kill feed
            if (isPlayerAttacker && combat.WeaponEntity != Entity.Null &&
                combat.WeaponEntity != _lastPlayerWeaponEntity)
            {
                _lastPlayerWeaponEntity = combat.WeaponEntity;
                _lastPlayerWeaponName = GetWeaponName(combat.WeaponEntity);
                _lastPlayerWeaponNameStr = _lastPlayerWeaponName.ToString();
            }

            // EPIC 15.22: Determine if this is a defensive result
            bool isDefensiveResult = combat.HitType == HitType.Blocked ||
                                     combat.HitType == HitType.Parried ||
                                     combat.HitType == HitType.Immune;

            // Defensive/miss damage numbers now flow through DamageVisualQueue
            // (enqueued by DamageApplicationSystem) — no CRE path needed.

            // EPIC 15.22: Show contextual floating text for special flags
            if (CombatUIRegistry.HasFloatingText && combat.Flags != ResultFlags.None)
            {
                ShowContextualFloatingText(combat.HitPoint, combat.Flags);
            }

            // EPIC 15.9/15.22: Hitmarker and Combo (player deals damage)
            if (isPlayerAttacker && combat.DidHit && !isDefensiveResult && bootstrap != null)
            {
                // Show hitmarker
                bootstrap.ShowHitmarker(combat.HitType);

                // Register combo hit
                bootstrap.RegisterComboHit();

                // Kill confirmation
                if (combat.TargetKilled)
                {
                    bootstrap.ShowKillmarker(
                        combat.HitType == HitType.Critical || combat.HitType == HitType.Execute);
                }
            }

            // EPIC 15.9: Directional damage and combo break (player takes damage)
            if (isPlayerTarget && combat.DidHit && !isDefensiveResult && bootstrap != null)
            {
                // Show directional damage indicator
                bootstrap.ShowDirectionalDamage(
                    new Vector3(combat.HitPoint.x, combat.HitPoint.y, combat.HitPoint.z),
                    combat.FinalDamage
                );

                // Break combo when player takes damage
                bootstrap.BreakCombo();
            }

            // Feedback effects
            if (CombatUIRegistry.HasFeedback)
            {
                if (isPlayerAttacker && combat.DidHit)
                {
                    CombatUIRegistry.Feedback.OnPlayerDealtDamage(
                        combat.FinalDamage,
                        combat.HitType,
                        combat.DamageType
                    );

                    // Hit stop for crits and executes
                    if (combat.HitType == HitType.Critical)
                    {
                        CombatUIRegistry.Feedback.TriggerHitStop(0.05f);
                    }
                    else if (combat.HitType == HitType.Execute)
                    {
                        CombatUIRegistry.Feedback.TriggerHitStop(0.1f);
                    }
                }
                else if (isPlayerTarget && combat.DidHit && !isDefensiveResult)
                {
                    CombatUIRegistry.Feedback.OnPlayerTookDamage(
                        combat.FinalDamage,
                        combat.HitType,
                        combat.DamageType
                    );

                    // Screen shake when player takes damage
                    float shakeIntensity = Mathf.Clamp01(combat.FinalDamage / 100f);
                    CombatUIRegistry.Feedback.TriggerCameraShake(shakeIntensity, 0.15f);
                }
                else if (isPlayerTarget && isDefensiveResult)
                {
                    // EPIC 15.22: Reduced feedback for blocked/parried hits
                    if (combat.HitType == HitType.Parried)
                    {
                        CombatUIRegistry.Feedback.TriggerHitStop(0.03f);
                    }
                }
            }

            // Combat log
            if (CombatUIRegistry.HasCombatLog)
            {
                CombatUIRegistry.CombatLog.LogCombatEvent(new CombatLogEntry
                {
                    AttackerName = GetEntityName(combat.AttackerEntity),
                    TargetName = GetEntityName(combat.TargetEntity),
                    Damage = combat.FinalDamage,
                    HitType = combat.HitType,
                    DamageType = combat.DamageType,
                    TargetKilled = combat.TargetKilled,
                    Timestamp = (float)SystemAPI.Time.ElapsedTime
                });
            }
        }

        /// <summary>
        /// EPIC 15.22: Show contextual floating text for special combat events.
        /// </summary>
        private void ShowContextualFloatingText(float3 hitPoint, ResultFlags flags)
        {
            // Offset slightly above the damage number
            float3 textPos = hitPoint + new float3(0, 0.5f, 0);

            if ((flags & ResultFlags.PoiseBreak) != 0)
            {
                CombatUIRegistry.FloatingText.ShowText("BROKEN", textPos, FloatingTextStyle.Important);
            }
        }

        private void ProcessDeathEvent(in DeathEvent death)
        {
            bool wasPlayer = death.DyingEntity == _playerEntity;

            // Feedback
            if (CombatUIRegistry.HasFeedback)
            {
                CombatUIRegistry.Feedback.OnEntityKilled(wasPlayer, death.DamageType);
            }

            // EPIC 15.9: Kill feed
            if (CombatUIRegistry.HasKillFeed && !wasPlayer)
            {
                // Use cached weapon name if killer is player
                string weaponName = death.KillerEntity == _playerEntity
                    ? _lastPlayerWeaponNameStr
                    : DefaultWeaponName;

                CombatUIRegistry.KillFeed.AddKill(new KillFeedEntry
                {
                    KillerName = GetEntityName(death.KillerEntity),
                    VictimName = GetEntityName(death.DyingEntity),
                    Type = DetermineKillType(death),
                    WeaponName = weaponName,
                    IsLocalPlayerKiller = death.KillerEntity == _playerEntity,
                    IsLocalPlayerVictim = false,
                    Timestamp = (float)SystemAPI.Time.ElapsedTime
                });
            }
        }

        private KillType DetermineKillType(in DeathEvent death)
        {
            // Determine kill type based on damage type
            return death.DamageType switch
            {
                DamageType.Fire or DamageType.Lightning => KillType.Explosive,
                _ => KillType.Normal
            };
        }

        private string GetEntityName(Entity entity)
        {
            if (entity == Entity.Null)
                return UnknownName;

            if (entity == _playerEntity)
                return PlayerName;

            // Check cache first — avoids ToString() and string interpolation GC
            int idx = entity.Index;
            if (_entityNameCache.TryGetValue(idx, out var cached))
                return cached;

            // Try to get display name from ItemDefinition (cached ComponentLookup)
            string name;
            if (_itemDefLookup.HasComponent(entity))
            {
                var itemDef = _itemDefLookup[entity];
                name = itemDef.DisplayName.Length > 0
                    ? itemDef.DisplayName.ToString()
                    : string.Concat("Enemy_", idx.ToString());
            }
            else
            {
                name = string.Concat("Enemy_", idx.ToString());
            }

            _entityNameCache[idx] = name;
            return name;
        }

        private FixedString64Bytes GetWeaponName(Entity weaponEntity)
        {
            if (weaponEntity == Entity.Null)
                return DefaultWeaponName;

            if (_itemDefLookup.HasComponent(weaponEntity))
            {
                var itemDef = _itemDefLookup[weaponEntity];
                if (itemDef.DisplayName.Length > 0)
                    return itemDef.DisplayName;
            }

            return DefaultWeaponName;
        }

        /// <summary>
        /// Set the player entity for feedback distinction.
        /// Call this when player spawns.
        /// </summary>
        public void SetPlayerEntity(Entity player)
        {
            _playerEntity = player;
            // Cache NetworkId for visibility filtering
            if (player != Entity.Null && EntityManager.HasComponent<GhostOwner>(player))
                _localPlayerNetworkId = EntityManager.GetComponentData<GhostOwner>(player).NetworkId;
            else
                _localPlayerNetworkId = -1;
        }

        private int GetLocalPlayerNetworkId()
        {
            // Re-resolve if not yet cached (player may spawn after first frame)
            if (_localPlayerNetworkId == -1 && _playerEntity != Entity.Null
                && EntityManager.HasComponent<GhostOwner>(_playerEntity))
            {
                _localPlayerNetworkId = EntityManager.GetComponentData<GhostOwner>(_playerEntity).NetworkId;
            }
            return _localPlayerNetworkId;
        }

        /// <summary>
        /// Update player position for directional damage calculation.
        /// </summary>
        public void SetPlayerPosition(float3 position)
        {
            _playerPosition = position;
        }

        // ─────────────────────────────────────────────────────────────────
        // EPIC 18.17 Phase 2: Visibility filter helpers
        // ─────────────────────────────────────────────────────────────────

        private bool ShouldShowDamageNumber(
            DamageNumberVisibility vis, in DamageVisualData data, int localNetId)
        {
            // Environment/unknown damage is always shown
            if (data.SourceNetworkId == -1) return true;

            return vis switch
            {
                DamageNumberVisibility.All => true,
                DamageNumberVisibility.SelfOnly => data.SourceNetworkId == localNetId,
                DamageNumberVisibility.Nearby =>
                    math.distancesq(data.HitPosition, _playerPosition) <= _nearbyDistanceSq,
                DamageNumberVisibility.Party =>
                    data.SourceNetworkId == localNetId || _partyMemberNetworkIds.Contains(data.SourceNetworkId),
                DamageNumberVisibility.None => false,
                _ => true
            };
        }

        private void RefreshPartyMemberCache()
        {
            _partyLinkLookup.Update(this);
            _ghostOwnerLookup.Update(this);
            _partyMemberLookup.Update(this);

            _partyMemberNetworkIds.Clear();

            if (_playerEntity == Entity.Null || !_partyLinkLookup.HasComponent(_playerEntity))
                return;

            var partyEntity = _partyLinkLookup[_playerEntity].PartyEntity;
            if (partyEntity == Entity.Null || !_partyMemberLookup.HasBuffer(partyEntity))
                return;

            var members = _partyMemberLookup[partyEntity];
            for (int i = 0; i < members.Length; i++)
            {
                var memberPlayer = members[i].PlayerEntity;
                if (memberPlayer != Entity.Null && _ghostOwnerLookup.HasComponent(memberPlayer))
                    _partyMemberNetworkIds.Add(_ghostOwnerLookup[memberPlayer].NetworkId);
            }
        }

        private void RefreshNearbyDistance()
        {
            var config = DamageVisibilityConfig.Instance;
            float dist = config != null ? config.NearbyDistance : 50f;
            _nearbyDistanceSq = dist * dist;
        }

        /// <summary>
        /// EPIC 16.11: Log warnings for missing scene components on first frame.
        /// Prevents silent failures when adapters/bootstrap are not in the scene.
        /// </summary>
        private void RunStartupDiagnostics()
        {
            if (!CombatUIRegistry.HasDamageNumbers)
                UnityEngine.Debug.LogWarning("[CombatUI] No IDamageNumberProvider registered. " +
                    "Add DamageNumbersProAdapter (or DamageNumberAdapterBase subclass) to your scene Canvas. " +
                    "All damage numbers will be silently dropped.");

            if (CombatUIBootstrap.Instance == null)
                UnityEngine.Debug.LogWarning("[CombatUI] CombatUIBootstrap.Instance is null. " +
                    "Add CombatUIBootstrap to your scene for hitmarkers, directional damage, combo, and kill feed.");

            if (!CombatUIRegistry.HasFeedback)
                UnityEngine.Debug.LogWarning("[CombatUI] No ICombatFeedbackProvider registered. " +
                    "Hit stop and camera shake will not function.");

            if (_playerEntity == Entity.Null)
                UnityEngine.Debug.LogWarning("[CombatUI] _playerEntity is Entity.Null on first update. " +
                    "CombatUIPlayerBindingSystem should set this when the local player spawns. " +
                    "Player-specific feedback (hitmarkers, combo, directional damage) is disabled until bound.");
        }
    }
}
