using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Weapons.UI
{
    /// <summary>
    /// EPIC 14.20: System that updates WeaponHUD from ECS weapon state.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class WeaponHUDSystem : SystemBase
    {
        private WeaponHUD _hud;
        private CrosshairController _crosshair;

        protected override void OnUpdate()
        {
            // Get UI instances
            if (_hud == null)
                _hud = WeaponHUD.Instance;

            if (_crosshair == null)
                _crosshair = CrosshairController.Instance;

            if (_hud == null)
                return;

            // Find local player's equipped weapon
            bool foundWeapon = false;

            foreach (var (usableAction, reloadState, entity) in
                     SystemAPI.Query<RefRO<UsableAction>, RefRO<ReloadState>>()
                     .WithAll<GhostOwnerIsLocal>()
                     .WithEntityAccess())
            {
                foundWeapon = true;
                var action = usableAction.ValueRO;
                var reload = reloadState.ValueRO;

                // Update ammo display
                _hud.UpdateAmmo(action.AmmoCount, action.ClipSize, action.ReserveAmmo);

                // Update reload state
                if (reload.IsReloading)
                {
                    if (!reload.WasReloading)
                    {
                        _hud.StartReload(reload.ReloadDuration);
                    }
                    _hud.UpdateReloadProgress(reload.ReloadProgress);
                }
                else if (reload.WasReloading)
                {
                    _hud.EndReload();
                }

                // Update crosshair spread
                if (_crosshair != null)
                {
                    // Could get spread from SpreadComponent if available
                    // For now, use a default
                }

                break; // Only process first weapon
            }

            // Handle no weapon equipped
            if (!foundWeapon)
            {
                // Try without reload state
                foreach (var usableAction in
                         SystemAPI.Query<RefRO<UsableAction>>()
                         .WithAll<GhostOwnerIsLocal>())
                {
                    foundWeapon = true;
                    var action = usableAction.ValueRO;
                    _hud.UpdateAmmo(action.AmmoCount, action.ClipSize, action.ReserveAmmo);
                    break;
                }

                if (!foundWeapon)
                {
                    _hud.ClearDisplay();
                }
            }
        }
    }

    /// <summary>
    /// Reload state component for tracking reload progress.
    /// </summary>
    public struct ReloadState : IComponentData
    {
        public bool IsReloading;
        public bool WasReloading;
        public float ReloadProgress;
        public float ReloadDuration;
        public float ReloadTime;
    }
}
