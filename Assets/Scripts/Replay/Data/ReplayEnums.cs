namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: Current state of the replay system.
    /// </summary>
    public enum ReplayState : byte
    {
        Idle = 0,
        Recording = 1,
        Playing = 2,
        Paused = 3,
        Seeking = 4
    }

    /// <summary>
    /// EPIC 18.10: Spectator camera modes.
    /// </summary>
    public enum SpectatorCameraMode : byte
    {
        FreeCam = 0,
        FollowPlayer = 1,
        FirstPerson = 2,
        Orbit = 3,
        KillCam = 4
    }

    /// <summary>
    /// EPIC 18.10: Types of events recorded in replay files.
    /// </summary>
    public enum ReplayEventType : byte
    {
        None = 0,
        Kill = 1,
        Death = 2,
        Ability = 3,
        Objective = 4,
        ChatMessage = 5,
        Bookmark = 6
    }

    /// <summary>
    /// EPIC 18.10: Replay frame types for delta encoding.
    /// </summary>
    public enum ReplayFrameType : byte
    {
        Keyframe = 0,
        DeltaFrame = 1
    }
}
