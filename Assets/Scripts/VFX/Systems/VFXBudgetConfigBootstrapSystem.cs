using Unity.Entities;

namespace DIG.VFX.Systems
{
    /// <summary>
    /// EPIC 16.7: Creates VFXBudgetConfig, VFXLODConfig, and VFXDynamicBudget singletons
    /// with defaults if none exist from authoring. Runs once.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct VFXBudgetConfigBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            if (!SystemAPI.HasSingleton<VFXBudgetConfig>())
            {
                var e = ecb.CreateEntity();
                ecb.AddComponent(e, VFXBudgetConfig.Default);
            }

            if (!SystemAPI.HasSingleton<VFXLODConfig>())
            {
                var e = ecb.CreateEntity();
                ecb.AddComponent(e, VFXLODConfig.Default);
            }

            if (!SystemAPI.HasSingleton<VFXDynamicBudget>())
            {
                var e = ecb.CreateEntity();
                ecb.AddComponent(e, VFXDynamicBudget.Default);
            }

            state.Enabled = false;
        }
    }
}
