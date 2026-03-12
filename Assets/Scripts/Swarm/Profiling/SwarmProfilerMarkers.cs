using Unity.Profiling;

namespace DIG.Swarm.Profiling
{
    /// <summary>
    /// EPIC 16.2: Profiler markers for all swarm systems.
    /// Usage: using (SwarmProfilerMarkers.FlowFieldBuild.Auto()) { ... }
    /// </summary>
    public static class SwarmProfilerMarkers
    {
        public static readonly ProfilerMarker FlowFieldBuild =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Swarm.FlowField.Build");

        public static readonly ProfilerMarker ParticleMovement =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Swarm.Particle.Movement");

        public static readonly ProfilerMarker ParticleSeparation =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Swarm.Particle.Separation");

        public static readonly ProfilerMarker TierEvaluation =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Swarm.Tier.Evaluation");

        public static readonly ProfilerMarker TierPromotion =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Swarm.Tier.Promotion");

        public static readonly ProfilerMarker TierDemotion =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Swarm.Tier.Demotion");

        public static readonly ProfilerMarker RenderBuild =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Swarm.Render.Build");

        public static readonly ProfilerMarker RenderDraw =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Swarm.Render.Draw");

        public static readonly ProfilerMarker AreaDamage =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Swarm.Damage.Area");

        public static readonly ProfilerMarker Spawner =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Swarm.Spawner");

        public static readonly ProfilerMarker CombatBehavior =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Swarm.CombatBehavior");
    }
}
