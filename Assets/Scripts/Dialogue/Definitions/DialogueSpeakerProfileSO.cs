using System;
using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 18.5: Speaker profile for dialogue UI — portraits, colors, voice bank.
    /// One asset per named speaker in the game.
    /// </summary>
    [CreateAssetMenu(fileName = "SpeakerProfile", menuName = "DIG/Dialogue/Speaker Profile", order = 5)]
    public class DialogueSpeakerProfileSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown in dialogue UI.")]
        public string SpeakerName;

        [Tooltip("Hash of SpeakerName — matched against DialogueNode.SpeakerName hash at runtime.")]
        [HideInInspector] public int SpeakerNameHash;

        [Header("Portraits")]
        [Tooltip("Expression-to-sprite mapping for this speaker.")]
        public SpeakerPortrait[] Portraits = Array.Empty<SpeakerPortrait>();

        [Tooltip("Fallback portrait when expression key has no match.")]
        public Sprite DefaultPortrait;

        [Header("Text Style")]
        [Tooltip("Text color for this speaker's dialogue lines.")]
        public Color TextColor = Color.white;

        [Tooltip("Accent color for the speaker name plate.")]
        public Color NamePlateColor = new Color(0.3f, 0.5f, 0.9f, 1f);

        [Header("Voice")]
        [Tooltip("Random mumble clips played per character during typewriter reveal (optional).")]
        public AudioClip[] VoiceBank = Array.Empty<AudioClip>();

        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(SpeakerName))
                SpeakerNameHash = SpeakerName.GetHashCode();
        }

        /// <summary>
        /// Returns the portrait sprite for the given expression key, or DefaultPortrait if not found.
        /// </summary>
        public Sprite GetPortrait(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return DefaultPortrait;

            for (int i = 0; i < Portraits.Length; i++)
            {
                if (string.Equals(Portraits[i].Expression, expression, StringComparison.OrdinalIgnoreCase))
                    return Portraits[i].Sprite;
            }
            return DefaultPortrait;
        }
    }

    /// <summary>
    /// EPIC 18.5: Maps an expression key to a portrait sprite.
    /// </summary>
    [Serializable]
    public class SpeakerPortrait
    {
        [Tooltip("Expression key (e.g., neutral, happy, angry, sad, surprised).")]
        public string Expression;
        public Sprite Sprite;
    }
}
