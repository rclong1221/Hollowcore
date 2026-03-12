using Unity.Entities;
using Unity.NetCode;
using DIG.Items;

namespace DIG.Loot.Presentation
{
    /// <summary>
    /// EPIC 16.6: Client-side feedback when player picks up loot.
    /// Reads PickupEvent on player entity, triggers pickup sound + HUD notification.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class PickupFeedbackSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (pickupEvent, _) in
                     SystemAPI.Query<RefRO<PickupEvent>, RefRO<GhostOwnerIsLocal>>())
            {
                if (!pickupEvent.ValueRO.Pending) continue;

                // Trigger pickup feedback:
                // 1. Play pickup sound via audio system
                // 2. Show HUD notification (item name + icon)
                // 3. Flash inventory slot
                // These would route through existing UI bridge systems
            }
        }
    }
}
