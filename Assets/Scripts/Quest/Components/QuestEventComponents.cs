using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Generic quest event — the key decoupling point.
    /// Created by emitter systems (one per game event type), consumed by QuestObjectiveEvaluationSystem.
    /// Transient: created and destroyed in the same frame.
    /// Existing game systems do NOT know about quests — emitters translate their events into these.
    /// </summary>
    public struct QuestEvent : IComponentData
    {
        /// <summary>Maps to ObjectiveType for matching against objectives.</summary>
        public ObjectiveType EventType;
        /// <summary>Context-dependent: prefab hash (Kill), interactable ID (Interact), item type ID (Collect), zone ID (ReachZone), recipe ID (Craft).</summary>
        public int TargetId;
        /// <summary>How many (usually 1).</summary>
        public int Count;
        /// <summary>Which player triggered the event.</summary>
        public Entity SourcePlayer;
        /// <summary>World position where the event occurred.</summary>
        public float3 Position;
    }

    /// <summary>
    /// EPIC 16.12: Tag for cleanup system to find and destroy QuestEvent entities.
    /// </summary>
    public struct QuestEventTag : IComponentData { }
}
