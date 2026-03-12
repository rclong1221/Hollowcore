using Unity.Entities;
using UnityEngine;
using UnityEngine.Playables;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Managed SystemBase that drives PlayableDirector lifecycle.
    /// Starts/stops Timeline playback, reads SignalReceiver callbacks,
    /// enqueues CinematicAnimEvent to static queue, triggers dialogue.
    /// Must be managed -- PlayableDirector is UnityEngine.Object.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class CinematicPlaybackSystem : SystemBase
    {
        private EntityQuery _stateQuery;
        private bool _wasPlaying;
        private int _lastCinematicId;
        private CinematicDefinitionSO _activeDef;
        private int _currentSubtitleIndex;
        private AudioSource _voiceAudioSource;

        protected override void OnCreate()
        {
            _stateQuery = GetEntityQuery(ComponentType.ReadWrite<CinematicState>());
            RequireForUpdate(_stateQuery);
        }

        protected override void OnUpdate()
        {
            var stateEntity = _stateQuery.GetSingletonEntity();
            var state = EntityManager.GetComponentData<CinematicState>(stateEntity);

            // Transition: not playing -> playing (start)
            if (state.IsPlaying && !_wasPlaying)
            {
                StartPlayback(ref state, stateEntity);
            }

            // Transition: playing -> not playing (end)
            if (!state.IsPlaying && _wasPlaying)
            {
                StopPlayback(stateEntity);
            }

            // During playback: tick elapsed time
            if (state.IsPlaying)
            {
                state.ElapsedTime += SystemAPI.Time.DeltaTime;

                // Process subtitle timing
                if (_activeDef != null && _activeDef.SubtitleKeys != null)
                {
                    while (_currentSubtitleIndex < _activeDef.SubtitleKeys.Length &&
                           _currentSubtitleIndex < _activeDef.SubtitleTimings.Length &&
                           state.ElapsedTime >= _activeDef.SubtitleTimings[_currentSubtitleIndex])
                    {
                        string subtitleText = _activeDef.SubtitleKeys[_currentSubtitleIndex];
                        float subtitleDuration = 3f; // Default display duration
                        if (_currentSubtitleIndex + 1 < _activeDef.SubtitleTimings.Length)
                        {
                            subtitleDuration = _activeDef.SubtitleTimings[_currentSubtitleIndex + 1] -
                                               _activeDef.SubtitleTimings[_currentSubtitleIndex];
                        }
                        CinematicUIRegistry.UpdateSubtitle(subtitleText, subtitleDuration);
                        _currentSubtitleIndex++;
                    }
                }

                // Natural end: elapsed >= duration
                if (state.Duration > 0f && state.ElapsedTime >= state.Duration)
                {
                    state.IsPlaying = false;
                    StopPlayback(stateEntity);
                }

                EntityManager.SetComponentData(stateEntity, state);
            }

            _wasPlaying = state.IsPlaying;
        }

        private void StartPlayback(ref CinematicState state, Entity stateEntity)
        {
            _lastCinematicId = state.CurrentCinematicId;
            _currentSubtitleIndex = 0;
            _activeDef = null;

            // Lookup definition — registry is on same entity as CinematicState (created by bootstrap)
            if (!EntityManager.HasComponent<CinematicRegistryManaged>(stateEntity)) return;
            var registry = EntityManager.GetComponentObject<CinematicRegistryManaged>(stateEntity);
            if (registry == null || !registry.IsInitialized) return;

            if (!registry.Definitions.TryGetValue(state.CurrentCinematicId, out var def))
            {
                Debug.LogWarning($"[Cinematic] CinematicId {state.CurrentCinematicId} not found in registry");
                return;
            }

            _activeDef = def;

            // Set duration from definition if not specified by RPC
            if (state.Duration <= 0f)
            {
                if (def.TimelineAsset != null)
                    state.Duration = (float)def.TimelineAsset.duration;
                else
                    state.Duration = def.Duration;
                EntityManager.SetComponentData(stateEntity, state);
            }

            // Start PlayableDirector if TimelineAsset exists
            if (def.TimelineAsset != null && state.CinematicType != CinematicType.TextOverlay)
            {
                var directorGO = new GameObject($"CinematicDirector_{def.CinematicId}");
                var director = directorGO.AddComponent<PlayableDirector>();
                director.playableAsset = def.TimelineAsset;
                director.extrapolationMode = DirectorWrapMode.Hold;
                director.Play();
                registry.ActiveDirector = director;
            }

            // Instantiate camera rig for FullCinematic
            if (state.CinematicType == CinematicType.FullCinematic && def.CinematicCameraPrefab != null)
            {
                var cameraRig = Object.Instantiate(def.CinematicCameraPrefab);
                registry.CameraRigInstance = cameraRig;

                var cam = cameraRig.GetComponentInChildren<Camera>();
                if (cam != null)
                    registry.CinematicCamera = cam;
            }

            // Play voice line
            if (def.VoiceLineClip != null)
            {
                if (_voiceAudioSource == null)
                {
                    var audioGO = new GameObject("CinematicVoice");
                    Object.DontDestroyOnLoad(audioGO);
                    _voiceAudioSource = audioGO.AddComponent<AudioSource>();
                    _voiceAudioSource.spatialBlend = 0f; // 2D
                    _voiceAudioSource.playOnAwake = false;
                }
                _voiceAudioSource.clip = def.VoiceLineClip;
                _voiceAudioSource.Play();
            }

            // Trigger dialogue if specified
            if (def.DialogueTreeId > 0)
            {
                CinematicAnimEventQueue.Enqueue(new CinematicAnimEvent
                {
                    EventType = CinematicAnimEventType.TriggerDialogue,
                    IntParam = def.DialogueTreeId
                });
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Cinematic] Started: '{def.Name}' (Id={def.CinematicId}, Type={state.CinematicType}, Duration={state.Duration:F1}s)");
#endif
        }

        private void StopPlayback(Entity stateEntity)
        {
            // Registry is on same entity as CinematicState (created by bootstrap)
            var registry = EntityManager.HasComponent<CinematicRegistryManaged>(stateEntity)
                ? EntityManager.GetComponentObject<CinematicRegistryManaged>(stateEntity)
                : null;

            if (registry != null)
            {
                // Stop and destroy PlayableDirector
                if (registry.ActiveDirector != null)
                {
                    registry.ActiveDirector.Stop();
                    Object.Destroy(registry.ActiveDirector.gameObject);
                    registry.ActiveDirector = null;
                }

                // Destroy camera rig
                if (registry.CameraRigInstance != null)
                {
                    Object.Destroy(registry.CameraRigInstance);
                    registry.CameraRigInstance = null;
                    registry.CinematicCamera = null;
                }
            }

            // Stop voice audio
            if (_voiceAudioSource != null && _voiceAudioSource.isPlaying)
                _voiceAudioSource.Stop();

            // Clear event queue
            CinematicAnimEventQueue.Clear();

            _activeDef = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Cinematic] Ended: CinematicId={_lastCinematicId}");
#endif
        }

        protected override void OnDestroy()
        {
            if (_voiceAudioSource != null)
            {
                Object.Destroy(_voiceAudioSource.gameObject);
                _voiceAudioSource = null;
            }
        }
    }
}
