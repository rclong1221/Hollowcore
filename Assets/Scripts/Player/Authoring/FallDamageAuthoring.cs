using UnityEngine;
using Unity.Entities;
using Player.Components;

[AddComponentMenu("Player/Authoring/Fall Damage Authoring")]
public class FallDamageAuthoring : MonoBehaviour
{
    [Header("Fall Damage Settings")]
    public float SafeFallHeight = 3.0f;
    public float MaxSafeFallHeight = 6.0f;
    public float LethalFallHeight = 15.0f;
    public float DamagePerMeter = 10.0f;
    [Header("Landing VFX")]
    public float ShakeAmplitude = 0.25f;
    public float ShakeFrequency = 6.0f;
    public float ShakeDecay = 2.5f;
    [Tooltip("How long the landing flag should persist for Mono adapters (seconds)")]
    public float LandingFlagDuration = 0.5f;

    class Baker : Baker<FallDamageAuthoring>
    {
        public override void Bake(FallDamageAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(e, new FallDamageSettings
            {
                SafeFallHeight = authoring.SafeFallHeight,
                MaxSafeFallHeight = authoring.MaxSafeFallHeight,
                LethalFallHeight = authoring.LethalFallHeight,
                DamagePerMeter = authoring.DamagePerMeter,
                ShakeAmplitude = authoring.ShakeAmplitude,
                ShakeFrequency = authoring.ShakeFrequency,
                ShakeDecay = authoring.ShakeDecay,
                LandingFlagDuration = authoring.LandingFlagDuration
            });
        }
    }
}
