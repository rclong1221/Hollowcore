namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Data struct pushed to IDialogueUIProvider for rendering.
    /// Contains resolved (localized) text and filtered choices.
    /// </summary>
    public struct DialogueUIState
    {
        public string SpeakerName;
        public string BodyText;
        public string AudioClipPath;
        public DialogueChoiceUI[] Choices;
        public DialogueNodeType NodeType;
        public float AutoAdvanceSec;
        public DialogueCameraMode CameraMode;

        // EPIC 18.5 additions
        public string Expression;
        public UnityEngine.AudioClip VoiceClip;
        public float TypewriterSpeed;
        public DialoguePriority Priority;
    }

    /// <summary>
    /// A single choice option for the dialogue UI, pre-filtered by ValidChoicesMask.
    /// </summary>
    public struct DialogueChoiceUI
    {
        public int ChoiceIndex;
        public string Text;
    }
}
