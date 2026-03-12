using Unity.Entities;
using Unity.NetCode;

namespace DIG.Core.Input
{
    /// <summary>
    /// ECS component that holds the current input paradigm state.
    /// Synced from ParadigmStateMachine to local player entity.
    /// ECS systems query this instead of calling MonoBehaviour singletons.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct InputParadigmState : IComponentData
    {
        /// <summary>Active input paradigm for this player.</summary>
        [GhostField] public InputParadigm ActiveParadigm;

        /// <summary>Current facing mode.</summary>
        [GhostField] public MovementFacingMode FacingMode;

        /// <summary>Whether click-to-move is enabled.</summary>
        [GhostField] public bool IsClickToMoveEnabled;

        /// <summary>Which button triggers click-to-move.</summary>
        [GhostField] public ClickToMoveButton ClickToMoveButton;

        /// <summary>Active mode overlay (Vehicle, Build, or None).</summary>
        [GhostField] public InputModeOverlay ActiveModeOverlay;

        /// <summary>Whether WASD direct movement is enabled.</summary>
        [GhostField] public bool IsWASDEnabled;

        /// <summary>
        /// Whether A/D keys turn the character (MMO style) vs strafe (Shooter style).
        /// When true, A/D = turn by default, strafe when RMB held (MMO paradigm).
        /// When false, A/D = always strafe (Shooter paradigm).
        /// </summary>
        [GhostField] public bool ADTurnsCharacter;

        /// <summary>
        /// Whether camera orbit is currently active.
        /// For RMB-hold orbit modes, this is true when RMB is held.
        /// For always-orbit modes, this is always true.
        /// </summary>
        [GhostField] public bool IsOrbitActive;

        /// <summary>Current camera orbit mode.</summary>
        [GhostField] public CameraOrbitMode CameraOrbitMode;
        
        /// <summary>
        /// Whether movement is screen-relative (isometric) or camera-relative (TPS/FPS).
        /// Screen-relative: W=up on screen, A=left on screen (fixed world directions)
        /// Camera-relative: W=camera forward, A=camera left (rotates with camera)
        /// </summary>
        [GhostField] public bool UseScreenRelativeMovement;
    }
}
