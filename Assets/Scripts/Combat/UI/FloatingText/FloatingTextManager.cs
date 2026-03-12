using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

namespace DIG.Combat.UI.FloatingText
{
    /// <summary>
    /// EPIC 15.9: Manager for pooled floating combat text.
    /// Implements IFloatingTextProvider for combat UI integration.
    /// </summary>
    public class FloatingTextManager : MonoBehaviour, IFloatingTextProvider
    {
        public static FloatingTextManager Instance { get; private set; }
        
        [Header("Prefab")]
        [Tooltip("Floating text element prefab with TextMeshPro")]
        [SerializeField] private FloatingTextElement textPrefab;
        
        [Header("Pool Settings")]
        [SerializeField] private int initialPoolSize = 30;
        [SerializeField] private int maxPoolSize = 50;
        
        [Header("Style Configuration")]
        [SerializeField] private FloatingTextStyleConfig styleConfig;
        
        [Header("Spawn Settings")]
        [SerializeField] private float minSpawnDistance = 0.5f; // Prevent overlap
        [SerializeField] private float spamPreventionTime = 0.1f; // Same text cooldown
        
        private Queue<FloatingTextElement> _pool = new();
        private List<FloatingTextElement> _active = new();
        private Transform _poolParent;
        
        // Spam prevention
        private Dictionary<string, float> _lastSpawnTimes = new();
        private float3 _lastSpawnPosition;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            InitializePool();
        }
        
        private void OnEnable()
        {
            CombatUIRegistry.RegisterFloatingText(this);
        }
        
        private void OnDisable()
        {
            CombatUIRegistry.UnregisterFloatingText(this);
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
        
        private void InitializePool()
        {
            _poolParent = new GameObject("FloatingTextPool").transform;
            _poolParent.SetParent(transform);
            _poolParent.localPosition = Vector3.zero;
            
            if (textPrefab == null)
            {
                Debug.LogWarning("[FloatingTextManager] No text prefab assigned!");
                return;
            }
            
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreatePooledElement();
            }
        }
        
        private FloatingTextElement CreatePooledElement()
        {
            var element = Instantiate(textPrefab, _poolParent);
            element.gameObject.SetActive(false);
            element.OnComplete += ReturnToPool;
            _pool.Enqueue(element);
            return element;
        }
        
        public void ShowText(string text, float3 worldPosition, FloatingTextStyle style)
        {
            if (textPrefab == null || styleConfig == null) return;
            
            // Spam prevention for same text
            if (_lastSpawnTimes.TryGetValue(text, out float lastTime))
            {
                if (Time.time - lastTime < spamPreventionTime)
                    return;
            }
            _lastSpawnTimes[text] = Time.time;
            
            // Offset if too close to last spawn
            Vector3 position = (Vector3)worldPosition;
            if (math.distance(worldPosition, _lastSpawnPosition) < minSpawnDistance)
            {
                position += new Vector3(
                    UnityEngine.Random.Range(-0.3f, 0.3f),
                    0.3f,
                    UnityEngine.Random.Range(-0.3f, 0.3f)
                );
            }
            _lastSpawnPosition = worldPosition;
            
            var element = GetFromPool();
            if (element == null) return;
            
            var styleData = styleConfig.GetStyle(style);
            element.Initialize(text, position, styleData);
            element.gameObject.SetActive(true);
            _active.Add(element);
        }
        
        public void ShowStatusApplied(StatusEffectType status, float3 worldPosition)
        {
            if (styleConfig == null) return;
            
            string text = styleConfig.GetStatusEffectName(status);
            Color color = styleConfig.GetStatusEffectColor(status);
            
            var element = GetFromPool();
            if (element == null) return;
            
            Vector3 position = (Vector3)worldPosition + Vector3.up * 0.5f;
            element.Initialize(text, position, color, 26f);
            element.gameObject.SetActive(true);
            _active.Add(element);
        }
        
        public void ShowCombatVerb(CombatVerb verb, float3 worldPosition)
        {
            if (styleConfig == null) return;
            
            string text = styleConfig.GetCombatVerbText(verb);
            
            // Determine style based on verb type
            FloatingTextStyle style = verb switch
            {
                CombatVerb.Immune or CombatVerb.Resist => FloatingTextStyle.Failure,
                CombatVerb.Parry or CombatVerb.Counter or CombatVerb.PerfectBlock or 
                CombatVerb.Finisher or CombatVerb.Riposte => FloatingTextStyle.Success,
                _ => FloatingTextStyle.Important
            };
            
            ShowText(text, worldPosition, style);
        }
        
        /// <summary>
        /// Show combo count text.
        /// </summary>
        public void ShowCombo(int comboCount, float3 worldPosition)
        {
            ShowText($"COMBO x{comboCount}!", worldPosition, FloatingTextStyle.Important);
        }
        
        /// <summary>
        /// Show XP gain.
        /// </summary>
        public void ShowXPGain(int amount, float3 worldPosition)
        {
            var element = GetFromPool();
            if (element == null) return;
            
            Vector3 position = (Vector3)worldPosition + Vector3.up * 0.3f;
            element.Initialize($"+{amount} XP", position, new Color(0.8f, 0.6f, 1f), 20f);
            element.gameObject.SetActive(true);
            _active.Add(element);
        }
        
        private FloatingTextElement GetFromPool()
        {
            if (_pool.Count > 0)
                return _pool.Dequeue();
            
            // Pool exhausted - create new if under max
            if (_active.Count < maxPoolSize)
                return CreatePooledElement();
            
            // Steal oldest active
            if (_active.Count > 0)
            {
                var oldest = _active[0];
                _active.RemoveAt(0);
                oldest.ForceComplete();
                return oldest;
            }
            
            return null;
        }
        
        private void ReturnToPool(FloatingTextElement element)
        {
            _active.Remove(element);
            element.gameObject.SetActive(false);
            _pool.Enqueue(element);
        }
        
        /// <summary>
        /// Clear all active floating text.
        /// </summary>
        public void ClearAll()
        {
            foreach (var element in _active.ToArray())
            {
                element.ForceComplete();
            }
            _active.Clear();
        }
        
        // Cleanup spam prevention dictionary periodically
        private void Update()
        {
            // Every 5 seconds, clean old entries
            if (Time.frameCount % 300 == 0)
            {
                var keysToRemove = new List<string>();
                foreach (var kvp in _lastSpawnTimes)
                {
                    if (Time.time - kvp.Value > 5f)
                        keysToRemove.Add(kvp.Key);
                }
                foreach (var key in keysToRemove)
                    _lastSpawnTimes.Remove(key);
            }
        }
    }
}
