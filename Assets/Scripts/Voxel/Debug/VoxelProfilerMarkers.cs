using Unity.Profiling;

namespace DIG.Voxel.Debug
{
    /// <summary>
    /// Centralized ProfilerMarker definitions for all voxel systems.
    ///
    /// Usage:
    ///   using (VoxelProfilerMarkers.ChunkStreaming.Auto())
    ///   {
    ///       // ... streaming code
    ///   }
    ///
    /// Or for subsections:
    ///   VoxelProfilerMarkers.ChunkStreaming_LoadChunks.Begin();
    ///   // ... load chunks
    ///   VoxelProfilerMarkers.ChunkStreaming_LoadChunks.End();
    /// </summary>
    public static class VoxelProfilerMarkers
    {
        // === STREAMING SYSTEM ===
        public static readonly ProfilerMarker ChunkStreaming =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkStreaming");
        public static readonly ProfilerMarker ChunkStreaming_UpdateViewer =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkStreaming.UpdateViewer");
        public static readonly ProfilerMarker ChunkStreaming_LoadChunks =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkStreaming.LoadChunks");
        public static readonly ProfilerMarker ChunkStreaming_UnloadChunks =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkStreaming.UnloadChunks");
        public static readonly ProfilerMarker ChunkStreaming_SpawnEntities =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkStreaming.SpawnEntities");

        // === VISIBILITY SYSTEM ===
        public static readonly ProfilerMarker ChunkVisibility =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkVisibility");
        public static readonly ProfilerMarker ChunkVisibility_BFS =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkVisibility.BFS");
        public static readonly ProfilerMarker ChunkVisibility_UpdateRenderers =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkVisibility.UpdateRenderers");

        // === PHYSICS COLLIDER SYSTEM ===
        public static readonly ProfilerMarker ChunkPhysics =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkPhysics");
        public static readonly ProfilerMarker ChunkPhysics_ScheduleJob =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkPhysics.ScheduleJob");
        public static readonly ProfilerMarker ChunkPhysics_Complete =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkPhysics.Complete");
        public static readonly ProfilerMarker ChunkPhysics_CreateCollider =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkPhysics.CreateCollider");

        // === DECORATOR SPAWN SYSTEM ===
        public static readonly ProfilerMarker DecoratorSpawn =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.DecoratorSpawn");
        public static readonly ProfilerMarker DecoratorSpawn_SurfaceDetection =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.DecoratorSpawn.SurfaceDetection");
        public static readonly ProfilerMarker DecoratorSpawn_Placement =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.DecoratorSpawn.Placement");
        public static readonly ProfilerMarker DecoratorSpawn_Instantiate =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.DecoratorSpawn.Instantiate");

        // === FLUID SYSTEMS ===
        public static readonly ProfilerMarker FluidSimulation =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.FluidSimulation");
        public static readonly ProfilerMarker FluidSimulation_Iterate =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.FluidSimulation.Iterate");
        public static readonly ProfilerMarker FluidSimulation_ScheduleJobs =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.FluidSimulation.ScheduleJobs");
        public static readonly ProfilerMarker FluidMesh =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.FluidMesh");
        public static readonly ProfilerMarker FluidDamage =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.FluidDamage");
        public static readonly ProfilerMarker FluidZoneUpdate =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.FluidZoneUpdate");

        // === INTERACTION SYSTEMS ===
        public static readonly ProfilerMarker VoxelInteraction =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.Interaction");
        public static readonly ProfilerMarker VoxelInteraction_Raycast =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.Interaction.Raycast");
        public static readonly ProfilerMarker VoxelInteraction_Mine =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.Interaction.Mine");
        public static readonly ProfilerMarker VoxelExplosion =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.Explosion");
        public static readonly ProfilerMarker VoxelExplosion_Crater =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.Explosion.Crater");
        public static readonly ProfilerMarker VoxelExplosion_SpawnLoot =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.Explosion.SpawnLoot");
        public static readonly ProfilerMarker VoxelLoot =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.Loot");
        public static readonly ProfilerMarker VoxelHazard =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.Hazard");

        // === INFRASTRUCTURE SYSTEMS ===
        public static readonly ProfilerMarker ChunkLookup =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkLookup");
        public static readonly ProfilerMarker ChunkLookup_RebuildHashMap =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.ChunkLookup.RebuildHashMap");
        public static readonly ProfilerMarker ChunkMemoryCleanup =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.MemoryCleanup");
        public static readonly ProfilerMarker FrameBudget =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.FrameBudget");
        public static readonly ProfilerMarker CameraData =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.CameraData");

        // === NETWORK SYSTEMS ===
        public static readonly ProfilerMarker VoxelModification =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.Modification");
        public static readonly ProfilerMarker VoxelBatching =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.Batching");
        public static readonly ProfilerMarker LootSpawn =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.LootSpawn");
        public static readonly ProfilerMarker LateJoinSync =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.LateJoinSync");

        // === DECORATOR SYSTEMS ===
        public static readonly ProfilerMarker DecoratorCleanup =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.DecoratorCleanup");
        public static readonly ProfilerMarker DecoratorInstancing =
            new ProfilerMarker(ProfilerCategory.Scripts, "DIG.Voxel.DecoratorInstancing");
    }
}
