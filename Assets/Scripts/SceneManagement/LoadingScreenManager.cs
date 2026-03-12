using System.Collections;
using DIG.SceneManagement.UI;
using UnityEngine;

namespace DIG.SceneManagement
{
    /// <summary>
    /// EPIC 18.6: Manages the loading screen lifecycle — show, hide, progress,
    /// tip rotation, and loading music.
    /// Singleton MonoBehaviour, DontDestroyOnLoad.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public class LoadingScreenManager : MonoBehaviour
    {
        public static LoadingScreenManager Instance { get; private set; }

        [SerializeField] private LoadingScreenView _view;

        private LoadingScreenProfileSO _activeProfile;
        private Coroutine _tipRotationCoroutine;
        private float _showStartTime;
        private AudioSource _loadingMusicSource;
        private float _lastProgress = -1f;
        private float _lastProgressTime;
        private const float ProgressThrottleMinDelta = 0.01f;
        private const float ProgressThrottleMinInterval = 0.05f;

        public bool IsVisible => _view != null && _view.IsVisible;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_view != null)
                _view.HideImmediate();

            // Pre-allocate loading music source
            var audioGO = new GameObject("LoadingMusic");
            audioGO.transform.SetParent(transform, false);
            _loadingMusicSource = audioGO.AddComponent<AudioSource>();
            _loadingMusicSource.playOnAwake = false;
            _loadingMusicSource.spatialBlend = 0f;
            _loadingMusicSource.loop = true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Show the loading screen with the given profile. Returns when fade-in completes.
        /// </summary>
        public Coroutine Show(LoadingScreenProfileSO profile)
        {
            _activeProfile = profile;
            _showStartTime = Time.unscaledTime;
            _lastProgress = -1f;

            if (_view != null && _activeProfile != null)
            {
                // Background
                if (_activeProfile.BackgroundSprites != null && _activeProfile.BackgroundSprites.Length > 0)
                    _view.SetBackground(_activeProfile.BackgroundSprites[
                        Random.Range(0, _activeProfile.BackgroundSprites.Length)]);

                // Initial tip
                if (_activeProfile.Tips != null && _activeProfile.Tips.Length > 0)
                    _view.SetTip(_activeProfile.Tips[Random.Range(0, _activeProfile.Tips.Length)]);

                // Progress style
                _view.SetProgress(0f, _activeProfile.ProgressBarStyle);

                // Loading music
                StartLoadingMusic(_activeProfile.MusicClip);

                // Tip rotation (every 5 seconds)
                if (_tipRotationCoroutine != null)
                    StopCoroutine(_tipRotationCoroutine);
                _tipRotationCoroutine = StartCoroutine(RotateTips(_activeProfile));
            }

            float fadeIn = _activeProfile != null ? _activeProfile.FadeInDuration : 0.3f;
            return StartCoroutine(_view != null ? _view.FadeIn(fadeIn) : EmptyCoroutine());
        }

        /// <summary>
        /// Hide the loading screen. Returns when fade-out completes.
        /// </summary>
        public Coroutine Hide()
        {
            if (_tipRotationCoroutine != null)
            {
                StopCoroutine(_tipRotationCoroutine);
                _tipRotationCoroutine = null;
            }

            StopLoadingMusic();

            float fadeOut = _activeProfile != null ? _activeProfile.FadeOutDuration : 0.3f;
            return StartCoroutine(_view != null ? _view.FadeOut(fadeOut) : EmptyCoroutine());
        }

        public void UpdateProgress(float progress)
        {
            if (_view == null || _activeProfile == null) return;

            float now = Time.unscaledTime;
            float delta = Mathf.Abs(progress - _lastProgress);
            bool shouldUpdate = _lastProgress < 0f ||
                delta >= ProgressThrottleMinDelta ||
                (now - _lastProgressTime) >= ProgressThrottleMinInterval;

            if (shouldUpdate)
            {
                _lastProgress = progress;
                _lastProgressTime = now;
                _view.SetProgress(progress, _activeProfile.ProgressBarStyle);
            }
        }

        public void SetPhaseText(string text)
        {
            if (_view != null)
                _view.SetPhaseText(text);
        }

        /// <summary>
        /// Returns true if MinDisplaySeconds has elapsed since Show() was called.
        /// </summary>
        public bool HasMetMinDisplayTime()
        {
            float min = _activeProfile != null ? _activeProfile.MinDisplaySeconds : 0f;
            return (Time.unscaledTime - _showStartTime) >= min;
        }

        /// <summary>
        /// Coroutine that waits until MinDisplaySeconds has been met.
        /// </summary>
        public IEnumerator WaitForMinDisplayTime()
        {
            while (!HasMetMinDisplayTime())
                yield return null;
        }

        private IEnumerator RotateTips(LoadingScreenProfileSO profile)
        {
            if (profile.Tips == null || profile.Tips.Length <= 1)
                yield break;

            while (true)
            {
                yield return new WaitForSecondsRealtime(5f);
                _view.SetTip(profile.Tips[Random.Range(0, profile.Tips.Length)]);
            }
        }

        private void StartLoadingMusic(AudioClip clip)
        {
            if (clip == null || _loadingMusicSource == null) return;

            _loadingMusicSource.clip = clip;
            _loadingMusicSource.volume = 0.5f;
            _loadingMusicSource.Play();
        }

        private void StopLoadingMusic()
        {
            if (_loadingMusicSource != null && _loadingMusicSource.isPlaying)
            {
                _loadingMusicSource.Stop();
                _loadingMusicSource.clip = null;
            }
        }

        private static IEnumerator EmptyCoroutine()
        {
            yield break;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }
    }
}
