using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Survival.Explosives
{
    /// <summary>
    /// Client-side system that handles explosion visual effects.
    /// Listens for ExplosionEvent components and triggers VFX/audio.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ExplosionVFXSystem : ISystem
    {
        private NetworkTick _lastProcessedTick;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();

            foreach (var explosionEvent in
                     SystemAPI.Query<RefRO<ExplosionEvent>>())
            {
                var evt = explosionEvent.ValueRO;

                // Skip if already processed this tick
                if (evt.Tick.TicksSince(_lastProcessedTick) <= 0)
                    continue;

                // Process explosion VFX
                // The presentation layer (MonoBehaviour) will read this and spawn effects
                // This ECS system just ensures the event data is available

                // Calculate effect scale based on blast radius
                float scale = evt.BlastRadius / 4f; // Normalize to medium charge size

                // Log for debugging (remove in production)
                // UnityEngine.Debug.Log($"Explosion VFX at {evt.Position} type={evt.Type} radius={evt.BlastRadius}");

                _lastProcessedTick = evt.Tick;
            }
        }
    }

    /// <summary>
    /// Client-side system that syncs explosive visual state to presentation layer.
    /// Updates warning light and beep audio state.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ExplosiveWarningDisplaySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Sync explosive visual state to presentation layer
            foreach (var (explosive, visualState) in
                     SystemAPI.Query<RefRO<PlacedExplosive>, RefRO<ExplosiveVisualState>>())
            {
                // Presentation layer (MonoBehaviour) reads:
                // - visualState.IsBeeping: whether to play beep audio
                // - visualState.BeepInterval: time between beeps
                // - visualState.LightOn: warning light state
                // - explosive.FuseTimeRemaining / explosive.InitialFuseTime: for UI countdown

                // No additional processing needed - data is already in components
            }
        }
    }

    /// <summary>
    /// Provides explosion event data for hybrid presentation layer.
    /// Stores latest explosion for MonoBehaviour to consume.
    /// </summary>
    public struct LatestExplosionDisplay : IComponentData
    {
        /// <summary>
        /// Position of the most recent explosion.
        /// </summary>
        public float3 Position;

        /// <summary>
        /// Type of explosive that detonated.
        /// </summary>
        public ExplosiveType Type;

        /// <summary>
        /// Blast radius for VFX scaling.
        /// </summary>
        public float BlastRadius;

        /// <summary>
        /// True if this explosion hasn't been displayed yet.
        /// </summary>
        public bool Pending;
    }

    /// <summary>
    /// Updates LatestExplosionDisplay singleton with newest explosion.
    /// Hybrid presentation layer consumes this.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(ExplosionVFXSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ExplosionDisplayUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Find most recent explosion event
            NetworkTick latestTick = default;
            ExplosionEvent? latestEvent = null;

            foreach (var explosionEvent in
                     SystemAPI.Query<RefRO<ExplosionEvent>>())
            {
                var evt = explosionEvent.ValueRO;
                if (!latestTick.IsValid || evt.Tick.IsNewerThan(latestTick))
                {
                    latestTick = evt.Tick;
                    latestEvent = evt;
                }
            }

            // Update singleton if we found an explosion
            if (latestEvent.HasValue && SystemAPI.HasSingleton<LatestExplosionDisplay>())
            {
                var evt = latestEvent.Value;
                SystemAPI.SetSingleton(new LatestExplosionDisplay
                {
                    Position = evt.Position,
                    Type = evt.Type,
                    BlastRadius = evt.BlastRadius,
                    Pending = true
                });
            }
        }
    }
}
