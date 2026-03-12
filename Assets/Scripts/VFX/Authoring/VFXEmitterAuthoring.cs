using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.VFX.Authoring
{
    /// <summary>
    /// EPIC 16.7 Phase 6: Convenience authoring component for VFX-emitting entities.
    /// Bakes a VFXEmitter component that VFXEmitterSystem reads at runtime.
    /// Used for ambient VFX points, spawn effects, zone entry effects, etc.
    /// </summary>
    public class VFXEmitterAuthoring : MonoBehaviour
    {
        [Header("VFX Configuration")]
        public int VFXTypeId;
        public VFXCategory Category = VFXCategory.Ambient;
        public float Intensity = 1.0f;
        public float Scale = 1.0f;
        public Color ColorTint = Color.clear;
        public float Duration = 0f;
        public int Priority = 0;

        [Header("Emission")]
        public VFXEmissionMode EmissionMode = VFXEmissionMode.OneShot;
        public float RepeatInterval = 1.0f;

        [Header("Trigger")]
        [Tooltip("0 = emit immediately. >0 = emit when player enters this radius.")]
        public float TriggerRadius = 0f;

        private class Baker : Baker<VFXEmitterAuthoring>
        {
            public override void Bake(VFXEmitterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new VFXEmitter
                {
                    VFXTypeId = authoring.VFXTypeId,
                    Category = authoring.Category,
                    Intensity = authoring.Intensity,
                    Scale = authoring.Scale,
                    ColorTint = new float4(authoring.ColorTint.r, authoring.ColorTint.g,
                                          authoring.ColorTint.b, authoring.ColorTint.a),
                    Duration = authoring.Duration,
                    Priority = authoring.Priority,
                    EmissionMode = authoring.EmissionMode,
                    RepeatInterval = authoring.RepeatInterval,
                    TriggerRadius = authoring.TriggerRadius,
                    LastEmitTime = float.NegativeInfinity,
                    HasEmittedOneShot = false
                });
            }
        }
    }

    /// <summary>
    /// EPIC 16.7 Phase 6: Emission mode for VFX emitter entities.
    /// </summary>
    public enum VFXEmissionMode : byte
    {
        OneShot = 0,
        Repeating = 1,
        Proximity = 2
    }

    /// <summary>
    /// EPIC 16.7 Phase 6: Runtime state for VFX emitter entities.
    /// </summary>
    public struct VFXEmitter : IComponentData
    {
        public int VFXTypeId;
        public VFXCategory Category;
        public float Intensity;
        public float Scale;
        public float4 ColorTint;
        public float Duration;
        public int Priority;
        public VFXEmissionMode EmissionMode;
        public float RepeatInterval;
        public float TriggerRadius;
        public float LastEmitTime;
        public bool HasEmittedOneShot;
    }
}
