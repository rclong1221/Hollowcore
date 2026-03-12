using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Audio.Components;
using DIG.Combat.Components;
using DIG.Music;

namespace Audio.Systems
{
    /// <summary>
    /// Ducks music and ambient audio when the local player enters combat.
    /// When CombatState.IsInCombat transitions true:
    ///   - MusicBus -> -3dB, low-pass at 8kHz
    ///   - AmbientBus -> -4dB
    /// On combat exit + 5s grace period:
    ///   - Smooth return to normal (2s crossfade)
    /// Uses the existing DIG.Combat.Components.CombatState component.
    /// EPIC 15.27 Phase 6.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MusicPlaybackSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class CombatMusicDuckSystem : SystemBase
    {
        private AudioManager _audioManager;
        private float _combatExitTimer;
        private float _currentMusicDuck;
        private float _currentAmbientDuck;

        private const float kMusicDuckDB = -3f;
        private const float kAmbientDuckDB = -4f;
        private const float kGracePeriod = 5f;
        private const float kFadeSpeed = 0.5f; // 2s crossfade

        protected override void OnUpdate()
        {
            if (_audioManager == null)
            {
                _audioManager = Object.FindAnyObjectByType<AudioManager>();
                if (_audioManager == null) return;
            }

            if (_audioManager.MasterMixer == null) return;

            float dt = SystemAPI.Time.DeltaTime;
            bool isInCombat = false;

            // EPIC 17.5: Read centralized combat state from MusicState singleton
            if (SystemAPI.HasSingleton<MusicState>())
            {
                var musicState = SystemAPI.GetSingleton<MusicState>();
                isInCombat = musicState.IsInCombat;
            }
            else
            {
                // Fallback: direct CombatState read if music system not initialized
                foreach (var combatState in SystemAPI.Query<RefRO<CombatState>>().WithAll<GhostOwnerIsLocal>())
                {
                    isInCombat = combatState.ValueRO.IsInCombat;
                }
            }

            // Track combat exit grace period
            if (isInCombat)
            {
                _combatExitTimer = kGracePeriod;
            }
            else
            {
                _combatExitTimer = math.max(0f, _combatExitTimer - dt);
            }

            bool shouldDuck = isInCombat || _combatExitTimer > 0f;

            // Smooth lerp duck values
            float targetMusicDuck = shouldDuck ? kMusicDuckDB : 0f;
            float targetAmbientDuck = shouldDuck ? kAmbientDuckDB : 0f;

            _currentMusicDuck = math.lerp(_currentMusicDuck, targetMusicDuck, dt * kFadeSpeed);
            _currentAmbientDuck = math.lerp(_currentAmbientDuck, targetAmbientDuck, dt * kFadeSpeed);

            // Apply to mixer (exposed params: MusicVolume, AmbientVolume, MusicCutoff)
            _audioManager.MasterMixer.SetFloat("MusicVolume", _currentMusicDuck);
            _audioManager.MasterMixer.SetFloat("AmbientVolume", _currentAmbientDuck);

            float musicCutoff = shouldDuck ? 8000f : 22000f;
            _audioManager.MasterMixer.SetFloat("MusicCutoff", musicCutoff);
        }
    }
}
