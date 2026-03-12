using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.Resources.UI
{
    /// <summary>
    /// EPIC 16.8 Phase 5: Managed bridge from ECS ResourcePool to UI.
    /// Reads ResourcePool from the local player entity, pushes to ResourceUIRegistry.
    /// Follows the CombatUIBridgeSystem pattern.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class ResourceUIBridgeSystem : SystemBase
    {
        private EntityQuery _localPlayerQuery;

        protected override void OnCreate()
        {
            _localPlayerQuery = GetEntityQuery(
                ComponentType.ReadOnly<ResourcePool>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
        }

        protected override void OnUpdate()
        {
            if (_localPlayerQuery.IsEmpty) return;
            if (ResourceUIRegistry.Instance == null) return;

            var entities = _localPlayerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                if (!EntityManager.IsComponentEnabled<GhostOwnerIsLocal>(entities[i])) continue;

                var pool = EntityManager.GetComponentData<ResourcePool>(entities[i]);
                ResourceUIRegistry.Instance.UpdateBars(pool);
                break;
            }
            entities.Dispose();
        }
    }
}
