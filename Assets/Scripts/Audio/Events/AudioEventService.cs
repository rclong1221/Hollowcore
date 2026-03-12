using UnityEngine;

namespace Audio.Events
{
    /// <summary>
    /// EPIC 18.8: Central service for playing AudioEventSOs, managing the MusicController,
    /// and coordinating AmbientZoneManager. Singleton, DontDestroyOnLoad.
    /// Provides the simple one-call API that designers expect.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class AudioEventService : MonoBehaviour
    {
        public static AudioEventService Instance { get; private set; }

        [Header("Sub-systems (auto-created if null)")]
        [SerializeField] private Audio.Music.MusicController _musicController;
        [SerializeField] private Audio.Ambient.AmbientZoneManager _ambientZoneManager;

        private AudioEventPlayer _player;

        public AudioEventPlayer Player => _player;
        public Audio.Music.MusicController Music => _musicController;
        public Audio.Ambient.AmbientZoneManager Ambient => _ambientZoneManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _player = new AudioEventPlayer();

            EnsureSubsystems();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                _player?.ReleaseAll();
                Instance = null;
            }
        }

        private void Update()
        {
            _player.Tick(Time.unscaledDeltaTime);
        }

        // ---- Public API (delegates to AudioEventPlayer) ----

        public AudioEventHandle Play(AudioEventSO evt, Vector3 position)
        {
            return _player.Play(evt, position);
        }

        public AudioEventHandle Play2D(AudioEventSO evt)
        {
            return _player.Play2D(evt);
        }

        public AudioEventHandle PlayAttached(AudioEventSO evt, Transform parent)
        {
            return _player.PlayAttached(evt, parent);
        }

        public void Stop(AudioEventHandle handle, float fadeOut = 0f)
        {
            _player.Stop(handle, fadeOut);
        }

        public bool IsPlaying(AudioEventHandle handle)
        {
            return _player.IsPlaying(handle);
        }

        public void StopAll()
        {
            _player.ReleaseAll();
        }

        private void EnsureSubsystems()
        {
            if (_musicController == null)
            {
                _musicController = GetComponentInChildren<Audio.Music.MusicController>();
                if (_musicController == null)
                {
                    var go = new GameObject("MusicController");
                    go.transform.SetParent(transform, false);
                    _musicController = go.AddComponent<Audio.Music.MusicController>();
                }
            }

            if (_ambientZoneManager == null)
            {
                _ambientZoneManager = GetComponentInChildren<Audio.Ambient.AmbientZoneManager>();
                if (_ambientZoneManager == null)
                {
                    var go = new GameObject("AmbientZoneManager");
                    go.transform.SetParent(transform, false);
                    _ambientZoneManager = go.AddComponent<Audio.Ambient.AmbientZoneManager>();
                }
            }
        }
    }
}
