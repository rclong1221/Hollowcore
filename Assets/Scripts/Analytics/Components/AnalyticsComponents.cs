using Unity.Collections;
using Unity.Entities;

namespace DIG.Analytics
{
    public struct AnalyticsConfig : IComponentData
    {
        public uint EnabledCategories;
        public float SampleRate;
        public float FlushIntervalSec;
    }

    public struct SessionState : IComponentData
    {
        public FixedString64Bytes SessionId;
        public uint StartTick;
        public int PlayerCount;
    }
}
