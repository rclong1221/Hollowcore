using Unity.Profiling;

namespace DIG.Performance
{
    /// <summary>
    /// Epic 7.7.1: Centralized ProfilerMarker definitions for collision system profiling.
    /// 
    /// Usage in any collision system:
    ///   using (CollisionProfilerMarkers.ProximityCollision.Auto())
    ///   {
    ///       // ... collision detection code
    ///   }
    /// 
    /// Or for subsections:
    ///   CollisionProfilerMarkers.ProximityCollision_GatherPlayers.Begin();
    ///   // ... gather players
    ///   CollisionProfilerMarkers.ProximityCollision_GatherPlayers.End();
    /// 
    /// View in Unity Profiler under "Scripts" or create custom module for "DIG.Collision".
    /// </summary>
    public static class CollisionProfilerMarkers
    {
        // === Main System Markers ===
        // These wrap entire OnUpdate() methods for high-level cost overview
        
        /// <summary>
        /// PlayerProximityCollisionSystem.OnUpdate - Main player-player proximity detection.
        /// Expected cost: O(N²) where N = player count, <0.5ms for 10 players.
        /// </summary>
        public static readonly ProfilerMarker ProximityCollision = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.ProximityDetection");
        
        /// <summary>
        /// PlayerCollisionResponseSystem.OnUpdate - Collision response calculations (power, stagger).
        /// Expected cost: O(C) where C = collision count, typically <0.1ms.
        /// </summary>
        public static readonly ProfilerMarker CollisionResponse = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Response");
        
        /// <summary>
        /// CollisionGracePeriodSystem.OnUpdate - Grace period timer ticks.
        /// Expected cost: O(G) where G = players with grace period, typically <0.05ms.
        /// </summary>
        public static readonly ProfilerMarker GracePeriod = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.GracePeriod");
        
        /// <summary>
        /// GroupIndexOverrideSystem.OnUpdate - GroupIndex auto-reset for projectiles.
        /// Expected cost: O(P) where P = active projectile owners, typically <0.02ms.
        /// </summary>
        public static readonly ProfilerMarker GroupIndexOverride = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.GroupIndexOverride");
        
        /// <summary>
        /// CollisionReconciliationSystem.OnUpdate - Prediction smoothing corrections.
        /// Expected cost: O(R) where R = entities with active reconcile, typically <0.05ms.
        /// </summary>
        public static readonly ProfilerMarker Reconciliation = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Reconciliation");
        
        /// <summary>
        /// CollisionMispredictionDetectionSystem.OnUpdate - Detects server corrections.
        /// Expected cost: O(N) where N = predicted entities, typically <0.02ms.
        /// </summary>
        public static readonly ProfilerMarker MispredictionDetection = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.MispredictionDetection");
        
        // === Subsection Markers for ProximityCollisionSystem ===
        // Use these to drill down into which part of detection is expensive
        
        /// <summary>Gathering player data into NativeList</summary>
        public static readonly ProfilerMarker ProximityCollision_GatherPlayers = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Proximity.GatherPlayers");
        
        /// <summary>O(N²) proximity distance checks (fallback when spatial hash unavailable)</summary>
        public static readonly ProfilerMarker ProximityCollision_DistanceChecks = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Proximity.DistanceChecks");
        
        // === Spatial Hash Markers (Epic 7.7.3) ===
        // Use these to profile O(N*k) spatial partitioning performance
        
        /// <summary>PlayerSpatialHashSystem.OnUpdate - Clearing and populating spatial hash grid</summary>
        public static readonly ProfilerMarker SpatialHash_PopulateGrid = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.SpatialHash.PopulateGrid");
        
        /// <summary>Spatial hash neighborhood queries (3x3 cell lookup)</summary>
        public static readonly ProfilerMarker SpatialHash_NeighborhoodQuery = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.SpatialHash.NeighborhoodQuery");
        
        /// <summary>O(N*k) spatial hash distance checks where k = avg players per neighborhood</summary>
        public static readonly ProfilerMarker ProximityCollision_SpatialHashChecks = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Proximity.SpatialHashChecks");
        
        /// <summary>Team/grace period filtering</summary>
        public static readonly ProfilerMarker ProximityCollision_Filtering = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Proximity.Filtering");
        
        /// <summary>Power ratio calculations</summary>
        public static readonly ProfilerMarker ProximityCollision_PowerCalculation = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Proximity.PowerCalculation");
        
        /// <summary>Applying collision effects (stagger, knockdown)</summary>
        public static readonly ProfilerMarker ProximityCollision_ApplyEffects = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Proximity.ApplyEffects");
        
        // === Job Scheduling Markers (Epic 7.7.5) ===
        // Use these to profile parallel job execution
        
        /// <summary>SpatialHashInsertJob - Parallel insertion into spatial hash grid</summary>
        public static readonly ProfilerMarker Job_SpatialHashInsert = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Job.SpatialHashInsert");
        
        /// <summary>CollisionBroadphaseJob - Parallel spatial hash queries for candidate pairs</summary>
        public static readonly ProfilerMarker Job_Broadphase = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Job.Broadphase");
        
        /// <summary>CollisionNarrowPhaseJob - Parallel distance validation for candidate pairs</summary>
        public static readonly ProfilerMarker Job_NarrowPhase = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Job.NarrowPhase");
        
        /// <summary>CollisionForceCalculationJob - Parallel force/power calculation for validated collisions</summary>
        public static readonly ProfilerMarker Job_ForceCalculation = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Job.ForceCalculation");
        
        /// <summary>Job scheduling overhead (creating and dispatching jobs)</summary>
        public static readonly ProfilerMarker Job_Scheduling = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Job.Scheduling");
        
        /// <summary>Job completion wait time (should be near zero if properly pipelined)</summary>
        public static readonly ProfilerMarker Job_WaitComplete = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Job.WaitComplete");
        
        // === Presentation System Markers ===
        // These run client-side only, in PresentationSystemGroup
        
        /// <summary>CollisionAudioSystem - Playing collision sounds</summary>
        public static readonly ProfilerMarker Audio = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Audio");
        
        /// <summary>CollisionVFXSystem - Spawning collision particles</summary>
        public static readonly ProfilerMarker VFX = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.VFX");
        
        /// <summary>LocalPlayerCollisionCameraShakeSystem - Camera shake triggers</summary>
        public static readonly ProfilerMarker CameraShake = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.CameraShake");
        
        /// <summary>LocalPlayerCollisionHapticsSystem - Controller rumble</summary>
        public static readonly ProfilerMarker Haptics = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.Haptics");
        
        // === Animation System Markers ===
        
        /// <summary>Stagger/Knockdown animation trigger systems</summary>
        public static readonly ProfilerMarker AnimationTriggers = 
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Collision.AnimationTriggers");
        
        // NOTE: ProfilerCounterValue<T> is incompatible with Burst-compiled ECS systems.
        // Use external profiling tools or non-Burst systems to track numeric metrics.
        // The ProfilerMarkers above provide timing data; for counts, check Unity Profiler's
        // built-in counters or add logging in DEVELOPMENT_BUILD.
    }
}
