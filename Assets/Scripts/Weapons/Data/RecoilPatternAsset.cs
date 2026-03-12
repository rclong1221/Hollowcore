using UnityEngine;
using System.Collections.Generic;

namespace DIG.Weapons.Data
{
    /// <summary>
    /// EPIC 15.5: Recoil Pattern ScriptableObject.
    /// Defines per-shot recoil offsets that create predictable, learnable spray patterns.
    /// Separates gameplay recoil (aim offset) from visual recoil (camera kick via FEEL).
    /// </summary>
    [CreateAssetMenu(fileName = "RecoilPattern_New", menuName = "DIG/Weapons/Recoil Pattern")]
    public class RecoilPatternAsset : ScriptableObject
    {
        [Header("Pattern Data")]
        [Tooltip("Sequence of aim offsets per shot (x = horizontal, y = vertical in degrees)")]
        [SerializeField] private Vector2[] pattern = new Vector2[]
        {
            new Vector2(0f, 2f),    // Shot 1: Up
            new Vector2(0.2f, 2f),  // Shot 2: Up-right
            new Vector2(-0.3f, 1.8f), // Shot 3: Up-left
            new Vector2(0.1f, 1.5f),  // Shot 4: Up
            new Vector2(0.4f, 1.2f),  // Shot 5: Right
        };

        [Header("Pattern Behavior")]
        [Tooltip("How to handle shots beyond the pattern length")]
        [SerializeField] private PatternOverflowMode overflowMode = PatternOverflowMode.RepeatLast;

        [Tooltip("Randomness added to each offset (degrees)")]
        [SerializeField] private float randomSpread = 0.3f;

        [Tooltip("Overall pattern scale multiplier")]
        [SerializeField] private float patternScale = 1f;

        [Header("Recovery")]
        [Tooltip("How fast the pattern resets when not firing (seconds to reset one step)")]
        [SerializeField] private float recoveryTimePerStep = 0.15f;

        [Tooltip("Delay before recovery starts after last shot (seconds)")]
        [SerializeField] private float recoveryDelay = 0.1f;

        [Header("First Shot Accuracy")]
        [Tooltip("First shot has no recoil offset (true for skill-based weapons)")]
        [SerializeField] private bool firstShotAccurate = true;

        [Header("Visual Recoil (FEEL)")]
        [Tooltip("Camera kick strength per shot (purely visual)")]
        [SerializeField] private float visualKickStrength = 1f;

        [Tooltip("Camera kick recovery speed")]
        [SerializeField] private float visualKickRecovery = 10f;

        [Tooltip("FOV punch amount (degrees)")]
        [SerializeField] private float fovPunch = 2f;

        /// <summary>
        /// Get the recoil offset for a specific shot index.
        /// </summary>
        public Vector2 GetRecoilOffset(int shotIndex, float randomSeed = 0f)
        {
            if (pattern == null || pattern.Length == 0)
            {
                return Vector2.zero;
            }

            // First shot accuracy
            if (firstShotAccurate && shotIndex == 0)
            {
                return Vector2.zero;
            }

            // Adjust for first shot accuracy
            int patternIndex = firstShotAccurate ? shotIndex - 1 : shotIndex;

            // Handle overflow
            Vector2 baseOffset;
            if (patternIndex < pattern.Length)
            {
                baseOffset = pattern[patternIndex];
            }
            else
            {
                switch (overflowMode)
                {
                    case PatternOverflowMode.RepeatLast:
                        baseOffset = pattern[pattern.Length - 1];
                        break;

                    case PatternOverflowMode.Loop:
                        baseOffset = pattern[patternIndex % pattern.Length];
                        break;

                    case PatternOverflowMode.PingPong:
                        int cycleLength = pattern.Length * 2 - 2;
                        int cyclePos = patternIndex % cycleLength;
                        if (cyclePos < pattern.Length)
                        {
                            baseOffset = pattern[cyclePos];
                        }
                        else
                        {
                            baseOffset = pattern[cycleLength - cyclePos];
                        }
                        break;

                    case PatternOverflowMode.Random:
                        // Use deterministic random based on shot index
                        var rand = new System.Random((int)(shotIndex * 1000 + randomSeed));
                        float x = (float)(rand.NextDouble() * 2 - 1) * randomSpread * 3f;
                        float y = (float)rand.NextDouble() * pattern[pattern.Length - 1].y;
                        baseOffset = new Vector2(x, y);
                        break;

                    default:
                        baseOffset = pattern[pattern.Length - 1];
                        break;
                }
            }

            // Apply scale
            baseOffset *= patternScale;

            // Add randomness
            if (randomSpread > 0)
            {
                var rand = new System.Random((int)(shotIndex * 1000 + randomSeed + Time.time * 1000));
                float rx = (float)(rand.NextDouble() * 2 - 1) * randomSpread;
                float ry = (float)(rand.NextDouble() * 2 - 1) * randomSpread;
                baseOffset += new Vector2(rx, ry);
            }

            return baseOffset;
        }

