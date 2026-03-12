using Unity.Mathematics;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Interface for dialogue panel UI display.
    /// Implemented by MonoBehaviour adapters that drive the actual UI elements.
    /// </summary>
    public interface IDialogueUIProvider
    {
        void OpenDialogue(DialogueUIState state);
        void AdvanceDialogue(DialogueUIState state);
        void CloseDialogue();
        void ShowActionFeedback(DialogueActionFeedback feedback);
    }

    /// <summary>
    /// EPIC 16.16: Interface for world-space bark text bubble display.
    /// </summary>
    public interface IBarkUIProvider
    {
        void ShowBark(string text, float3 worldPosition, float range);
        void HideBark();
    }

    /// <summary>
    /// Feedback data for failed dialogue actions.
    /// </summary>
    public struct DialogueActionFeedback
    {
        public DialogueActionType ActionType;
        public bool Success;
        public string Message;
    }
}
