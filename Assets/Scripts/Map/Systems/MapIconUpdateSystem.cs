using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using Unity.Collections;
using DIG.Combat.Components;
using Player.Components;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Projects entity world positions into 2D map icon buffer.
    /// Uses frame-spread pattern (DetectionSystem) to distribute work across K frames.
    /// Reads MapIcon + LocalToWorld, filters dead/corpse entities, range culls.
    /// Also adds local player and party member icons (refreshed on cycle start only).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class MapIconUpdateSystem : SystemBase
    {
        private EntityQuery _mapIconQuery;
        private EntityQuery _deathStateQuery;
        private uint _frameCount;

        protected override void OnCreate()
        {
            RequireForUpdate<MinimapConfig>();
            RequireForUpdate<MapManagedState>();
            _mapIconQuery = GetEntityQuery(
                ComponentType.ReadOnly<MapIcon>(),
                ComponentType.ReadOnly<LocalToWorld>()
            );
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<MinimapConfig>();
            var managed = SystemAPI.ManagedAPI.GetSingleton<MapManagedState>();
            if (!managed.IsInitialized) return;

            _frameCount++;

            // Find local player position
            float3 playerPos = float3.zero;
            bool foundPlayer = false;

            foreach (var ltw in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<GhostOwnerIsLocal>())
            {
                playerPos = ltw.ValueRO.Position;
                foundPlayer = true;
                break;
            }
            if (!foundPlayer) return;

            float maxRangeSq = config.MaxIconRange * config.MaxIconRange;
            int spread = math.max(1, config.UpdateFrameSpread);
            uint frameSlot = _frameCount % (uint)spread;

            // Clear icon buffer and re-add player/party at start of each spread cycle
            if (frameSlot == 0)
            {
                managed.IconBuffer.Clear();

                // Add party member positions (only once per cycle, not every frame)
                foreach (var (ltw, _) in SystemAPI.Query<RefRO<LocalToWorld>, RefRO<PlayerTag>>()
                    .WithNone<GhostOwnerIsLocal>())
                {
                    float3 partyPos = ltw.ValueRO.Position;
                    float distSq = math.distancesq(partyPos.xz, playerPos.xz);
                    if (distSq > maxRangeSq) continue;

                    managed.IconBuffer.Add(new MapIconEntry
                    {
                        WorldPos2D = partyPos.xz,
                        IconType = MapIconType.PartyMember,
                        Priority = 200,
                        ColorPacked = 0,
                        SourceEntity = Entity.Null
                    });
                }

                // Add local player icon (highest priority)
                managed.IconBuffer.Add(new MapIconEntry
                {
                    WorldPos2D = playerPos.xz,
                    IconType = MapIconType.Player,
                    Priority = 255,
                    ColorPacked = 0,
                    SourceEntity = Entity.Null
                });
            }

            // Frame-spread icon updates — batch component lookups
            var entities = _mapIconQuery.ToEntityArray(Allocator.Temp);
            var icons = _mapIconQuery.ToComponentDataArray<MapIcon>(Allocator.Temp);
            var transforms = _mapIconQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            var deathLookup = GetComponentLookup<DeathState>(true);
            var corpseLookup = GetComponentLookup<CorpseState>(true);
            var hitboxLookup = GetComponentLookup<HasHitboxes>(true);

            for (int i = 0; i < entities.Length; i++)
            {
                // Frame-spread: only process 1/K entities per frame
                if ((uint)entities[i].Index % (uint)spread != frameSlot) continue;

                var entity = entities[i];

                // Skip dead entities
                if (deathLookup.HasComponent(entity))
                {
                    var deathState = deathLookup[entity];
                    if (deathState.Phase != global::Player.Components.DeathPhase.Alive) continue;
                }

                // Skip corpses (CorpseState is IEnableableComponent, enabled = dead)
                if (corpseLookup.HasComponent(entity) &&
                    EntityManager.IsComponentEnabled<CorpseState>(entity))
                    continue;

                if (!icons[i].VisibleOnMinimap) continue;

                // Range cull
                float3 worldPos = transforms[i].Position;
                float distSq = math.distancesq(worldPos.xz, playerPos.xz);
                if (distSq > maxRangeSq) continue;

                // Enemy filter: require HasHitboxes to avoid phantom ghost duplicates
                if (icons[i].IconType == MapIconType.Enemy || icons[i].IconType == MapIconType.Boss)
                {
                    if (!hitboxLookup.HasComponent(entity)) continue;
                }

                managed.IconBuffer.Add(new MapIconEntry
                {
                    WorldPos2D = worldPos.xz,
                    IconType = icons[i].IconType,
                    Priority = icons[i].Priority,
                    ColorPacked = icons[i].CustomColorPacked,
                    SourceEntity = entity
                });
            }

            entities.Dispose();
            icons.Dispose();
            transforms.Dispose();
        }
    }
}