        /// <summary>
        /// Get the number of steps in the pattern.
        /// </summary>
        public int PatternLength => pattern?.Length ?? 0;

        /// <summary>
        /// Get/set the pattern array.
        /// </summary>
        public Vector2[] Pattern
        {
            get => pattern;
            set => pattern = value;
        }

        /// <summary>
        /// Get/set the overflow mode.
        /// </summary>
        public PatternOverflowMode Overflow
        {
            get => overflowMode;
            set => overflowMode = value;
        }

        /// <summary>
        /// Get/set random spread.
        /// </summary>
        public float RandomnessScale
        {
            get => randomSpread;
            set => randomSpread = value;
        }

        /// <summary>
        /// Recovery time per pattern step.
        /// </summary>
        public float RecoveryTimePerStep
        {
            get => recoveryTimePerStep;
            set => recoveryTimePerStep = value;
        }

        /// <summary>
        /// Delay before recovery starts.
        /// </summary>
        public float RecoveryDelay
        {
            get => recoveryDelay;
            set => recoveryDelay = value;
        }

        /// <summary>
        /// Visual kick strength for FEEL integration.
        /// </summary>
        public float VisualKickStrength
        {
            get => visualKickStrength;
            set => visualKickStrength = value;
        }

        /// <summary>
        /// Visual kick recovery speed.
        /// </summary>
        public float VisualKickRecovery
        {
            get => visualKickRecovery;
            set => visualKickRecovery = value;
        }

        /// <summary>
        /// FOV punch amount in degrees.
        /// </summary>
        public float FovPunch
        {
            get => fovPunch;
            set => fovPunch = value;
        }

        /// <summary>
        /// Whether first shot is accurate.
        /// </summary>
        public bool FirstShotAccurate
        {
            get => firstShotAccurate;
            set => firstShotAccurate = value;
        }

        /// <summary>
        /// Calculate total pattern difficulty (for balancing).
        /// </summary>
        public float CalculatePatternDifficulty()
        {
            if (pattern == null || pattern.Length == 0) return 0f;

            float totalMagnitude = 0f;
            float totalVariance = 0f;
            Vector2 prev = Vector2.zero;

            for (int i = 0; i < pattern.Length; i++)
            {
                totalMagnitude += pattern[i].magnitude;
                if (i > 0)
                {
                    totalVariance += (pattern[i] - prev).magnitude;
                }
                prev = pattern[i];
            }

            // Higher magnitude and variance = harder to control
            return (totalMagnitude + totalVariance * 2f) / pattern.Length;
        }

#if UNITY_EDITOR
        [ContextMenu("Generate Assault Rifle Pattern")]
        private void GenerateAssaultRiflePattern()
        {
            pattern = new Vector2[]
            {
                new Vector2(0f, 2.5f),
                new Vector2(0.1f, 2.2f),
                new Vector2(-0.2f, 2.0f),
                new Vector2(0.3f, 1.8f),
                new Vector2(0.5f, 1.5f),
                new Vector2(0.3f, 1.3f),
                new Vector2(-0.4f, 1.2f),
                new Vector2(-0.6f, 1.0f),
                new Vector2(-0.4f, 0.8f),
                new Vector2(0.2f, 0.7f),
                new Vector2(0.6f, 0.6f),
                new Vector2(0.4f, 0.5f),
            };
            patternScale = 1f;
            randomSpread = 0.3f;
            firstShotAccurate = true;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("Generate Pistol Pattern")]
        private void GeneratePistolPattern()
        {
            pattern = new Vector2[]
            {
                new Vector2(0f, 3f),
                new Vector2(0.2f, 2.5f),
                new Vector2(-0.2f, 2f),
            };
            patternScale = 1f;
            randomSpread = 0.2f;
            firstShotAccurate = true;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("Generate SMG Pattern")]
        private void GenerateSMGPattern()
        {
            pattern = new Vector2[]
            {
                new Vector2(0f, 1.5f),
                new Vector2(0.2f, 1.4f),
                new Vector2(-0.1f, 1.3f),
                new Vector2(0.3f, 1.2f),
                new Vector2(-0.3f, 1.1f),
                new Vector2(0.4f, 1.0f),
                new Vector2(-0.2f, 0.9f),
                new Vector2(0.2f, 0.8f),
            };
            patternScale = 1f;
            randomSpread = 0.4f;
            firstShotAccurate = false;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// How to handle shots beyond the defined pattern.
    /// </summary>
    public enum PatternOverflowMode
    {
        /// <summary>Keep using the last pattern value.</summary>
        RepeatLast,

        /// <summary>Loop back to the start of the pattern.</summary>
        Loop,

        /// <summary>Reverse through the pattern then forward again.</summary>
        PingPong,

        /// <summary>Use random values within bounds.</summary>
        Random
    }
}
