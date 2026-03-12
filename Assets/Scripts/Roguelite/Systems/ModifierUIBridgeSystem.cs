using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: Managed bridge from ECS modifier/difficulty state to game UI.
    /// Runs in PresentationSystemGroup (Client|Local only).
    /// Pushes modifier count, difficulty values, and choice availability to IModifierUIProvider.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ModifierUIBridgeSystem : SystemBase
    {
        private int _lastModifierCount = -1;
        private float _lastDifficulty;
        private int _lastChoiceCount;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
            RequireForUpdate<RuntimeDifficultyScale>();
        }

        protected override void OnUpdate()
        {
            if (!ModifierUIRegistry.HasProvider) return;

            var runEntity = SystemAPI.GetSingletonEntity<RunState>();
            var difficulty = SystemAPI.GetSingleton<RuntimeDifficultyScale>();

            int modCount = 0;
            if (SystemAPI.HasBuffer<RunModifierStack>(runEntity))
                modCount = SystemAPI.GetBuffer<RunModifierStack>(runEntity).Length;

            // Push modifier display update on change
            if (modCount != _lastModifierCount || math.abs(difficulty.ZoneDifficultyMultiplier - _lastDifficulty) > 0.001f)
            {
                _lastModifierCount = modCount;
                _lastDifficulty = difficulty.ZoneDifficultyMultiplier;
                ModifierUIRegistry.Provider.UpdateModifierDisplay(modCount, difficulty.ZoneDifficultyMultiplier);
            }

            // Notify UI of pending choices (fire once when choices appear)
            if (SystemAPI.HasBuffer<PendingModifierChoice>(runEntity))
            {
                int choiceCount = SystemAPI.GetBuffer<PendingModifierChoice>(runEntity).Length;
                if (choiceCount != _lastChoiceCount && choiceCount > 0)
                    ModifierUIRegistry.Provider.OnModifierChoicesReady(choiceCount);
                _lastChoiceCount = choiceCount;
            }
        }
    }
}
