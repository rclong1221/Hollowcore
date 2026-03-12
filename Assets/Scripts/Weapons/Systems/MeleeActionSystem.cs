using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Items;
using DIG.Weapons.Config;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Handles melee attacks, hitbox timing, and combo chains.
    /// EPIC 15.7: Configurable combo system with InputPerSwing, HoldToCombo, RhythmBased modes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(UsableActionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct MeleeActionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            bool isServer = state.WorldUnmanaged.IsServer();

            // Get global combo settings (or use defaults if not present)
            ComboSystemSettings globalSettings = ComboSystemSettings.Default;
            if (SystemAPI.HasSingleton<ComboSystemSettings>())
            {
                globalSettings = SystemAPI.GetSingleton<ComboSystemSettings>();
            }

            foreach (var (action, melee, meleeState, hitbox, request, transform, charItem, entity) in
                     SystemAPI.Query<RefRW<UsableAction>, RefRO<MeleeAction>, RefRW<MeleeState>,
                                    RefRW<MeleeHitbox>, RefRO<UseRequest>, RefRO<LocalTransform>, RefRO<CharacterItem>>()
                     .WithEntityAccess())
            {
                Entity owner = charItem.ValueRO.OwnerEntity;
                bool hasGhostOwnerIsLocal = owner != Entity.Null && SystemAPI.HasComponent<GhostOwnerIsLocal>(owner);
                bool isGhostOwnerEnabled = hasGhostOwnerIsLocal && SystemAPI.IsComponentEnabled<GhostOwnerIsLocal>(owner);

                // Client-side: Only process weapons owned by the local player
                if (!isServer && (owner == Entity.Null || !isGhostOwnerEnabled))
                    continue;

                ref var stateRef = ref meleeState.ValueRW;
                ref var hitboxRef = ref hitbox.ValueRW;
                var config = melee.ValueRO;

                // ============================================================
                // Resolve effective combo settings (global vs per-weapon)
                // ============================================================
                ComboInputMode inputMode = globalSettings.InputMode;
                int queueDepth = globalSettings.QueueDepth;
                ComboCancelPolicy cancelPolicy = globalSettings.CancelPolicy;
                ComboCancelPriority cancelPriority = globalSettings.CancelPriority;
                ComboQueueClearPolicy queueClearPolicy = globalSettings.QueueClearPolicy;
                float rhythmWindowStart = globalSettings.RhythmWindowStart;
                float rhythmWindowEnd = globalSettings.RhythmWindowEnd;
                float rhythmPerfectBonus = globalSettings.RhythmPerfectBonus;

                if (!config.UseGlobalComboConfig && globalSettings.AllowPerWeaponOverride)
                {
                    inputMode = (ComboInputMode)config.InputModeOverride;
                    queueDepth = config.QueueDepthOverride;
                    cancelPolicy = (ComboCancelPolicy)config.CancelPolicyOverride;
                    cancelPriority = (ComboCancelPriority)config.CancelPriorityOverride;
                    queueClearPolicy = (ComboQueueClearPolicy)config.QueueClearPolicyOverride;
                }

                // ============================================================
                // Check for cancel inputs from player
                // ============================================================
                bool cancelRequested = false;
                bool shouldClearQueue = false;

                if (owner != Entity.Null && SystemAPI.HasComponent<PlayerInput>(owner))
                {
                    var playerInput = SystemAPI.GetComponent<PlayerInput>(owner);

                    if ((cancelPriority & ComboCancelPriority.Dodge) != 0 &&
                        (playerInput.DodgeRoll.IsSet || playerInput.DodgeDive.IsSet))
                    {
                        cancelRequested = true;
                        if ((queueClearPolicy & ComboQueueClearPolicy.OnDodge) != 0)
                            shouldClearQueue = true;
                    }

                    if ((cancelPriority & ComboCancelPriority.Jump) != 0 && playerInput.Jump.IsSet)
                        cancelRequested = true;

                    if ((cancelPriority & ComboCancelPriority.Block) != 0 && playerInput.AltUse.IsSet)
                    {
                        cancelRequested = true;
                        if ((queueClearPolicy & ComboQueueClearPolicy.OnBlock) != 0)
                            shouldClearQueue = true;
                    }

                    if ((cancelPriority & ComboCancelPriority.Movement) != 0 &&
                        (playerInput.Horizontal != 0 || playerInput.Vertical != 0))
                    {
                        cancelRequested = true;
                    }
                }

                // ============================================================
                // Get combo data from buffer
                // ============================================================
                bool hasComboData = SystemAPI.HasBuffer<ComboData>(entity);
                DynamicBuffer<ComboData> comboBuffer = default;
                if (hasComboData) comboBuffer = SystemAPI.GetBuffer<ComboData>(entity);

                float currentAttackSpeed = config.AttackSpeed;
                float currentHitboxStart = config.HitboxActiveStart;
                float currentHitboxEnd = config.HitboxActiveEnd;
                float currentComboWindow = config.ComboWindow;
                int maxCombos = config.ComboCount;

                if (hasComboData && comboBuffer.Length > 0)
                {
                    int safeIndex = math.min(stateRef.CurrentCombo, comboBuffer.Length - 1);
                    var step = comboBuffer[safeIndex];
                    currentAttackSpeed = step.Duration > 0 ? 1f / step.Duration : config.AttackSpeed;
                    currentComboWindow = step.InputWindowEnd - step.InputWindowStart;
                    maxCombos = comboBuffer.Length;
                }

                // ============================================================
                // Input detection (mode-specific)
                // ============================================================
                bool currentInput = request.ValueRO.StartUse;
                bool previousInput = stateRef.PreviousInputState;
                bool isNewPress = currentInput && !previousInput;
                bool isHeld = currentInput && previousInput;
                bool isReleased = !currentInput && previousInput;

                // Update previous input state for next frame
                stateRef.PreviousInputState = currentInput;

                // Determine if we should queue/start attack based on input mode
                bool shouldQueueFromInput = false;
                bool shouldStartFromInput = false;

                switch (inputMode)
                {
                    case ComboInputMode.InputPerSwing:
                        shouldQueueFromInput = isNewPress;
                        shouldStartFromInput = isNewPress && !stateRef.InputConsumed;
                        break;

                    case ComboInputMode.HoldToCombo:
                        shouldQueueFromInput = currentInput;
                        shouldStartFromInput = currentInput;
                        break;

                    case ComboInputMode.RhythmBased:
                        if (stateRef.IsAttacking)
                        {
                            float attackDuration = 1f / currentAttackSpeed;
                            float normalizedTime = stateRef.AttackTime / attackDuration;

                            if (isNewPress)
                            {
                                if (normalizedTime >= rhythmWindowStart && normalizedTime <= rhythmWindowEnd)
                                {
                                    shouldQueueFromInput = true;
                                    stateRef.RhythmSuccess = true;
                                    stateRef.RhythmBonus = rhythmPerfectBonus;
                                }
                                else if (normalizedTime < rhythmWindowStart)
                                {
                                    stateRef.RhythmSuccess = false;
                                    stateRef.RhythmBonus = 1f;
                                    stateRef.QueuedAttack = false;
                                    stateRef.QueuedAttackCount = 0;
                                }
                            }
                        }
                        else
                        {
                            shouldStartFromInput = isNewPress;
                        }
                        break;
                }

                // ============================================================
                // Attack state update
                // ============================================================
                bool wasAttacking = stateRef.IsAttacking;

                // Handle cancel request
                if (stateRef.IsAttacking && cancelRequested)
                {
                    float attackDuration = 1f / currentAttackSpeed;
                    float normalizedTime = stateRef.AttackTime / attackDuration;
                    bool inRecovery = normalizedTime > currentHitboxEnd;

                    bool canCancel = cancelPolicy == ComboCancelPolicy.Anytime ||
                                     (cancelPolicy == ComboCancelPolicy.RecoveryOnly && inRecovery);

                    if (canCancel)
                    {
                        stateRef.IsAttacking = false;
                        stateRef.HitboxActive = false;
                        hitboxRef.IsActive = false;
                        stateRef.AttackTime = 0f;
                        stateRef.NormalizedTime = 0f;
                        stateRef.WasCanceled = true;
                        stateRef.InputConsumed = false;

                        if (shouldClearQueue || (queueClearPolicy & ComboQueueClearPolicy.OnCancel) != 0)
                        {
                            stateRef.QueuedAttack = false;
                            stateRef.QueuedAttackCount = 0;
                        }
                    }
                }

                if (stateRef.IsAttacking)
                {
                    stateRef.AttackTime += deltaTime;
                    stateRef.TimeSinceAttack = 0f;

                    float attackDuration = 1f / currentAttackSpeed;
                    float normalizedTime = stateRef.AttackTime / attackDuration;
                    stateRef.NormalizedTime = normalizedTime;

                    // Hitbox activation
                    bool shouldBeActive = normalizedTime >= currentHitboxStart && normalizedTime <= currentHitboxEnd;

                    if (shouldBeActive && !stateRef.HitboxActive)
                    {
                        stateRef.HitboxActive = true;
                        hitboxRef.IsActive = true;
                        stateRef.HasHitThisSwing = false;
                    }
                    else if (!shouldBeActive && stateRef.HitboxActive)
                    {
                        stateRef.HitboxActive = false;
                        hitboxRef.IsActive = false;
                    }

                    // Hit detection
                    if (stateRef.HitboxActive && !stateRef.HasHitThisSwing)
                    {
                        float3 hitboxCenter = transform.ValueRO.Position +
                                              math.mul(transform.ValueRO.Rotation, hitbox.ValueRO.Offset);

                        var aabb = new Aabb
                        {
                            Min = hitboxCenter - hitbox.ValueRO.Size * 0.5f,
                            Max = hitboxCenter + hitbox.ValueRO.Size * 0.5f
                        };

                        var bodyIndices = new NativeList<int>(Allocator.Temp);
                        if (physicsWorld.OverlapAabb(new OverlapAabbInput
                        {
                            Aabb = aabb,
                            Filter = CollisionFilter.Default
                        }, ref bodyIndices))
                        {
                            var bodies = physicsWorld.CollisionWorld.Bodies;
                            foreach (var bodyIndex in bodyIndices)
                            {
                                if (bodyIndex >= 0 && bodyIndex < bodies.Length)
                                {
                                    var hitEntity = bodies[bodyIndex].Entity;
                                    if (hitEntity != entity && hitEntity != owner)
                                    {
                                        stateRef.HasHitThisSwing = true;
                                        break;
                                    }
                                }
                            }
                        }
                        bodyIndices.Dispose();
                    }

                    // Queue attack based on input mode and queue depth
                    if (shouldQueueFromInput && !stateRef.QueuedAttack)
                    {
                        if (queueDepth < 0 || stateRef.QueuedAttackCount < queueDepth)
                        {
                            stateRef.QueuedAttack = true;
                            stateRef.QueuedAttackCount++;
                        }
                    }

                    // Attack complete
                    if (normalizedTime >= 1f)
                    {
                        stateRef.IsAttacking = false;
                        stateRef.HitboxActive = false;
                        hitboxRef.IsActive = false;
                        stateRef.AttackTime = 0f;
                        stateRef.NormalizedTime = 0f;
                        stateRef.InputConsumed = false;
                    }
                }
                else
                {
                    stateRef.TimeSinceAttack += deltaTime;

                    // Reset combo if outside window
                    if (stateRef.TimeSinceAttack > currentComboWindow)
                    {
                        stateRef.CurrentCombo = 0;
                        stateRef.RhythmBonus = 1f;
                    }

                    // For InputPerSwing: reset input consumed when attack ends and no input
                    if (!currentInput)
                    {
                        stateRef.InputConsumed = false;
                    }
                }

                // ============================================================
                // Start attack logic
                // ============================================================
                bool shouldStartAttack = !stateRef.IsAttacking && (shouldStartFromInput || stateRef.QueuedAttack);

                if (shouldStartAttack)
                {
                    stateRef.IsAttacking = true;
                    stateRef.AttackTime = 0f;
                    stateRef.HasHitThisSwing = false;
                    stateRef.WasCanceled = false;

                    // Mark input as consumed for InputPerSwing mode
                    if (inputMode == ComboInputMode.InputPerSwing)
                    {
                        stateRef.InputConsumed = true;
                    }

                    // Consume queued attack
                    bool wasQueued = stateRef.QueuedAttack;
                    if (stateRef.QueuedAttack)
                    {
                        stateRef.QueuedAttack = false;
                        stateRef.QueuedAttackCount = math.max(0, stateRef.QueuedAttackCount - 1);
                    }

                    // Advance combo
                    if (wasQueued || (stateRef.TimeSinceAttack <= currentComboWindow && stateRef.CurrentCombo < maxCombos - 1))
                    {
                        stateRef.CurrentCombo++;
                    }
                    else
                    {
                        stateRef.CurrentCombo = 0;
                    }

                    stateRef.TimeSinceAttack = 0f;
                }
            }
        }
    }
}
