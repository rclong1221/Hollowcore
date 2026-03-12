using Unity.Entities;
using DIG.Targeting;

namespace DIG.Core.Input
{
    /// <summary>
    /// Non-Burst system that syncs ParadigmStateMachine settings to the ParadigmSettings singleton.
    /// This runs early in the frame so movement/facing systems can read the settings from ECS.
    /// 
    /// Runs in InitializationSystemGroup so it works even without a network player spawned.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ParadigmSettingsSyncSystem : SystemBase
    {
        private Entity _singletonEntity;

        protected override void OnCreate()
        {
            // Create the singleton entity with ParadigmSettings
            _singletonEntity = EntityManager.CreateEntity(typeof(ParadigmSettings));
            EntityManager.SetName(_singletonEntity, "ParadigmSettings");
            
            // Initialize with defaults - valid even without ParadigmStateMachine
            // Server world won't have ParadigmStateMachine, so use sensible defaults
            EntityManager.SetComponentData(_singletonEntity, new ParadigmSettings
            {
                ActiveParadigm = InputParadigm.MMO,
                FacingMode = MovementFacingMode.CameraForward,
                UseScreenRelativeMovement = false,
                IsWASDEnabled = true,
                ADTurnsCharacter = true, // MMO default: A/D turns character
                CursorVisible = false,
                IsIsometric = false,
                ActiveTargetingMode = TargetingMode.CameraRaycast,
                IsValid = true // Valid even as defaults for server-side
            });
        }

        protected override void OnUpdate()
        {
            // Sync from managed ParadigmStateMachine to ECS singleton
            // On server, ParadigmStateMachine.Instance is null - keep default settings
            var machine = ParadigmStateMachine.Instance;
            if (machine == null || machine.ActiveProfile == null)
            {
                // Keep current settings (including defaults) valid
                // Server doesn't have ParadigmStateMachine but needs valid settings
                return;
            }

            var profile = machine.ActiveProfile;
            
            // Derive isIsometric from paradigm type
            bool isIsometric = profile.paradigm == InputParadigm.ARPG 
                || profile.paradigm == InputParadigm.MOBA 
                || profile.paradigm == InputParadigm.TwinStick;
            
            // Preserve CameraYaw - it's set separately by CinemachineCameraController
            var currentSettings = EntityManager.GetComponentData<ParadigmSettings>(_singletonEntity);
            
            var newSettings = new ParadigmSettings
            {
                ActiveParadigm = profile.paradigm,
                FacingMode = profile.facingMode,
                UseScreenRelativeMovement = profile.useScreenRelativeMovement,
                IsWASDEnabled = profile.wasdEnabled,
                ADTurnsCharacter = profile.adTurnsCharacter,
                CursorVisible = profile.cursorFreeByDefault,
                IsIsometric = isIsometric,
                IsClickToMoveEnabled = profile.clickToMoveEnabled,
                ClickToMoveButton = profile.clickToMoveButton,
                UsePathfinding = profile.usePathfinding,
                ActiveTargetingMode = profile.defaultTargetingMode,
                CameraYaw = currentSettings.CameraYaw, // Preserve camera yaw from CinemachineCameraController
                IsValid = true
            };

            EntityManager.SetComponentData(_singletonEntity, newSettings);
        }

        protected override void OnDestroy()
        {
            if (EntityManager.Exists(_singletonEntity))
            {
                EntityManager.DestroyEntity(_singletonEntity);
            }
        }
    }
}
