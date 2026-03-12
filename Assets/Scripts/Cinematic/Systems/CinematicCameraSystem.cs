using Unity.Entities;
using UnityEngine;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Camera blend system for FullCinematic type.
    /// Smoothly blends from gameplay camera to cinematic camera and back.
    /// Follows DialogueCameraSystem handoff pattern.
    /// Only affects FullCinematic type — InWorldEvent/TextOverlay leave gameplay camera active.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class CinematicCameraSystem : SystemBase
    {
        private EntityQuery _stateQuery;
        private EntityQuery _configQuery;

        private bool _wasPlaying;
        private bool _isBlendingIn;
        private bool _isBlendingOut;
        private float _blendTimer;
        private float _blendDuration;

        // Cached gameplay camera state
        private Camera _gameplayCamera;
        private Vector3 _cachedCameraPos;
        private Quaternion _cachedCameraRot;

        protected override void OnCreate()
        {
            _stateQuery = GetEntityQuery(ComponentType.ReadWrite<CinematicState>());
            _configQuery = GetEntityQuery(ComponentType.ReadOnly<CinematicConfigSingleton>());
            RequireForUpdate(_stateQuery);
        }

        protected override void OnUpdate()
        {
            var stateEntity = _stateQuery.GetSingletonEntity();
            var state = EntityManager.GetComponentData<CinematicState>(stateEntity);

            // Only FullCinematic uses camera blend
            if (state.CinematicType != CinematicType.FullCinematic && !_isBlendingOut)
            {
                _wasPlaying = state.IsPlaying;
                return;
            }

            float dt = SystemAPI.Time.DeltaTime;

            // Transition: start blend in
            if (state.IsPlaying && !_wasPlaying && state.CinematicType == CinematicType.FullCinematic)
            {
                var config = _configQuery.CalculateEntityCount() > 0
                    ? _configQuery.GetSingleton<CinematicConfigSingleton>()
                    : new CinematicConfigSingleton { BlendInDuration = 0.5f, BlendOutDuration = 0.5f };

                _gameplayCamera = Camera.main;
                if (_gameplayCamera != null)
                {
                    _cachedCameraPos = _gameplayCamera.transform.position;
                    _cachedCameraRot = _gameplayCamera.transform.rotation;
                }

                _isBlendingIn = true;
                _isBlendingOut = false;
                _blendTimer = 0f;
                _blendDuration = config.BlendInDuration;
            }

            // Transition: start blend out
            if (!state.IsPlaying && _wasPlaying)
            {
                var config = _configQuery.CalculateEntityCount() > 0
                    ? _configQuery.GetSingleton<CinematicConfigSingleton>()
                    : new CinematicConfigSingleton { BlendOutDuration = 0.5f };

                _isBlendingIn = false;
                _isBlendingOut = true;
                _blendTimer = 0f;
                _blendDuration = config.BlendOutDuration;
            }

            // Blend in: gameplay -> cinematic
            if (_isBlendingIn)
            {
                _blendTimer += dt;
                float t = Mathf.Clamp01(_blendTimer / Mathf.Max(_blendDuration, 0.01f));

                state.BlendProgress = t;
                EntityManager.SetComponentData(stateEntity, state);

                var cinematicCam = GetCinematicCamera(stateEntity);
                if (_gameplayCamera != null && cinematicCam != null)
                {
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);
                    _gameplayCamera.transform.position = Vector3.Lerp(
                        _cachedCameraPos, cinematicCam.transform.position, smoothT);
                    _gameplayCamera.transform.rotation = Quaternion.Slerp(
                        _cachedCameraRot, cinematicCam.transform.rotation, smoothT);
                }

                if (t >= 1f)
                    _isBlendingIn = false;
            }

            // Blend out: cinematic -> gameplay
            if (_isBlendingOut)
            {
                _blendTimer += dt;
                float t = Mathf.Clamp01(_blendTimer / Mathf.Max(_blendDuration, 0.01f));

                state.BlendProgress = 1f - t;
                EntityManager.SetComponentData(stateEntity, state);

                if (t >= 1f)
                {
                    _isBlendingOut = false;
                    state.BlendProgress = 0f;
                    EntityManager.SetComponentData(stateEntity, state);
                }
            }

            _wasPlaying = state.IsPlaying;
        }

        private Camera GetCinematicCamera(Entity stateEntity)
        {
            // Registry is on same entity as CinematicState (created by bootstrap)
            if (!EntityManager.HasComponent<CinematicRegistryManaged>(stateEntity)) return null;
            var registry = EntityManager.GetComponentObject<CinematicRegistryManaged>(stateEntity);
            return registry?.CinematicCamera;
        }
    }
}
