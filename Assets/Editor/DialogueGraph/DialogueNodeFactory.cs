#if UNITY_EDITOR
namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Factory for creating the correct DialogueNodeViewBase subclass
    /// based on DialogueNodeType.
    /// </summary>
    public static class DialogueNodeFactory
    {
        public static DialogueNodeViewBase CreateNodeView(DialogueNodeType nodeType, int nodeId)
        {
            return nodeType switch
            {
                DialogueNodeType.Speech => new SpeechNodeView(nodeId),
                DialogueNodeType.PlayerChoice => new ChoiceNodeView(nodeId),
                DialogueNodeType.Condition => new ConditionNodeView(nodeId),
                DialogueNodeType.Action => new ActionNodeView(nodeId),
                DialogueNodeType.Random => new RandomNodeView(nodeId),
                DialogueNodeType.End => new EndNodeView(nodeId),
                DialogueNodeType.Hub => new SpeechNodeView(nodeId, DialogueNodeType.Hub),
                _ => new SpeechNodeView(nodeId)
            };
        }
    }
}
#endif
