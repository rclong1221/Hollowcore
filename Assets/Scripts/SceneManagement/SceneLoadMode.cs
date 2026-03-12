namespace DIG.SceneManagement
{
    /// <summary>
    /// EPIC 18.6: How a scene definition is loaded at runtime.
    /// </summary>
    public enum SceneLoadMode : byte
    {
        Single = 0,   // UnityEngine.SceneManagement.SceneManager (replaces active scene)
        Additive = 1, // SceneManager with LoadSceneMode.Additive
        SubScene = 2  // Unity.Scenes.SceneSystem (ECS SubScene by GUID)
    }

    /// <summary>
    /// Visual transition animation between scenes.
    /// </summary>
    public enum TransitionAnimation : byte
    {
        None = 0,
        Fade = 1,
        Dissolve = 2,
        SlideLeft = 3,
        SlideRight = 4
    }

    /// <summary>
    /// What triggers a GameFlowTransition.
    /// </summary>
    public enum TransitionCondition : byte
    {
        Event = 0,      // Fired explicitly via SceneService.FireEvent()
        Immediate = 1,  // Transitions as soon as source state is entered
        SceneLoaded = 2 // After target scene finishes loading
    }

    /// <summary>
    /// Loading screen progress bar visual style.
    /// </summary>
    public enum ProgressBarStyle : byte
    {
        Continuous = 0,
        Stepped = 1,
        Indeterminate = 2
    }
}
