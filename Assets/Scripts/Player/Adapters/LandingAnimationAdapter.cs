using Unity.Entities;
using UnityEngine;
using Unity.NetCode;
using Player.Bridges;

/// <summary>
/// MonoBehaviour adapter that listens for a `LandingFlag` on the local player entity
/// and triggers the attached Animator/LandingAnimatorBridge to play a landing animation.
/// Attach this to the player prefab (the GameObject that has the Animator).
/// 
/// This adapter bridges ECS (DOTS) landing events to MonoBehaviour presentation layer.
/// </summary>
public class LandingAnimationAdapter : MonoBehaviour
{
    [Tooltip("Animator to trigger when landing occurs. If null, will get Animator on same GameObject.")]
    public Animator Animator;

    [Header("Animator Parameter Names (Legacy Fallback)")]
    [Tooltip("Trigger parameter name to activate landing animation (default: 'Land')")]
    public string LandTrigger = "Land";

    [Tooltip("Optional float parameter name for landing intensity (default: 'LandIntensity'). Leave blank to skip.")]
    public string LandIntensityParam = "LandIntensity";

    // Cached client world reference
    private World _clientWorld;
    private Unity.Entities.EntityQuery _playerQueryWithOwner;
    private Unity.Entities.EntityQuery _playerQueryFallback;
    private Entity _cachedPlayerEntity = Entity.Null;
    private AnimatorRigBridge _animRigBridge;
    private LandingAnimatorBridge _landingBridge;
    private bool _landingTriggered;
    private float _lastIntensity;

    private void Awake()
    {
        if (Animator == null) Animator = GetComponent<Animator>();
        // Find animation bridges
        _animRigBridge = GetComponent<AnimatorRigBridge>();
        _landingBridge = GetComponent<LandingAnimatorBridge>();
    }

    private void Update()
    {
        // Ensure we have a client world
        if (_clientWorld == null || !_clientWorld.IsCreated)
        {
            foreach (var world in World.All)
            {
                if (world.IsCreated && world.Name == "ClientWorld")
                {
                    _clientWorld = world;
                    break;
                }
            }
            if (_clientWorld == null) return;

            // Cache queries for the found world
            var em = _clientWorld.EntityManager;
            _playerQueryWithOwner = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<GhostOwnerIsLocal>());
            _playerQueryFallback = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>());
        }

        var entityManager = _clientWorld.EntityManager;

        // Resolve cached player entity if needed
        if (_cachedPlayerEntity == Entity.Null || !entityManager.Exists(_cachedPlayerEntity))
        {
            if (!_playerQueryWithOwner.IsEmptyIgnoreFilter)
            {
                using (var arr = _playerQueryWithOwner.ToEntityArray(Unity.Collections.Allocator.Temp))
                {
                    if (arr.Length > 0) _cachedPlayerEntity = arr[0];
                }
            }

            if (_cachedPlayerEntity == Entity.Null && !_playerQueryFallback.IsEmptyIgnoreFilter)
            {
                using (var arr = _playerQueryFallback.ToEntityArray(Unity.Collections.Allocator.Temp))
                {
                    if (arr.Length > 0) _cachedPlayerEntity = arr[0];
                }
            }
        }

        if (_cachedPlayerEntity == Entity.Null) return;

        // Read LandingFlag (non-structural read). If present and time > 0, trigger animation.
        if (entityManager.HasComponent<Player.Components.LandingFlag>(_cachedPlayerEntity))
        {
            var lf = entityManager.GetComponentData<Player.Components.LandingFlag>(_cachedPlayerEntity);
            
            // Only trigger once per landing (check if this is a new landing)
            bool isNewLanding = lf.TimeLeft > 0f && (!_landingTriggered || lf.Intensity != _lastIntensity);
            
            if (isNewLanding)
            {
                _landingTriggered = true;
                _lastIntensity = lf.Intensity;
                
                // Prefer LandingAnimatorBridge if available (full-featured)
                if (_landingBridge != null)
                {
                    _landingBridge.TriggerLandingWithIntensity(lf.Intensity);
                }
                else
                {
                    // Fallback to direct animator control
                    if (Animator != null)
                    {
                        if (!string.IsNullOrEmpty(LandIntensityParam))
                        {
                            Animator.SetFloat(LandIntensityParam, lf.Intensity);
                        }
                        if (!string.IsNullOrEmpty(LandTrigger))
                        {
                            Animator.SetTrigger(LandTrigger);
                        }
                    }
                }
                
                // Also notify the AnimatorRigBridge if present so client-side rigs can react
                if (_animRigBridge != null)
                {
                    _animRigBridge.TriggerLanding();
                }
            }
        }
        else
        {
            // Reset trigger flag when LandingFlag is removed
            _landingTriggered = false;
        }
    }
}
