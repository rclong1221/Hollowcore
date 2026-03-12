using UnityEngine;

namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: Controls replay playback state: play, pause, seek, speed.
    /// Thin wrapper over ReplayPlayer that provides input-friendly API.
    /// </summary>
    public class ReplayTimelineController : MonoBehaviour
    {
        [Header("Speed Presets")]
        [SerializeField] private float[] _speedPresets = { 0.25f, 0.5f, 1f, 2f, 4f };
        private int _currentSpeedIndex = 2; // 1x

        public float[] SpeedPresets => _speedPresets;
        public int CurrentSpeedIndex => _currentSpeedIndex;
        public float CurrentSpeed => _speedPresets[_currentSpeedIndex];

        public void TogglePlayPause()
        {
            var player = ReplayPlayer.Instance;
            if (player == null) return;

            if (player.State == ReplayState.Playing)
                player.Pause();
            else if (player.State == ReplayState.Paused || player.State == ReplayState.Idle)
                player.Play();
        }

        public void CycleSpeedUp()
        {
            _currentSpeedIndex = Mathf.Min(_currentSpeedIndex + 1, _speedPresets.Length - 1);
            ReplayPlayer.Instance?.SetSpeed(_speedPresets[_currentSpeedIndex]);
        }

        public void CycleSpeedDown()
        {
            _currentSpeedIndex = Mathf.Max(_currentSpeedIndex - 1, 0);
            ReplayPlayer.Instance?.SetSpeed(_speedPresets[_currentSpeedIndex]);
        }

        public void SeekTo(float normalized)
        {
            ReplayPlayer.Instance?.Seek(normalized);
        }

        public void StepForward()
        {
            ReplayPlayer.Instance?.StepForward();
        }

        public void StepBackward()
        {
            ReplayPlayer.Instance?.StepBackward();
        }

        public void ResetSpeed()
        {
            _currentSpeedIndex = 2; // 1x
            ReplayPlayer.Instance?.SetSpeed(1f);
        }
    }
}
