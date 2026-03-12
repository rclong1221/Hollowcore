using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Handles tool switching based on player input.
    /// Updates ActiveTool component and resets previous tool's usage state.
    /// </summary>
    /// <remarks>
    /// TODO: This system needs to be integrated with the EPIC14.5 slot-based equip system.
    /// Previously used ToolSlotDelta (scroll wheel) which has been removed.
    /// Tools should be assigned to equipment slots and switched via DIGEquipmentProvider.
    /// 
    /// For now, this system is disabled until tool switching is redesigned.
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ToolSwitchingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            // Disable this system until tool switching is redesigned for EPIC14.5
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // System is disabled - see OnCreate
            // TODO: Integrate with EPIC14.5 slot-based equip system
        }
    }
}
