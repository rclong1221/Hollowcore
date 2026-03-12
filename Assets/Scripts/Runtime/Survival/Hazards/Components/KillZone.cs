using Unity.Entities;

namespace DIG.Survival.Hazards
{
    public struct KillZone : IComponentData
    {
        public float DamagePerSecond;
    }
}
