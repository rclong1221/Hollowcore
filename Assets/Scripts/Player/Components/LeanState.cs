using Unity.Entities;
using Unity.Mathematics;

namespace Player.Components
{
    // Runtime lean state for players. Systems update this component and camera/animation
    // systems may read it to offset the camera or animate a peek.
    public struct LeanState : IComponentData
    {
        // Current lean value in range [-1, 1]. Negative = left, positive = right.
        public float CurrentLean;

        // Target lean requested by input.
        public float TargetLean;

        // Speed (units/sec) at which CurrentLean moves towards TargetLean.
        public float LeanSpeed;
    }
}
