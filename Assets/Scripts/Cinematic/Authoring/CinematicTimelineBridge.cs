using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: MonoBehaviour placed on GameObjects with PlayableDirector.
    /// Converts Timeline Signal emissions to CinematicAnimEvent structs
    /// and enqueues them to CinematicAnimEventQueue for ECS consumers.
    /// </summary>
    [AddComponentMenu("DIG/Cinematic/Timeline Bridge")]
    [RequireComponent(typeof(PlayableDirector))]
    public class CinematicTimelineBridge : MonoBehaviour
    {
        private PlayableDirector _director;

        private void Awake()
        {
            _director = GetComponent<PlayableDirector>();
        }

        /// <summary>
        /// Called by SignalReceiver when a CinematicAnimSignal fires.
        /// Drives NPC entity animation state.
        /// </summary>
        public void OnAnimationSignal(int targetId, int animationHash)
        {
            CinematicAnimEventQueue.Enqueue(new CinematicAnimEvent
            {
                EventType = CinematicAnimEventType.PlayAnimation,
                TargetId = targetId,
                IntParam = animationHash
            });
        }

        /// <summary>
        /// Called by SignalReceiver when a CinematicVFXSignal fires.
        /// Spawns VFX at marker position.
        /// </summary>
        public void OnVFXSignal(int vfxTypeId, Vector3 position)
        {
            CinematicAnimEventQueue.Enqueue(new CinematicAnimEvent
            {
                EventType = CinematicAnimEventType.SpawnVFX,
                TargetId = vfxTypeId,
                Position = position
            });
        }

        /// <summary>
        /// Called by SignalReceiver when a CinematicSoundSignal fires.
        /// Plays sound effect at position.
        /// </summary>
        public void OnSoundSignal(int soundId, Vector3 position, float volume)
        {
            CinematicAnimEventQueue.Enqueue(new CinematicAnimEvent
            {
                EventType = CinematicAnimEventType.PlaySound,
                TargetId = soundId,
                FloatParam = volume,
                Position = position
            });
        }

        /// <summary>
        /// Called by SignalReceiver when a CinematicDialogueSignal fires.
        /// Triggers dialogue node mid-cinematic.
        /// </summary>
        public void OnDialogueSignal(int dialogueTreeId)
        {
            CinematicAnimEventQueue.Enqueue(new CinematicAnimEvent
            {
                EventType = CinematicAnimEventType.TriggerDialogue,
                IntParam = dialogueTreeId
            });
        }

        /// <summary>
        /// Called by SignalReceiver when a CinematicFadeSignal fires.
        /// Screen fade to/from black.
        /// </summary>
        public void OnFadeSignal(float duration, bool fadeToBlack)
        {
            CinematicAnimEventQueue.Enqueue(new CinematicAnimEvent
            {
                EventType = CinematicAnimEventType.FadeToBlack,
                FloatParam = duration,
                IntParam = fadeToBlack ? 1 : 0
            });
        }
    }
}
