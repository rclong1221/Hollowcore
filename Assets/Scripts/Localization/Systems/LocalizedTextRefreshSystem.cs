using Unity.Entities;

namespace DIG.Localization
{
    /// <summary>
    /// When LocaleChangedTag entities exist, dispatches locale change
    /// to all ILocalizableUI providers via LocalizationUIRegistry,
    /// then destroys the tag entities.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LocalizedTextRefreshSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<LocaleChangedTag>();
        }

        protected override void OnUpdate()
        {
            LocalizationUIRegistry.NotifyLocaleChanged();

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<LocaleChangedTag>>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
