using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Roguelite.Zones
{
    public enum ZoneSelectionMode : byte
    {
        Fixed = 0,
        WeightedRandom = 1,
        PlayerChoice = 2,
        Conditional = 3
    }

    /// <summary>
    /// Ordered sequence of zone layers for a run. Each layer = one zone slot.
    /// Supports looping for endless runs (Risk of Rain 2 style).
    /// </summary>
    [CreateAssetMenu(fileName = "ZoneSequence", menuName = "DIG/Roguelite/Zone Sequence", order = 11)]
    public class ZoneSequenceSO : ScriptableObject
    {
        public string SequenceName;

        [Tooltip("Ordered layers. Each layer = one zone slot in the run.")]
        public List<ZoneSequenceLayer> Layers = new();

        [Tooltip("If true, after the last layer the sequence loops back to LoopStartIndex.")]
        public bool EnableLooping;

        [Tooltip("Layer index to loop back to (0-based). Only used if EnableLooping is true.")]
        public int LoopStartIndex;

        [Tooltip("Difficulty multiplier applied per loop iteration.")]
        public float LoopDifficultyMultiplier = 1.5f;
    }

    [Serializable]
    public class ZoneSequenceLayer
    {
        public string LayerName;
        public ZoneSelectionMode Mode;

        [Tooltip("For PlayerChoice mode: how many options to present.")]
        public int ChoiceCount = 2;

        public List<ZoneSequenceEntry> Entries = new();
    }

    [Serializable]
    public struct ZoneSequenceEntry
    {
        public ZoneDefinitionSO Zone;
        public float Weight;

        [Tooltip("For Conditional mode: minimum ascension level required. 0 = always available.")]
        public byte MinAscensionLevel;

        [Tooltip("For Conditional mode: minimum loop count required. 0 = first pass.")]
        public byte MinLoopCount;
    }
}
