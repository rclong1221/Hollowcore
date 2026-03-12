using UnityEngine;

namespace Audio.Systems
{
    /// <summary>
    /// Simple tester component to fire footstep playback through the AudioManager for QA.
    /// Press Space to play a single footstep. Use the inspector to adjust parameters.
    /// </summary>
    public class FootstepTester : MonoBehaviour
    {
        public int MaterialId = 0;
        [Tooltip("0=walk,1=crouch,2=idle,3=run")] public int Stance = 0;
        public Vector3 Offset = Vector3.zero;
        public float AutoRepeatInterval = 0f; // 0 = manual only

        Audio.Systems.AudioManager _mgr;
        float _timer;

        void Start()
        {
            _mgr = Object.FindAnyObjectByType<Audio.Systems.AudioManager>();
            if (_mgr == null) Debug.LogWarning("FootstepTester: No AudioManager found in scene.", this);
        }

        void Update()
        {
            if (_mgr == null)
            {
                _mgr = Object.FindAnyObjectByType<Audio.Systems.AudioManager>();
            }

            if (_mgr == null) return;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                _mgr.PlayFootstep(MaterialId, transform.position + Offset, Stance);
            }

            if (AutoRepeatInterval > 0f)
            {
                _timer += Time.deltaTime;
                if (_timer >= AutoRepeatInterval)
                {
                    _timer = 0f;
                    _mgr.PlayFootstep(MaterialId, transform.position + Offset, Stance);
                }
            }
        }
    }
}
