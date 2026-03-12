namespace DIG.Tutorial.Config
{
    /// <summary>
    /// EPIC 18.4: How a tutorial step determines completion.
    /// </summary>
    public enum CompletionCondition : byte
    {
        ManualContinue = 0,
        InputPerformed = 1,
        UIScreenOpened = 2,
        CustomEvent = 3,
        Timer = 4,
    }
}
