using Unity.Entities;
using UnityEngine;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Managed bridge system that pushes CinematicState to
    /// CinematicUIRegistry -> ICinematicUIProvider.
    /// Controls letterbox bars, skip prompt, subtitle text, HUD fade, progress.
    /// Follows CombatUIBridgeSystem / AchievementUIBridgeSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CinematicPlaybackSystem))]
    public partial class CinematicUIBridgeSystem : SystemBase
    {
        private EntityQuery _stateQuery;
        private EntityQuery _configQuery;
        private bool _wasPlaying;
        private int _noProviderWarnFrame;

        protected override void OnCreate()
        {
            _stateQuery = GetEntityQuery(ComponentType.ReadOnly<CinematicState>());
            _configQuery = GetEntityQuery(ComponentType.ReadOnly<CinematicConfigSingleton>());
            RequireForUpdate(_stateQuery);
        }

        protected override void OnUpdate()
        {
            if (!CinematicUIRegistry.HasProvider)
            {
                _noProviderWarnFrame++;
                if (_noProviderWarnFrame == 120)
                    Debug.LogWarning("[Cinematic] No ICinematicUIProvider registered after 120 frames. Cinematic UI will not display.");
                return;
            }

            var state = _stateQuery.GetSingleton<CinematicState>();
            var config = _configQuery.CalculateEntityCount() > 0
                ? _configQuery.GetSingleton<CinematicConfigSingleton>()
                : new CinematicConfigSingleton
                {
                    HUDFadeDuration = 0.3f,
                    LetterboxHeight = 0.12f,
                    BlendInDuration = 0.5f,
                    BlendOutDuration = 0.5f
                };

            // Transition: start
            if (state.IsPlaying && !_wasPlaying)
            {
                CinematicUIRegistry.OnCinematicStart(state.CurrentCinematicId, state.CinematicType);

                switch (state.CinematicType)
                {
                    case CinematicType.FullCinematic:
                        CinematicUIRegistry.SetHUDVisible(false, config.HUDFadeDuration);
                        CinematicUIRegistry.SetLetterbox(config.LetterboxHeight, config.BlendInDuration);
                        break;
                    case CinematicType.TextOverlay:
                        // Partial HUD hide (crosshair + action bar hidden, minimap stays)
                        CinematicUIRegistry.SetHUDVisible(false, config.HUDFadeDuration);
                        break;
                    case CinematicType.InWorldEvent:
                        // No HUD change
                        break;
                }
            }

            // Transition: end
            if (!state.IsPlaying && _wasPlaying)
            {
                CinematicUIRegistry.OnCinematicEnd(state.CurrentCinematicId, false);
                CinematicUIRegistry.SetHUDVisible(true, config.HUDFadeDuration);
                CinematicUIRegistry.SetLetterbox(0f, config.BlendOutDuration);
                CinematicUIRegistry.UpdateSkipPrompt(false, 0, 0);
                CinematicUIRegistry.UpdateSubtitle("", 0f);
            }

            // During playback
            if (state.IsPlaying)
            {
                // Update skip prompt
                CinematicUIRegistry.UpdateSkipPrompt(
                    state.CanSkip,
                    state.SkipVotesReceived,
                    state.TotalPlayersInScene);

                // Update progress
                float progress = state.Duration > 0f
                    ? Mathf.Clamp01(state.ElapsedTime / state.Duration)
                    : 0f;
                CinematicUIRegistry.UpdateProgress(progress);
            }

            _wasPlaying = state.IsPlaying;
        }
    }
}
