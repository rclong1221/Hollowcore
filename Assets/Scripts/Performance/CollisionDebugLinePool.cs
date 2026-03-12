using UnityEngine;
using System.Collections.Generic;

namespace DIG.Performance
{
    /// <summary>
    /// Epic 7.7.2: Object pool for debug line visualization.
    /// 
    /// Eliminates per-frame Debug.DrawLine allocations in the editor by pooling
    /// LineRenderer GameObjects. This is especially important when visualizing
    /// many collision detection rays during development.
    /// 
    /// Usage:
    /// 1. Call Initialize() at start to create pool
    /// 2. Call GetLine() to get a pooled LineRenderer
    /// 3. Call ReleaseAll() at end of frame to return all to pool
    /// 4. Call Dispose() when done to destroy pooled objects
    /// </summary>
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
    public class CollisionDebugLinePool : MonoBehaviour
    {
        // Singleton instance for easy access
        private static CollisionDebugLinePool _instance;
        public static CollisionDebugLinePool Instance => _instance;
        
        [Header("Pool Configuration")]
        [SerializeField] private int _initialPoolSize = 64;
        [SerializeField] private int _maxPoolSize = 256;
        [SerializeField] private Material _debugLineMaterial;
        
        // Object pools
        private Queue<LineRenderer> _availableLines;
        private List<LineRenderer> _activeLines;
        private GameObject _poolContainer;
        
        // Statistics for profiling
        private int _totalCreated;
        private int _peakActive;
        
        /// <summary>
        /// Total LineRenderers created (including pool growth)
        /// </summary>
        public int TotalCreated => _totalCreated;
        
        /// <summary>
        /// Peak number of simultaneously active lines
        /// </summary>
        public int PeakActive => _peakActive;
        
        /// <summary>
        /// Current number of available lines in pool
        /// </summary>
        public int Available => _availableLines?.Count ?? 0;
        
        /// <summary>
        /// Current number of active lines in use
        /// </summary>
        public int Active => _activeLines?.Count ?? 0;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            Initialize();
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                Dispose();
                _instance = null;
            }
        }
        
        /// <summary>
        /// Initialize the object pool with initial capacity
        /// </summary>
        public void Initialize()
        {
            _availableLines = new Queue<LineRenderer>(_initialPoolSize);
            _activeLines = new List<LineRenderer>(_initialPoolSize);
            
            // Create container for pooled objects
            _poolContainer = new GameObject("[CollisionDebugLinePool]");
            _poolContainer.transform.SetParent(transform);
            _poolContainer.SetActive(false); // Hide inactive lines
            
            // Pre-allocate initial pool
            for (int i = 0; i < _initialPoolSize; i++)
            {
                var line = CreateLineRenderer();
                _availableLines.Enqueue(line);
            }
            
            UnityEngine.Debug.Log($"[CollisionDebugLinePool] Initialized with {_initialPoolSize} lines");
        }
        
        /// <summary>
        /// Get a LineRenderer from the pool
        /// </summary>
        public LineRenderer GetLine()
        {
            LineRenderer line;
            
            if (_availableLines.Count > 0)
            {
                line = _availableLines.Dequeue();
            }
            else if (_totalCreated < _maxPoolSize)
            {
                // Pool exhausted, grow it
                line = CreateLineRenderer();
                UnityEngine.Debug.LogWarning($"[CollisionDebugLinePool] Pool exhausted, growing to {_totalCreated}");
            }
            else
            {
                // At max capacity, can't provide more
                UnityEngine.Debug.LogWarning($"[CollisionDebugLinePool] Max capacity ({_maxPoolSize}) reached!");
                return null;
            }
            
            // Activate the line
            line.gameObject.SetActive(true);
            _activeLines.Add(line);
            
            // Track peak usage
            if (_activeLines.Count > _peakActive)
            {
                _peakActive = _activeLines.Count;
            }
            
            return line;
        }
        
        /// <summary>
        /// Draw a debug line using a pooled LineRenderer
        /// </summary>
        public void DrawLine(Vector3 start, Vector3 end, Color color, float width = 0.02f)
        {
            var line = GetLine();
            if (line == null) return;
            
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
        
        /// <summary>
        /// Draw a debug sphere using multiple pooled LineRenderers
        /// </summary>
        public void DrawWireSphere(Vector3 center, float radius, Color color, int segments = 16)
        {
            // Draw three circles (XY, XZ, YZ planes)
            DrawCircle(center, Vector3.forward, Vector3.up, radius, color, segments);
            DrawCircle(center, Vector3.up, Vector3.right, radius, color, segments);
            DrawCircle(center, Vector3.right, Vector3.forward, radius, color, segments);
        }
        
        private void DrawCircle(Vector3 center, Vector3 normal, Vector3 up, float radius, Color color, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + Quaternion.LookRotation(normal, up) * (Vector3.up * radius);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep;
                var rotation = Quaternion.LookRotation(normal, up) * Quaternion.Euler(0, 0, angle);
                Vector3 nextPoint = center + rotation * (Vector3.up * radius);
                DrawLine(prevPoint, nextPoint, color, 0.01f);
                prevPoint = nextPoint;
            }
        }
        
        /// <summary>
        /// Release all active lines back to the pool
        /// Call this at the end of each frame
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var line in _activeLines)
            {
                if (line != null)
                {
                    line.gameObject.SetActive(false);
                    line.transform.SetParent(_poolContainer.transform);
                    _availableLines.Enqueue(line);
                }
            }
            _activeLines.Clear();
        }
        
        /// <summary>
        /// Dispose of all pooled objects
        /// </summary>
        public void Dispose()
        {
            if (_activeLines != null)
            {
                foreach (var line in _activeLines)
                {
                    if (line != null)
                        Destroy(line.gameObject);
                }
                _activeLines.Clear();
            }
            
            if (_availableLines != null)
            {
                while (_availableLines.Count > 0)
                {
                    var line = _availableLines.Dequeue();
                    if (line != null)
                        Destroy(line.gameObject);
                }
            }
            
            if (_poolContainer != null)
            {
                Destroy(_poolContainer);
            }
            
            UnityEngine.Debug.Log($"[CollisionDebugLinePool] Disposed. Total created: {_totalCreated}, Peak active: {_peakActive}");
        }
        
        private LineRenderer CreateLineRenderer()
        {
            var go = new GameObject($"DebugLine_{_totalCreated}");
            go.transform.SetParent(_poolContainer.transform);
            
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.02f;
            line.endWidth = 0.02f;
            
            // Use debug material if available, otherwise default
            if (_debugLineMaterial != null)
            {
                line.material = _debugLineMaterial;
            }
            else
            {
                // Create simple unlit material
                line.material = new Material(Shader.Find("Sprites/Default"));
            }
            
            _totalCreated++;
            return line;
        }
        
        /// <summary>
        /// Log pool statistics for debugging
        /// </summary>
        public void LogStatistics()
        {
            UnityEngine.Debug.Log($"[CollisionDebugLinePool] Stats - Total: {_totalCreated}, Active: {Active}, Available: {Available}, Peak: {_peakActive}");
        }
        
        private void LateUpdate()
        {
            // Automatically release all lines at end of frame
            ReleaseAll();
        }
    }
    #endif
}
