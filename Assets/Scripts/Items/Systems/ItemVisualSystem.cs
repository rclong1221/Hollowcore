using Unity.Entities;
using Unity.NetCode;

namespace DIG.Items.Systems
{
    /// <summary>
    /// Client-side system that shows/hides item visuals based on equip state.
    /// Handles first-person vs third-person visual switching.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ItemVisualSystem : SystemBase
    {
        // Toggle debug logging for this system. Set to true to enable logs.
        private const bool DebugEnabled = false;
        
        protected override void OnUpdate()
        {
            // Update item visibility based on state
            foreach (var (item, perspective, entity) in 
                     SystemAPI.Query<RefRO<CharacterItem>, RefRW<PerspectiveItem>>()
                     .WithEntityAccess())
            {
                bool shouldBeVisible = item.ValueRO.State == ItemState.Equipped || 
                                       item.ValueRO.State == ItemState.Equipping;

                // Determine which perspective to show
                bool isLocalPlayer = IsLocalPlayerItem(item.ValueRO.OwnerEntity);
                perspective.ValueRW.IsFirstPerson = isLocalPlayer;

                // Enable/disable visual entities
                if (perspective.ValueRO.FirstPersonVisual != Entity.Null)
                {
                    SetEntityEnabled(perspective.ValueRO.FirstPersonVisual, shouldBeVisible && isLocalPlayer);
                }

                if (perspective.ValueRO.ThirdPersonVisual != Entity.Null)
                {
                    SetEntityEnabled(perspective.ValueRO.ThirdPersonVisual, shouldBeVisible && !isLocalPlayer);
                }
            }

            // Handle items without perspective component (simple show/hide)
            foreach (var (item, entity) in 
                     SystemAPI.Query<RefRO<CharacterItem>>()
                     .WithNone<PerspectiveItem>()
                     .WithEntityAccess())
            {
                bool shouldBeVisible = item.ValueRO.State == ItemState.Equipped || 
                                       item.ValueRO.State == ItemState.Equipping;
                
                // TODO: Set LinkedEntityGroup or child renderers enabled
            }
        }

        private bool IsLocalPlayerItem(Entity ownerEntity)
        {
            if (ownerEntity == Entity.Null)
                return false;

            // Check if owner has GhostOwnerIsLocal enabled
            if (EntityManager.HasComponent<GhostOwnerIsLocal>(ownerEntity))
            {
                return EntityManager.IsComponentEnabled<GhostOwnerIsLocal>(ownerEntity);
            }
            return false;
        }

        private void SetEntityEnabled(Entity entity, bool enabled)
        {
            if (entity == Entity.Null)
                return;

            // Use Disabled component or LinkedEntityGroup
            if (enabled)
            {
                if (EntityManager.HasComponent<Disabled>(entity))
                {
                    EntityManager.RemoveComponent<Disabled>(entity);
                }
            }
            else
            {
                if (!EntityManager.HasComponent<Disabled>(entity))
                {
                    EntityManager.AddComponent<Disabled>(entity);
                }
            }
        }
    }
}
