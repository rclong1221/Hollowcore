using System.Collections.Generic;
using UnityEngine;

namespace DIG.Performance
{
    /// <summary>
    /// Epic 7.7.1: Benchmark spawner for collision system stress testing.
    /// 
    /// Spawns N "players" in a dense area with random movement patterns
    /// to generate worst-case collision scenarios for profiling.
    /// 
    /// Usage:
    /// 1. Create empty scene with plane collider as floor
    /// 2. Add this component to a GameObject
    /// 3. Assign PlayerPrefab (any capsule with Rigidbody/CharacterController)
    /// 4. Press Play and adjust PlayerCount in inspector
    /// 5. Open Unity Profiler > Scripts to view DIG.Collision.* markers
    /// 
    /// For ECS testing, use the companion system that spawns ghost prefabs.
    /// </summary>
    public class CollisionBenchmarkSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("Prefab with collider for physics-based collision testing")]
        public GameObject PlayerPrefab;
        
        [Tooltip("Number of players to spawn. Increase to stress test O(N²) scaling.")]
        [Range(2, 200)]
        public int PlayerCount = 10;
        
        [Tooltip("Size of spawn area (meters). Smaller = denser = more collisions.")]
        public float SpawnAreaSize = 10f;
        
        [Tooltip("Height above ground to spawn players")]
        public float SpawnHeight = 1f;
        
        [Header("Movement Settings")]
        [Tooltip("Base movement speed for simulated players")]
        public float MoveSpeed = 5f;
        
        [Tooltip("Sprint multiplier for sprinting players")]
        public float SprintMultiplier = 1.5f;
        
        [Tooltip("Percentage of players that sprint (0-1)")]
        [Range(0f, 1f)]
        public float SprintPercentage = 0.5f;
        
        [Tooltip("How often players change direction (seconds)")]
        public float DirectionChangeInterval = 2f;
        
        [Header("Collision Filtering Test")]
        [Tooltip("Enable to test friendly fire filtering overhead (assign alternating teams)")]
        public bool SimulateTeams = false;
        
        [Tooltip("Number of teams when SimulateTeams is enabled")]
        [Range(2, 10)]
        public int TeamCount = 2;
        
        [Tooltip("Enable friendly fire (if false, same-team collisions filtered)")]
        public bool FriendlyFireEnabled = true;
        
        [Header("Runtime Stats")]
        [Tooltip("Current number of spawned players")]
        [SerializeField] private int _spawnedCount;
        
        [Tooltip("Collision pairs this frame (N*(N-1)/2 max)")]
        [SerializeField] private int _potentialCollisions;
        
        // Internal state
        private readonly List<BenchmarkPlayer> _players = new List<BenchmarkPlayer>();
        private float _respawnTimer;
        
        private class BenchmarkPlayer
        {
            public GameObject GameObject;
            public Rigidbody Rigidbody;
            public CharacterController CharacterController;
            public Vector3 MoveDirection;
            public float Speed;
            public float DirectionTimer;
            public int TeamId;
        }
        
        private void Start()
        {
            SpawnPlayers();
        }
        
        private void Update()
        {
            // Handle dynamic player count changes in editor
            if (_spawnedCount != PlayerCount)
            {
                ClearPlayers();
                SpawnPlayers();
            }
            
            // Update stats
            _spawnedCount = _players.Count;
            _potentialCollisions = _spawnedCount * (_spawnedCount - 1) / 2;
            
            // Move players
            float deltaTime = Time.deltaTime;
            foreach (var player in _players)
            {
                UpdatePlayer(player, deltaTime);
            }
        }
        
        private void SpawnPlayers()
        {
            if (PlayerPrefab == null)
            {
                UnityEngine.Debug.LogError("[CollisionBenchmarkSpawner] PlayerPrefab not assigned! Creating default capsules.");
                CreateDefaultPrefab();
            }
            
            for (int i = 0; i < PlayerCount; i++)
            {
                Vector3 position = GetRandomSpawnPosition();
                GameObject go = Instantiate(PlayerPrefab, position, Quaternion.identity, transform);
                go.name = $"BenchmarkPlayer_{i}";
                
                // Determine if this player sprints
                bool isSprinter = Random.value < SprintPercentage;
                float speed = MoveSpeed * (isSprinter ? SprintMultiplier : 1f);
                
                // Assign team if testing collision filtering
                int teamId = SimulateTeams ? (i % TeamCount) : 0;
                
                var player = new BenchmarkPlayer
                {
                    GameObject = go,
                    Rigidbody = go.GetComponent<Rigidbody>(),
                    CharacterController = go.GetComponent<CharacterController>(),
                    MoveDirection = GetRandomDirection(),
                    Speed = speed,
                    DirectionTimer = Random.Range(0f, DirectionChangeInterval),
                    TeamId = teamId
                };
                
                // Color by team for visual debugging
                if (SimulateTeams)
                {
                    var renderer = go.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = GetTeamColor(teamId);
                    }
                }
                
                _players.Add(player);
            }
            
            _spawnedCount = _players.Count;
            UnityEngine.Debug.Log($"[CollisionBenchmarkSpawner] Spawned {_spawnedCount} players in {SpawnAreaSize}x{SpawnAreaSize}m area. Potential collision pairs: {_potentialCollisions}");
        }
        
        private void ClearPlayers()
        {
            foreach (var player in _players)
            {
                if (player.GameObject != null)
                {
                    Destroy(player.GameObject);
                }
            }
            _players.Clear();
            _spawnedCount = 0;
        }
        
        private Vector3 GetRandomSpawnPosition()
        {
            float halfSize = SpawnAreaSize * 0.5f;
            return new Vector3(
                Random.Range(-halfSize, halfSize),
                SpawnHeight,
                Random.Range(-halfSize, halfSize)
            );
        }
        
        private Vector3 GetRandomDirection()
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
        
        private void UpdatePlayer(BenchmarkPlayer player, float deltaTime)
        {
            if (player.GameObject == null) return;
            
            // Update direction timer
            player.DirectionTimer -= deltaTime;
            if (player.DirectionTimer <= 0f)
            {
                player.MoveDirection = GetRandomDirection();
                player.DirectionTimer = DirectionChangeInterval;
            }
            
            // Move player
            Vector3 velocity = player.MoveDirection * player.Speed;
            
            if (player.CharacterController != null)
            {
                player.CharacterController.Move(velocity * deltaTime);
            }
            else if (player.Rigidbody != null)
            {
                player.Rigidbody.linearVelocity = new Vector3(velocity.x, player.Rigidbody.linearVelocity.y, velocity.z);
            }
            else
            {
                player.GameObject.transform.position += velocity * deltaTime;
            }
            
            // Keep in bounds
            Vector3 pos = player.GameObject.transform.position;
            float halfSize = SpawnAreaSize * 0.5f;
            
            if (Mathf.Abs(pos.x) > halfSize || Mathf.Abs(pos.z) > halfSize)
            {
                // Bounce off boundary
                player.MoveDirection = -player.MoveDirection;
                pos.x = Mathf.Clamp(pos.x, -halfSize, halfSize);
                pos.z = Mathf.Clamp(pos.z, -halfSize, halfSize);
                player.GameObject.transform.position = pos;
            }
        }
        
        private Color GetTeamColor(int teamId)
        {
            // Generate distinct colors for teams
            float hue = (float)teamId / TeamCount;
            return Color.HSVToRGB(hue, 0.8f, 0.9f);
        }
        
        private void CreateDefaultPrefab()
        {
            // Create simple capsule prefab at runtime
            PlayerPrefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            PlayerPrefab.name = "DefaultBenchmarkPlayer";
            
            // Add rigidbody for physics-based movement
            var rb = PlayerPrefab.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            
            // Disable prefab (it's just a template)
            PlayerPrefab.SetActive(false);
        }
        
        private void OnDestroy()
        {
            ClearPlayers();
            
            // Clean up default prefab if we created one
            if (PlayerPrefab != null && PlayerPrefab.name == "DefaultBenchmarkPlayer")
            {
                Destroy(PlayerPrefab);
            }
        }
        
        private void OnDrawGizmos()
        {
            // Draw spawn area bounds
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * SpawnHeight, new Vector3(SpawnAreaSize, 0.1f, SpawnAreaSize));
            
            // Draw player count info
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, 
                $"Players: {PlayerCount}\nPairs: {PlayerCount * (PlayerCount - 1) / 2}");
            #endif
        }
        
        /// <summary>
        /// Force respawn all players (useful for benchmark resets)
        /// </summary>
        [ContextMenu("Respawn All Players")]
        public void RespawnPlayers()
        {
            ClearPlayers();
            SpawnPlayers();
        }
        
        /// <summary>
        /// Toggle friendly fire for runtime testing of filtering overhead
        /// </summary>
        [ContextMenu("Toggle Friendly Fire")]
        public void ToggleFriendlyFire()
        {
            FriendlyFireEnabled = !FriendlyFireEnabled;
            UnityEngine.Debug.Log($"[CollisionBenchmarkSpawner] Friendly Fire: {(FriendlyFireEnabled ? "ENABLED" : "DISABLED")}");
        }
    }
}
