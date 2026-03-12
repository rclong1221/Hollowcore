using Unity.Entities;

namespace Player.Components
{
    public struct ProneTuning : IComponentData
    {
        public float ClearanceMargin;
        public int SafeStandSteps;
        public int SafeStandRadialSamples;
        public float HeightInterpSpeed;
    }
}
