using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Server-side system tracking cinematic elapsed time.
    /// Broadcasts CinematicEndRpc when timeout is reached (natural end).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CinematicTriggerSystem))]
    public partial class CinematicEndSystem : SystemBase
    {
        private bool _isTracking;
        private int _activeCinematicId;
        private float _elapsed;
        private float _duration;

        protected override void OnCreate()
        {
            // Only run when there are triggers in the world
        }

        protected override void OnUpdate()
        {
            if (!_isTracking) return;

            _elapsed += SystemAPI.Time.DeltaTime;

            if (_elapsed >= _duration)
            {
                // Natural end
                var endEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(endEntity, new CinematicEndRpc
                {
                    CinematicId = _activeCinematicId,
                    WasSkipped = false
                });
                EntityManager.AddComponent<SendRpcCommandRequest>(endEntity);

                _isTracking = false;

                // Notify trigger system
                var triggerSystem = World.GetExistingSystemManaged<CinematicTriggerSystem>();
                triggerSystem?.NotifyCinematicEnded();

                // Notify skip system
                var skipSystem = World.GetExistingSystemManaged<CinematicSkipSystem>();
                skipSystem?.StopTracking();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Cinematic] Server: Natural end for CinematicId={_activeCinematicId} at {_elapsed:F1}s");
#endif
            }
        }

        /// <summary>Called by CinematicTriggerSystem when a cinematic starts.</summary>
        public void BeginTracking(int cinematicId, float duration)
        {
            _isTracking = true;
            _activeCinematicId = cinematicId;
            _elapsed = 0f;
            _duration = duration;
        }

        /// <summary>Called when cinematic ends early (skip).</summary>
        public void StopTracking()
        {
            _isTracking = false;
        }
    }
}
