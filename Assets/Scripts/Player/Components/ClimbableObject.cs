using Unity.Entities;
using Unity.Mathematics;

namespace Player.Components
{
    public enum ClimbableType
    {
        Ladder = 0,
        Pipe = 1,
        RockWall = 2
    }

    // Marks an entity as climbable with top/bottom positions and climb speed.
    public struct ClimbableObject : IComponentData
    {
        public ClimbableType Type;
        public float3 BottomPosition;
        public float3 TopPosition;
        public float ClimbSpeed; // meters per second when climbing
        public float InteractionRadius; // how close player needs to be to mount
    }
}
