using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Environment; // CurrentEnvironmentZone

namespace Visuals.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class AtmosphereSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (AtmosphereManager.Instance == null) return;
            
            // Find Local Player Zone
            foreach (var zone in 
                     SystemAPI.Query<RefRO<CurrentEnvironmentZone>>()
                     .WithAll<GhostOwnerIsLocal>())
            {
                AtmosphereManager.Instance.SetAtmosphere(zone.ValueRO.ZoneType);
                // Only support one local player (usual case)
                return; 
            }
        }
    }
}
