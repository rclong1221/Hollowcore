using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// Reads PlayerAbilityState and PlayerAbilitySlot from the local player entity
    /// and dispatches updates to the registered IAbilityUIProvider.
    ///
    /// Runs in PresentationSystemGroup on client/local only.
    ///
    /// EPIC 18.19 - Phase 7
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class AbilityUIBridgeSystem : SystemBase
    {
        private static IAbilityUIProvider _provider;

        /// <summary>
        /// Register a UI provider. Called by MonoBehaviour adapters on Awake.
        /// </summary>
        public static void RegisterProvider(IAbilityUIProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Unregister the UI provider. Called by MonoBehaviour adapters on OnDestroy.
        /// </summary>
        public static void UnregisterProvider(IAbilityUIProvider provider)
        {
            if (_provider == provider)
                _provider = null;
        }

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerAbilityState>();
        }

        protected override void OnUpdate()
        {
            if (_provider == null) return;

            var dbRef = default(AbilityDatabaseRef);
            bool hasDb = SystemAPI.TryGetSingleton(out dbRef) && dbRef.Value.IsCreated;

            foreach (var (abilityState, slots, _) in
                SystemAPI.Query<RefRO<PlayerAbilityState>, DynamicBuffer<PlayerAbilitySlot>,
                    RefRO<GhostOwnerIsLocal>>())
            {
                // Update each slot
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    float cooldownTotal = 0f;
                    int maxCharges = 0;

                    if (hasDb && slot.AbilityId >= 0)
                    {
                        ref var abilities = ref dbRef.Value.Value.Abilities;
                        if (slot.AbilityId < abilities.Length)
                        {
                            ref var def = ref abilities[slot.AbilityId];
                            cooldownTotal = def.Cooldown;
                            maxCharges = def.MaxCharges;
                        }
                    }

                    _provider.UpdateSlot(
                        i,
                        slot.AbilityId,
                        slot.CooldownRemaining,
                        cooldownTotal,
                        slot.ChargesRemaining,
                        maxCharges
                    );
                }

                // Update GCD
                _provider.UpdateGCD(abilityState.ValueRO.GCDRemaining, 1f);

                // Update cast bar
                bool isCasting = abilityState.ValueRO.Phase == AbilityCastPhase.Casting
                              || abilityState.ValueRO.Phase == AbilityCastPhase.Telegraph;
                float castProgress = 0f;
                string phaseName = "";

                if (isCasting && hasDb && abilityState.ValueRO.ActiveSlotIndex < slots.Length)
                {
                    var activeSlot = slots[abilityState.ValueRO.ActiveSlotIndex];
                    if (activeSlot.AbilityId >= 0)
                    {
                        ref var abilities = ref dbRef.Value.Value.Abilities;
                        if (activeSlot.AbilityId < abilities.Length)
                        {
                            ref var def = ref abilities[activeSlot.AbilityId];
                            float totalTime = abilityState.ValueRO.Phase == AbilityCastPhase.Telegraph
                                ? def.TelegraphDuration
                                : def.CastTime;
                            castProgress = totalTime > 0f
                                ? abilityState.ValueRO.PhaseElapsed / totalTime
                                : 1f;
                            phaseName = abilityState.ValueRO.Phase == AbilityCastPhase.Telegraph
                                ? "Telegraph"
                                : "Casting";
                        }
                    }
                }

                _provider.UpdateCastBar(isCasting, castProgress, phaseName);
            }
        }
    }
}
