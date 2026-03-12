using Unity.Entities;
using Unity.NetCode;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Capture point zone entity. PvPObjectiveSystem reads overlapping
    /// player collisions to determine control.
    /// 20 bytes.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct PvPCaptureZone : IComponentData
    {
        [GhostField] public byte ZoneId;
        [GhostField] public byte ControllingTeam;
        [GhostField] public byte ContestingTeam;
        [GhostField] public byte PlayersInZone;
        [GhostField(Quantization = 100)] public float CaptureProgress;
        [GhostField(Quantization = 10)] public float PointsPerSecond;
        [GhostField(Quantization = 10)] public float Radius;
        public int Padding;
    }
}
