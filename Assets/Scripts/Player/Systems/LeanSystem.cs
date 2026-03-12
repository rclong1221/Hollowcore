using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Player.Components;
using Player.Settings;

// Minimal lean system: reads lean inputs on `PlayerInputComponent` and smooths
// a `LeanState` component (CurrentLean -> TargetLean). Camera/animation systems
// can consume `LeanState.CurrentLean` to apply visuals.
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class LeanSystem : SystemBase
{
    private LeanSettings _settings;

    protected override void OnCreate()
    {
        base.OnCreate();
        // Try to load designer settings from Resources/LeanSettings.asset
        _settings = UnityEngine.Resources.Load<LeanSettings>("LeanSettings");
        if (_settings == null)
        {
            // If not present, create a lightweight default in-memory settings object
            _settings = UnityEngine.ScriptableObject.CreateInstance<LeanSettings>();
        }
    }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        // Copy primitive values out of the ScriptableObject so we don't capture
        // the UnityEngine.Object inside any job/closure.
        var em = EntityManager;
        float leanSpeedSetting = _settings != null ? _settings.LeanSpeed : 5f;
        bool canLeanWhileMoving = _settings != null ? _settings.CanLeanWhileMoving : true;
        float deadMoveThreshold = _settings != null ? _settings.DeadMoveThreshold : 0.1f;
        // Add LeanState to any player entities missing it so we have a stable target
        var missingQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(PlayerTag) },
            None = new ComponentType[] { typeof(LeanState) }
        });

        if (missingQuery.CalculateEntityCount() > 0)
        {
            using var ents = missingQuery.ToEntityArray(Allocator.Temp);
            foreach (var e in ents)
            {
                if (!em.HasComponent<LeanState>(e))
                {
                    em.AddComponentData(e, new LeanState { CurrentLean = 0f, TargetLean = 0f, LeanSpeed = leanSpeedSetting });
                }
            }
        }
        // Unified update: iterate all entities with LeanState and resolve input
        // preference: NetCode `PlayerInput` (predicted) takes priority when present,
        // otherwise fall back to hybrid `PlayerInputComponent`.
        foreach (var (rwLean, entity) in SystemAPI.Query<RefRW<LeanState>>().WithEntityAccess())
        {
            ref var lean = ref rwLean.ValueRW;

            // Determine input source
            bool leftPressed = false;
            bool rightPressed = false;
            float2 move = float2.zero;

            if (em.HasComponent<PlayerInput>(entity))
            {
                var net = em.GetComponentData<PlayerInput>(entity);
                leftPressed = net.LeanLeft.IsSetByte != 0;
                rightPressed = net.LeanRight.IsSetByte != 0;
                move = new float2(net.Horizontal, net.Vertical);
            }
            else if (em.HasComponent<Player.Components.PlayerInputComponent>(entity))
            {
                var hyb = em.GetComponentData<Player.Components.PlayerInputComponent>(entity);
                leftPressed = hyb.LeanLeft != 0;
                rightPressed = hyb.LeanRight != 0;
                move = hyb.Move;
            }
            else
            {
                // No input available for this entity
                continue;
            }

            float target = 0f;
            if (leftPressed && !rightPressed)
                target = -1f;
            else if (rightPressed && !leftPressed)
                target = 1f;
            else
                target = 0f;

            // Block leaning while moving if configured
            if (!canLeanWhileMoving)
            {
                float moveMag = math.length(move);
                if (moveMag > deadMoveThreshold)
                    target = 0f;
            }

            lean.TargetLean = target;

            // Smooth current -> target using per-entity LeanSpeed
            float current = lean.CurrentLean;
            float maxDelta = lean.LeanSpeed * dt;
            float diff = lean.TargetLean - current;
            if (math.abs(diff) <= maxDelta)
            {
                current = lean.TargetLean;
            }
            else
            {
                current += math.sign(diff) * maxDelta;
            }
            lean.CurrentLean = math.clamp(current, -1f, 1f);

#if LEAN_DEBUG
            // Throttled debug: only log when target or current non-zero to avoid spam
            if (math.abs(lean.TargetLean) > 0.001f || math.abs(lean.CurrentLean) > 0.001f)
            {
                UnityEngine.Debug.Log($"[LeanSystem] Entity {entity} Target={lean.TargetLean} Current={lean.CurrentLean} move={move}");
            }
#endif
        }
    }
}
