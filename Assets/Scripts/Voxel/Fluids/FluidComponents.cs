using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Voxel.Fluids
{
    /// <summary>
    /// ECS component for a fluid cell in the voxel grid.
    /// Each voxel can contain fluid that flows and spreads.
    /// </summary>
    public struct FluidCell : IComponentData
    {
        /// <summary>Fluid type (0 = none)</summary>
        public byte Type;
        
        /// <summary>Fill level (0-255, where 255 = full)</summary>
        public byte Level;
        
        /// <summary>Pressure level (for pressurized fluids)</summary>
        public byte Pressure;
        
        /// <summary>Temperature (for lava cooling)</summary>
        public half Temperature;
        
        public bool IsEmpty => Type == 0 || Level == 0;
        public bool IsFull => Level >= 255;
        
        public static FluidCell Empty => new FluidCell { Type = 0, Level = 0, Pressure = 0, Temperature = (half)20f };
        
        public static FluidCell Water(byte level = 255) => new FluidCell 
        { 
            Type = (byte)FluidType.Water, 
            Level = level, 
            Pressure = 0, 
            Temperature = (half)15f 
        };
        
        public static FluidCell Lava(byte level = 255) => new FluidCell 
        { 
            Type = (byte)FluidType.Lava, 
            Level = level, 
            Pressure = 0, 
            Temperature = (half)1200f 
        };
    }
    
    /// <summary>
    /// Component for entities (players, NPCs) submerged in fluid.
    /// Added when an entity enters a fluid zone.
    /// </summary>
    public struct InFluidZone : IComponentData
    {
        /// <summary>Type of fluid the entity is in</summary>
        public byte FluidType;
        
        /// <summary>How deep the entity is submerged (meters)</summary>
        public float SubmersionDepth;
        
        /// <summary>How long entity has been in this fluid</summary>
        public float TimeInFluid;
        
        /// <summary>World position of fluid surface</summary>
        public float3 FluidSurfacePosition;
        
        /// <summary>Temperature of the fluid</summary>
        public float FluidTemperature;
    }
    
    /// <summary>
    /// Tag component for entities that can enter fluids.
    /// </summary>
    public struct FluidInteractor : IComponentData
    {
        /// <summary>Entity's height for submersion calculation</summary>
        public float Height;
        
        /// <summary>Does entity float in fluids</summary>
        public byte CanFloat;  // 0 = no, 1 = yes
        
        /// <summary>Entity's buoyancy factor</summary>
        public float Buoyancy;
    }
    
    /// <summary>
    /// Fluid eruption event - spawned when pressurized fluid is released.
    /// </summary>
    public struct FluidEruptionEvent : IComponentData
    {
        public float3 Position;
        public byte FluidType;
        public float PressureLevel;
        public float Radius;
        public float Duration;
        public float TimeRemaining;
    }
    
    /// <summary>
    /// Component for chunks containing fluid.
    /// </summary>
    public struct ChunkFluidData : IComponentData
    {
        /// <summary>Does this chunk have any fluid</summary>
        public byte HasFluid;  // 0 = no, 1 = yes
        
        /// <summary>Primary fluid type in this chunk</summary>
        public byte PrimaryFluidType;
        
        /// <summary>Highest fluid level Y in chunk</summary>
        public float MaxFluidY;
        
        /// <summary>Lowest fluid level Y in chunk</summary>
        public float MinFluidY;
    }
    
    /// <summary>
    /// Singleton component for fluid simulation settings.
    /// </summary>
    public struct FluidSimulationSettings : IComponentData
    {
        /// <summary>Is fluid simulation enabled</summary>
        public byte Enabled;
        
        /// <summary>Simulation ticks per second</summary>
        public float TickRate;
        
        /// <summary>Maximum fluid updates per frame</summary>
        public int MaxUpdatesPerFrame;
        
        /// <summary>Radius around player to simulate fluids</summary>
        public float SimulationRadius;
    }

    /// <summary>
    /// Reference to the child entity that holds the fluid mesh.
    /// </summary>
    public struct FluidMeshReference : IComponentData
    {
        public Entity MeshEntity;
    }

    /// <summary>
    /// Buffer element for storing fluid cells in a chunk.
    /// </summary>
    [InternalBufferCapacity(0)] // Store outside chunk to save chunk space (32k items)
    public struct FluidBufferElement : IBufferElementData
    {
        public FluidCell Value;
        
        public static implicit operator FluidCell(FluidBufferElement e) => e.Value;
        public static implicit operator FluidBufferElement(FluidCell e) => new FluidBufferElement { Value = e };
    }
}
