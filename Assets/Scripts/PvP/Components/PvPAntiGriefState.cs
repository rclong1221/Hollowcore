using Unity.Entities;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Tracks AFK detection and leaver penalty state per player.
    /// 8 bytes.
    /// </summary>
    public struct PvPAntiGriefState : IComponentData
    {
        public float TimeSinceLastInput;
        public byte AFKWarningCount;
        public byte LeaverPenaltyCount;
        public byte IsAFK;
        public byte Padding;
    }
}
