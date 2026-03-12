using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;
using System.Collections.Generic;

namespace DIG.Combat.UI.Views
{
    /// <summary>
    /// EPIC 15.9: Directional damage indicator showing damage source direction.
    /// Uses radial indicators around screen edge.
    /// </summary>
    public class DirectionalDamageIndicatorView : MonoBehaviour
    {
        [System.Serializable]
        public class DamageIndicator
        {
            public Image Image;
            public float Angle; // Radians
            public float Intensity;
            public float Timer;
        }
        
        [Header("Indicator Prefab")]
        [SerializeField] private Image indicatorPrefab;
        
        [Header("Settings")]
        [Tooltip("Number of indicator segments around the screen")]
        [SerializeField] private int indicatorCount = 8;
        
        [Tooltip("Distance from center to place indicators")]
        [SerializeField] private float indicatorRadius = 300f;
        
        [Tooltip("Duration of indicator visibility")]
        [SerializeField] private float indicatorDuration = 1.5f;
        
        [Tooltip("Fade out speed")]
        [SerializeField] private float fadeSpeed = 2f;
        
        [Header("Intensity Settings")]
        [Tooltip("Damage amount for maximum intensity")]
        [SerializeField] private float maxDamageForIntensity = 50f;
        
        [Tooltip("Indicator color")]
        [SerializeField] private Color indicatorColor = new Color(1f, 0f, 0f, 0.8f);
        
        [Header("References")]
        [SerializeField] private RectTransform indicatorContainer;
        
        private List<DamageIndicator> _indicators = new();
        private Camera _mainCamera;
        private Transform _playerTransform;
        
        private void Awake()
        {
            _mainCamera = Camera.main;
            CreateIndicators();
        }
        
        private void Start()
        {
            // Try to find player
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }
        
        private void CreateIndicators()
        {
            if (indicatorPrefab == null || indicatorContainer == null)
            {
                Debug.LogWarning("[DirectionalDamageIndicator] Missing prefab or container!");
                return;
            }
            
            float angleStep = (2f * Mathf.PI) / indicatorCount;
            
            for (int i = 0; i < indicatorCount; i++)
            {
                float angle = i * angleStep;
                
                var indicator = Instantiate(indicatorPrefab, indicatorContainer);
                indicator.gameObject.SetActive(true);
                
                // Position around circle
                float x = Mathf.Sin(angle) * indicatorRadius;
                float y = Mathf.Cos(angle) * indicatorRadius;
                indicator.rectTransform.anchoredPosition = new Vector2(x, y);
                
                // Rotate to point inward
                indicator.rectTransform.rotation = Quaternion.Euler(0, 0, -angle * Mathf.Rad2Deg);
                
                // Start invisible
                indicator.color = new Color(indicatorColor.r, indicatorColor.g, indicatorColor.b, 0);
                
                _indicators.Add(new DamageIndicator
                {
                    Image = indicator,
                    Angle = angle,
                    Intensity = 0,
                    Timer = 0
                });
            }
        }
        
        private void Update()
        {
            // Update fade for all indicators
            for (int i = 0; i < _indicators.Count; i++)
            {
                var indicator = _indicators[i];
                
                if (indicator.Timer > 0)
                {
                    indicator.Timer -= Time.deltaTime;
                    
                    // Calculate alpha based on timer and intensity
                    float timerAlpha = Mathf.Clamp01(indicator.Timer / indicatorDuration);
                    float alpha = timerAlpha * indicator.Intensity * indicatorColor.a;
                    
                    indicator.Image.color = new Color(
                        indicatorColor.r,
                        indicatorColor.g,
                        indicatorColor.b,
                        alpha
                    );
                }
                else if (indicator.Image.color.a > 0)
                {
                    // Fade out
                    var color = indicator.Image.color;
                    color.a = Mathf.Max(0, color.a - Time.deltaTime * fadeSpeed);
                    indicator.Image.color = color;
                }
            }
        }
        
        /// <summary>
        /// Show damage indicator from a world position.
        /// </summary>
        public void ShowDamage(float3 damageSourcePosition, float damageAmount)
        {
            if (_playerTransform == null || _mainCamera == null)
                return;
            
            // Calculate direction from player to damage source
            Vector3 playerPos = _playerTransform.position;
            Vector3 sourcePos = new Vector3(damageSourcePosition.x, playerPos.y, damageSourcePosition.z);
            Vector3 direction = (sourcePos - playerPos).normalized;
            
            // Get player's forward direction (ignore Y)
            Vector3 playerForward = _playerTransform.forward;
            playerForward.y = 0;
            playerForward.Normalize();
            
            // Calculate angle
            float angle = Mathf.Atan2(
                Vector3.Dot(direction, _playerTransform.right),
                Vector3.Dot(direction, playerForward)
            );
            
            // Find closest indicator
            int bestIndex = 0;
            float bestDiff = float.MaxValue;
            
            for (int i = 0; i < _indicators.Count; i++)
            {
                float diff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, _indicators[i].Angle * Mathf.Rad2Deg));
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestIndex = i;
                }
            }
            
            // Calculate intensity based on damage
            float intensity = Mathf.Clamp01(damageAmount / maxDamageForIntensity);
            
            // Activate indicator
            var indicator = _indicators[bestIndex];
            indicator.Intensity = Mathf.Max(indicator.Intensity, intensity);
            indicator.Timer = indicatorDuration;
            
            // Also activate adjacent indicators with reduced intensity
            int prevIndex = (bestIndex - 1 + _indicators.Count) % _indicators.Count;
            int nextIndex = (bestIndex + 1) % _indicators.Count;
            
            _indicators[prevIndex].Intensity = Mathf.Max(_indicators[prevIndex].Intensity, intensity * 0.3f);
            _indicators[prevIndex].Timer = Mathf.Max(_indicators[prevIndex].Timer, indicatorDuration * 0.5f);
            
            _indicators[nextIndex].Intensity = Mathf.Max(_indicators[nextIndex].Intensity, intensity * 0.3f);
            _indicators[nextIndex].Timer = Mathf.Max(_indicators[nextIndex].Timer, indicatorDuration * 0.5f);
        }
        
        /// <summary>
        /// Show damage indicator from a specific direction angle.
        /// </summary>
        public void ShowDamageFromAngle(float angleRadians, float damageAmount)
        {
            int bestIndex = 0;
            float bestDiff = float.MaxValue;
            
            for (int i = 0; i < _indicators.Count; i++)
            {
                float diff = Mathf.Abs(Mathf.DeltaAngle(angleRadians * Mathf.Rad2Deg, _indicators[i].Angle * Mathf.Rad2Deg));
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestIndex = i;
                }
            }
            
            float intensity = Mathf.Clamp01(damageAmount / maxDamageForIntensity);
            
            var indicator = _indicators[bestIndex];
            indicator.Intensity = Mathf.Max(indicator.Intensity, intensity);
            indicator.Timer = indicatorDuration;
        }
        
        /// <summary>
        /// Set player transform reference.
        /// </summary>
        public void SetPlayerTransform(Transform player)
        {
            _playerTransform = player;
        }
        
        /// <summary>
        /// Clear all indicators.
        /// </summary>
        public void ClearAll()
        {
            foreach (var indicator in _indicators)
            {
                indicator.Timer = 0;
                indicator.Intensity = 0;
                indicator.Image.color = new Color(indicatorColor.r, indicatorColor.g, indicatorColor.b, 0);
            }
        }
    }
}
