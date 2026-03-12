using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Combat.Components;
using DIG.AI.Components;

namespace DIG.Combat.Bridges
{
    /// <summary>
    /// EPIC 15.32: Renders telegraph ground decals.
    /// Managed system in PresentationSystemGroup (client-side).
    /// Reads TelegraphZone entities from ServerWorld and renders ground indicators.
    ///
    /// Stub — requires decal pooling and VFX infrastructure.
    /// System is disabled (Enabled=false) until implementation.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class TelegraphVisualBridge : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<TelegraphZone>();
            Enabled = false;
        }

        protected override void OnUpdate() { }
    }
}
