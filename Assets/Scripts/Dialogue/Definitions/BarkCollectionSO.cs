using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: A pool of ambient bark lines for NPC chatter.
    /// Assigned to NPCs via DialogueSpeakerAuthoring.BarkCollection.
    /// </summary>
    [CreateAssetMenu(fileName = "BarkCollection", menuName = "DIG/Dialogue/Bark Collection", order = 2)]
    public class BarkCollectionSO : ScriptableObject
    {
        [Tooltip("Unique identifier.")]
        public int BarkId;

        public BarkCategory Category;

        [Tooltip("Pool of random lines. Selection is weighted.")]
        public BarkLine[] Lines = new BarkLine[0];

        [Tooltip("Minimum seconds between barks from this collection.")]
        [Min(1f)] public float Cooldown = 30f;

        [Tooltip("Audio/text display range in meters.")]
        [Min(1f)] public float MaxRange = 10f;

        [Tooltip("Only bark when player has line of sight to NPC.")]
        public bool RequiresLineOfSight;
    }
}
