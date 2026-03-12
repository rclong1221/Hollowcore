using Unity.Entities;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Runtime configuration singleton for the dialogue system.
    /// Created from DialogueConfigSO by DialogueBootstrapSystem.
    /// </summary>
    public struct DialogueConfig : IComponentData
    {
        public uint MaxSessionDurationTicks;
        public byte MaxFlagsPerNpc;
        public bool AutoAdvanceEnabled;
        public float BarkProximityRange;
        public float BarkCheckInterval;
        public int BarkCheckFrameSpread;

        // EPIC 18.5 additions
        public float TypewriterCharsPerSecond;
        public float PausePeriod;
        public float PauseComma;
        public float PauseExclamation;
        public int HistoryCapacity;
    }
}
