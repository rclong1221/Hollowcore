using Unity.NetCode;

namespace DIG.SceneManagement.Network
{
    /// <summary>
    /// EPIC 18.6: Server → all clients: begin loading a target scene state.
    /// </summary>
    public struct SceneTransitionRequest : IRpcCommand
    {
        /// <summary>Hash of the target state ID (Animator.StringToHash for compact int).</summary>
        public int TargetStateHash;

        /// <summary>Monotonic ID correlating request/ack/complete for this transition.</summary>
        public uint TransitionId;
    }

    /// <summary>
    /// EPIC 18.6: Client → server: scene loading is complete for this transition.
    /// </summary>
    public struct SceneReadyAck : IRpcCommand
    {
        public uint TransitionId;
    }

    /// <summary>
    /// EPIC 18.6: Server → all clients: all clients have loaded, remove loading screen.
    /// </summary>
    public struct SceneTransitionComplete : IRpcCommand
    {
        public uint TransitionId;
    }
}
