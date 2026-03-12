using Unity.Entities;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;

namespace DIG.Combat.UI
{
    /// <summary>
    /// EPIC 16.11: Binds the local player entity to CombatUIBridgeSystem so that
    /// player-specific combat feedback (hitmarkers, directional damage, combo, kill feed,
    /// camera shake) can distinguish player-as-attacker from player-as-target.
    ///
    /// Also updates player world position each frame for directional damage indicators.
    ///
    /// Follows the same pattern as EquipmentProviderBindingSystem (Items).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(CombatUIBridgeSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class CombatUIPlayerBindingSystem : SystemBase
    {
        private EntityQuery _localPlayerQuery;
        private EntityQuery _fallbackPlayerQuery;
        private Entity _cachedPlayerEntity;
        private CombatUIBridgeSystem _bridgeSystem;

        protected override void OnCreate()
        {
            // Primary: NetCode local player (ClientSimulation)
            _localPlayerQuery = GetEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<LocalToWorld>()
            );

            // Fallback: any player entity (LocalWorld / editor play without NetCode)
            _fallbackPlayerQuery = GetEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalToWorld>()
            );
        }

        protected override void OnUpdate()
        {
            // Lazy-fetch bridge system (created by Unity, may not exist on first frame)
            if (_bridgeSystem == null)
            {
                _bridgeSystem = World.GetExistingSystemManaged<CombatUIBridgeSystem>();
                if (_bridgeSystem == null)
                    return;
            }

            // Prefer GhostOwnerIsLocal query, fall back to any PlayerTag
            var query = !_localPlayerQuery.IsEmpty ? _localPlayerQuery : _fallbackPlayerQuery;
            if (query.CalculateEntityCount() != 1)
                return;

            var entity = query.GetSingletonEntity();

            // Bind on first discovery or entity change (respawn creates new entity)
            if (entity != _cachedPlayerEntity)
            {
                _cachedPlayerEntity = entity;
                _bridgeSystem.SetPlayerEntity(entity);
            }

            // Update position each frame for directional damage indicators
            var ltw = SystemAPI.GetComponent<LocalToWorld>(entity);
            _bridgeSystem.SetPlayerPosition(ltw.Position);
        }
    }
}
