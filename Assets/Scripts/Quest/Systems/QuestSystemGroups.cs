using Unity.Entities;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: System group for quest evaluation logic.
    /// Runs after all emitters have created QuestEvent entities.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class QuestEvaluationSystemGroup : ComponentSystemGroup { }
}
