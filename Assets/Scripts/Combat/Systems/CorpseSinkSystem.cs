using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Combat.Components;
using DIG.VFX;
using Player.Components;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// EPIC 16.3: Client-side corpse sink-into-ground presentation.
    /// After ragdoll + corpse persistence time elapses, sinks the corpse downward
    /// at a constant rate over FadeOutDuration. Zero shader/art dependency.
    ///
    /// Timing is derived from replicated DeathState.StateStartTime + global CorpseConfig,
    /// so this works on both local and remote clients without replicating CorpseState.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct CorpseSinkSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            float dt = SystemAPI.Time.DeltaTime;

            if (dt <= 0f) return;

            // Global config (or defaults if no authoring placed)
            float ragdollDur = 2.0f;
            float corpseLife = 15.0f;
            float fadeDur = 1.5f;
            if (SystemAPI.HasSingleton<CorpseConfig>())
            {
                var cfg = SystemAPI.GetSingleton<CorpseConfig>();
                ragdollDur = cfg.RagdollDuration;
                corpseLife = cfg.CorpseLifetime;
                fadeDur = cfg.FadeOutDuration;
            }

            float sinkStartDelay = ragdollDur + corpseLife;
            float sinkDepth = 1.5f;
            float sinkRate = sinkDepth / math.max(fadeDur, 0.01f);

            foreach (var (transform, deathState) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<DeathState>>()
                     .WithNone<PlayerTag>()
                     .WithNone<DissolveCapable>())
            {
                if (deathState.ValueRO.Phase == DeathPhase.Alive)
                    continue;

                float timeSinceDeath = currentTime - deathState.ValueRO.StateStartTime;

                if (timeSinceDeath >= sinkStartDelay && timeSinceDeath < sinkStartDelay + fadeDur)
                {
                    var pos = transform.ValueRO.Position;
                    pos.y -= sinkRate * dt;
                    transform.ValueRW.Position = pos;
                }
            }
        }
    }
}
