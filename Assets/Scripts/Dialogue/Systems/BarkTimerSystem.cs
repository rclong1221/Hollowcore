using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Ticks bark cooldowns and creates BarkRequest transient entities
    /// when an NPC with a BarkEmitter is near a player. Client|Local only (barks are cosmetic).
    /// Frame-spread using entityIndex % BarkCheckFrameSpread to prevent thundering herd.
    /// Pre-computes local player positions once per frame to avoid nested O(N*M) query.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class BarkTimerSystem : SystemBase
    {
        private int _frameCount;
        private EntityQuery _localPlayerQuery;
        private ComponentLookup<DialogueSessionState> _sessionLookup;

        protected override void OnCreate()
        {
            RequireForUpdate<DialogueConfig>();
            _localPlayerQuery = GetEntityQuery(
                ComponentType.ReadOnly<LocalToWorld>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
            _sessionLookup = GetComponentLookup<DialogueSessionState>(true);
        }

        protected override void OnUpdate()
        {
            _frameCount++;
            _sessionLookup.Update(this);

            var config = SystemAPI.GetSingleton<DialogueConfig>();
            float time = (float)SystemAPI.Time.ElapsedTime;
            int spreadFrames = math.max(1, config.BarkCheckFrameSpread);
            float rangeSq = config.BarkProximityRange * config.BarkProximityRange;

            // Pre-compute local player positions once per frame
            var playerPositions = _localPlayerQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            if (playerPositions.Length == 0)
            {
                playerPositions.Dispose();
                return;
            }

            int entityIndex = 0;
            foreach (var (emitter, ltw, entity) in
                SystemAPI.Query<RefRW<BarkEmitter>, RefRO<LocalToWorld>>()
                    .WithEntityAccess())
            {
                entityIndex++;

                // Frame-slot spreading
                if (entityIndex % spreadFrames != _frameCount % spreadFrames)
                    continue;

                // Cooldown check
                if (time - emitter.ValueRO.LastBarkTime < emitter.ValueRO.BarkCooldown)
                    continue;

                // Skip if NPC is in active dialogue
                if (_sessionLookup.HasComponent(entity) && _sessionLookup[entity].IsActive)
                    continue;

                // Check proximity to any player using pre-computed positions
                float3 npcPos = ltw.ValueRO.Position;
                bool nearPlayer = false;

                for (int p = 0; p < playerPositions.Length; p++)
                {
                    if (math.distancesq(npcPos, playerPositions[p].Position) < rangeSq)
                    {
                        nearPlayer = true;
                        break;
                    }
                }

                if (!nearPlayer) continue;

                // Create bark request
                emitter.ValueRW.LastBarkTime = time;
                var barkEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(barkEntity, new BarkRequest
                {
                    EmitterEntity = entity,
                    LineIndex = -1,
                    Position = npcPos
                });
            }

            playerPositions.Dispose();
        }
    }
}
