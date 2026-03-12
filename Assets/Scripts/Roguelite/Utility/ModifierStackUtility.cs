using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: Shared utility for manipulating RunModifierStack buffers.
    /// Used by AscensionSetupSystem and ModifierAcquisitionSystem.
    /// </summary>
    [BurstCompile]
    public static class ModifierStackUtility
    {
        /// <summary>
        /// Attempts to add a modifier to the stack by looking up its definition in the registry.
        /// Returns true if added or stacked, false if not found or at max stacks.
        /// </summary>
        public static bool TryAddModifier(
            ref ModifierRegistryBlob registry,
            DynamicBuffer<RunModifierStack> stack,
            int modifierId)
        {
            int regIndex = FindModifierIndex(ref registry, modifierId);
            if (regIndex < 0) return false;

            ref var def = ref registry.Modifiers[regIndex];

            // Check if already in stack (for stacking)
            for (int i = 0; i < stack.Length; i++)
            {
                if (stack[i].ModifierId == modifierId)
                {
                    if (!def.Stackable || stack[i].StackCount >= def.MaxStacks)
                        return false;

                    var existing = stack[i];
                    existing.StackCount++;
                    // Multiplicative modifiers compound exponentially (1.1² = 1.21)
                    // Additive modifiers scale linearly (+0.5 × 3 stacks = +1.5)
                    existing.EffectiveValue = def.IsMultiplicative
                        ? math.pow(def.FloatValue, existing.StackCount)
                        : def.FloatValue * existing.StackCount;
                    stack[i] = existing;
                    return true;
                }
            }

            // Add new entry
            stack.Add(new RunModifierStack
            {
                ModifierId = modifierId,
                StackCount = 1,
                Target = def.Target,
                StatId = def.StatId,
                EffectiveValue = def.FloatValue,
                IsMultiplicative = def.IsMultiplicative
            });
            return true;
        }

        /// <summary>
        /// Checks whether a modifier can be added (exists in registry and not at max stacks).
        /// </summary>
        public static bool CanAddModifier(
            ref ModifierRegistryBlob registry,
            DynamicBuffer<RunModifierStack> stack,
            int modifierId)
        {
            int regIndex = FindModifierIndex(ref registry, modifierId);
            if (regIndex < 0) return false;

            ref var def = ref registry.Modifiers[regIndex];

            for (int i = 0; i < stack.Length; i++)
            {
                if (stack[i].ModifierId == modifierId)
                    return def.Stackable && stack[i].StackCount < def.MaxStacks;
            }

            return true; // Not in stack yet
        }

        /// <summary>
        /// Linear scan for modifier index in registry blob. Returns -1 if not found.
        /// </summary>
        private static int FindModifierIndex(ref ModifierRegistryBlob registry, int modifierId)
        {
            for (int i = 0; i < registry.Modifiers.Length; i++)
            {
                if (registry.Modifiers[i].ModifierId == modifierId)
                    return i;
            }
            return -1;
        }
    }
}
