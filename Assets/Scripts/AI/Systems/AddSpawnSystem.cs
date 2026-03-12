using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using DIG.AI.Components;

namespace DIG.AI.Systems
{
    /// <summary>
    /// EPIC 15.32: Handles spawning add groups for boss encounters.
    /// Reads SpawnGroupDefinition buffer and instantiates adds when triggered
    /// by phase transitions or encounter triggers.
    ///
    /// Stub — requires ghost prefab instantiation via NetCode spawning infrastructure.
    /// System is disabled (Enabled=false) until implementation.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PhaseTransitionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class AddSpawnSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<EncounterState>();
            Enabled = false;
        }

        protected override void OnUpdate() { }
    }
}
