using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using DIG.Combat.Components;
using Player.Components;

namespace DIG.VFX.Systems
{
    /// <summary>
    /// EPIC 16.7 Phase 5: Managed companion to CorpseSinkSystem that drives dissolve shader.
    /// When an entity has DissolveCapable tag + a renderer using DIG/URP/Dissolve shader,
    /// this system animates _DissolveAmount from 0→1 over FadeOutDuration.
    /// Entities without dissolve materials fall through to CorpseSinkSystem position sink.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(DIG.Combat.Systems.CorpseSinkSystem))]
    public partial class CorpseDissolveSystem : SystemBase
    {
        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        static readonly ProfilerMarker k_Marker = new("CorpseDissolveSystem.Update");

        // Cache: avoid GetComponentsInChildren + new MaterialPropertyBlock every frame
        private readonly Dictionary<Entity, CachedRendererData> _rendererCache = new();
        private readonly List<Entity> _staleEntries = new();

        private struct CachedRendererData
        {
            public Renderer[] Renderers;
            public MaterialPropertyBlock MPB;
        }

        protected override void OnUpdate()
        {
            k_Marker.Begin();

            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            float dt = SystemAPI.Time.DeltaTime;

            if (dt <= 0f)
            {
                k_Marker.End();
                return;
            }

            // Global config
            float ragdollDur = 2.0f;
            float corpseLife = 15.0f;
            float fadeDur = 1.5f;
            if (SystemAPI.HasSingleton<CorpseConfig>())
            {
                var cfg = SystemAPI.GetSingleton<CorpseConfig>();
                ragdollDur = cfg.RagdollDuration;
                corpseLife = cfg.CorpseLifetime;
                fadeDur = cfg.FadeOutDuration;
            }

            float sinkStartDelay = ragdollDur + corpseLife;
            float dissolveRate = 1f / math.max(fadeDur, 0.01f);

            // Track which cached entities are still alive this frame
            _staleEntries.Clear();
            foreach (var kvp in _rendererCache)
                _staleEntries.Add(kvp.Key);

            foreach (var (deathState, dissolveCapable, entity) in
                     SystemAPI.Query<RefRO<DeathState>, RefRO<DissolveCapable>>()
                     .WithNone<PlayerTag>()
                     .WithEntityAccess())
            {
                _staleEntries.Remove(entity);

                if (deathState.ValueRO.Phase == DeathPhase.Alive)
                    continue;

                float timeSinceDeath = currentTime - deathState.ValueRO.StateStartTime;

                // Not yet in fade phase
                if (timeSinceDeath < sinkStartDelay)
                    continue;

                // Compute dissolve amount
                float elapsed = timeSinceDeath - sinkStartDelay;
                float dissolveAmount = math.saturate(elapsed * dissolveRate);

                // Get or cache renderers
                if (!_rendererCache.TryGetValue(entity, out var cached))
                {
                    var go = GetGameObjectForEntity(entity);
                    if (go == null) continue;

                    var renderers = go.GetComponentsInChildren<Renderer>();
                    if (renderers == null || renderers.Length == 0) continue;

                    cached = new CachedRendererData
                    {
                        Renderers = renderers,
                        MPB = new MaterialPropertyBlock()
                    };
                    _rendererCache[entity] = cached;
                }

                // Apply dissolve via cached MPB
                foreach (var renderer in cached.Renderers)
                {
                    if (renderer == null) continue;
                    renderer.GetPropertyBlock(cached.MPB);
                    cached.MPB.SetFloat(DissolveAmountId, dissolveAmount);
                    renderer.SetPropertyBlock(cached.MPB);
                }
            }

            // Evict stale entries (entities destroyed or no longer matching query)
            for (int i = 0; i < _staleEntries.Count; i++)
                _rendererCache.Remove(_staleEntries[i]);

            k_Marker.End();
        }

        private GameObject GetGameObjectForEntity(Entity entity)
        {
            if (EntityManager.HasComponent<Transform>(entity))
            {
                var transform = EntityManager.GetComponentObject<Transform>(entity);
                if (transform != null)
                    return transform.gameObject;
            }

            return null;
        }

        protected override void OnDestroy()
        {
            _rendererCache.Clear();
        }
    }
}
