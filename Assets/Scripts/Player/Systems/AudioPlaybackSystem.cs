using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using Player.Components;
using DIG.Core.Feedback;

/// <summary>
/// Runtime consumer that looks for `FootstepEvent` and `LandingEvent` components and forwards
/// them to the scene `AudioManager` for playback. This system runs on the main thread and is
/// intentionally simple — it removes event components after processing.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class AudioPlaybackSystem : SystemBase
{
    // Manager lookup removed as we use static bridge
    // private Audio.Systems.AudioManager _audioManager;

    protected override void OnCreate()
    {
        base.OnCreate();
    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
    }

    protected override void OnUpdate()
    {
        // No audio manager check needed
        // if (_audioManager == null) ...

        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (fe, entity) in SystemAPI.Query<RefRO<FootstepEvent>>().WithEntityAccess())
        {
            var evt = fe.ValueRO;
            var pos = new Vector3(evt.Position.x, evt.Position.y, evt.Position.z);
            GameplayFeedbackManager.TriggerFootstep(evt.MaterialId, evt.Stance, pos);
            ecb.RemoveComponent<FootstepEvent>(entity);
        }

        foreach (var (le, entity) in SystemAPI.Query<RefRO<LandingEvent>>().WithEntityAccess())
        {
            var evt = le.ValueRO;
            var pos = new Vector3(evt.Position.x, evt.Position.y, evt.Position.z);
            GameplayFeedbackManager.TriggerLand(1.0f, evt.MaterialId, pos);
            ecb.RemoveComponent<LandingEvent>(entity);
        }

        // Process jump events
        foreach (var (je, entity) in SystemAPI.Query<RefRO<JumpEvent>>().WithEntityAccess())
        {
            var evt = je.ValueRO;
            var pos = new Vector3(evt.Position.x, evt.Position.y, evt.Position.z);
            GameplayFeedbackManager.TriggerJump(evt.MaterialId, evt.Intensity, pos);
            ecb.RemoveComponent<JumpEvent>(entity);
        }

        // Process roll events
        foreach (var (re, entity) in SystemAPI.Query<RefRO<RollEvent>>().WithEntityAccess())
        {
            var evt = re.ValueRO;
            var pos = new Vector3(evt.Position.x, evt.Position.y, evt.Position.z);
            GameplayFeedbackManager.TriggerRoll(evt.MaterialId, evt.Intensity, pos);
            ecb.RemoveComponent<RollEvent>(entity);
        }

        // Process dive events
        foreach (var (de, entity) in SystemAPI.Query<RefRO<DiveEvent>>().WithEntityAccess())
        {
            var evt = de.ValueRO;
            var pos = new Vector3(evt.Position.x, evt.Position.y, evt.Position.z);
            GameplayFeedbackManager.TriggerDive(evt.MaterialId, evt.Intensity, pos);
            ecb.RemoveComponent<DiveEvent>(entity);
        }

        // Process climb start events
        foreach (var (ce, entity) in SystemAPI.Query<RefRO<ClimbStartEvent>>().WithEntityAccess())
        {
            var evt = ce.ValueRO;
            var pos = new Vector3(evt.Position.x, evt.Position.y, evt.Position.z);
            GameplayFeedbackManager.TriggerClimbStart(evt.MaterialId, pos);
            ecb.RemoveComponent<ClimbStartEvent>(entity);
        }

        // Process slide events
        foreach (var (se, entity) in SystemAPI.Query<RefRO<SlideEvent>>().WithEntityAccess())
        {
            var evt = se.ValueRO;
            var pos = new Vector3(evt.Position.x, evt.Position.y, evt.Position.z);
            GameplayFeedbackManager.TriggerSlide(evt.Intensity, evt.MaterialId, pos);
            ecb.RemoveComponent<SlideEvent>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
