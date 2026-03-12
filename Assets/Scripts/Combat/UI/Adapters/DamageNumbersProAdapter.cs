using UnityEngine;
using Unity.Mathematics;
using DIG.Targeting.Theming;
using DIG.Combat.UI.Config;
using DamageNumbersPro;

namespace DIG.Combat.UI.Adapters
{
    /// <summary>
    /// EPIC 15.9: Full Damage Numbers Pro integration with proper pooling.
    /// EPIC 15.22: Extended with hit severity tiers, defensive feedback, contextual events,
    /// ResultFlags support, and AAA culling (distance, frustum, priority).
    /// All visual config (prefabs, colors, scales, culling) comes from DamageFeedbackProfile.
    /// </summary>
    public class DamageNumbersProAdapter : DamageNumberAdapterBase
    {
        [Header("Spawn Settings")]
        [Tooltip("Vertical offset from hit position")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0, 1.5f, 0);

        [Tooltip("Random horizontal spread range")]
        [SerializeField] private float randomOffsetRange = 0.3f;

        [Header("Damage Stacking")]
        [Tooltip("Combine rapid damage into single number if within this time")]
        [SerializeField] private float stackWindow = 0.1f;

        [Tooltip("Minimum damage to display (ignore tiny amounts)")]
        [SerializeField] private float minDisplayThreshold = 1f;

        [Header("Culling Overrides")]
        [Tooltip("Enable frustum culling (skip numbers behind or outside the camera)")]
        [SerializeField] private bool enableFrustumCulling = true;

        // Cached strings to avoid GC allocations (EPIC 15.22: zero-alloc pooling)
        private static readonly string CritText = " CRIT!";
        private static readonly string ExecuteText = "EXECUTE!";
        private static readonly string WeaknessText = "\u25b2"; // ▲
        private static readonly string ResistText = "\u25bc";   // ▼
        private static readonly string MissText = "MISS";
        private static readonly string HealPrefix = "+";
        private static readonly string ShieldIcon = "\u25c8 "; // ◈

        // Pre-concatenated left text (avoids per-spawn string alloc)
        private static readonly string HeadshotPrefix = "HEADSHOT ";
        private static readonly string BackstabPrefix = "BACKSTAB ";
        private static readonly string PoiseBreakPrefix = "BROKEN ";
        private static readonly string BlockedPrefix = "BLOCKED ";
        private static readonly string ParryPrefix = "PARRY! ";
        private static readonly string ImmunePrefix = "IMMUNE ";

        // Pooled elemental suffix strings (avoids per-spawn switch alloc)
        private static readonly string FireSuffix = "\ud83d\udd25";       // 🔥
        private static readonly string IceSuffix = "\u2744";              // ❄
        private static readonly string LightningSuffix = "\u26a1";        // ⚡
        private static readonly string PoisonSuffix = "\u2620";           // ☠
        private static readonly string HolySuffix = "\u2726";             // ✦
        private static readonly string ShadowSuffix = "\u25c6";           // ◆
        private static readonly string ArcaneSuffix = "\u2727";           // ✧

        // Stacking state
        private float _lastDamageTime;
        private float _stackedDamage;
        private float3 _lastPosition;
        private HitType _lastHitType;
        private DamageType _lastDamageType;
        private ResultFlags _lastFlags;
        private float _flushTimer = -1f;

        // EPIC 15.22: Culling state
        private Camera _cachedCamera;
        private int _activeNumberCount;
        private Plane[] _frustumPlanes = new Plane[6];
        private float _lastFrustumUpdateTime;

        // ─────────────────────────────────────────────────────────────────
        // Profile accessors
        // ─────────────────────────────────────────────────────────────────

        private DamageNumber GetPrefabForHitType(HitType hitType)
        {
            if (feedbackProfile == null) return null;

            var prefab = feedbackProfile.GetHitProfile(hitType).Prefab;
            if (prefab != null) return prefab;

            // Fallback chain
            return hitType switch
            {
                HitType.Execute => feedbackProfile.CriticalHit.Prefab ?? feedbackProfile.NormalHit.Prefab,
                HitType.Parried => feedbackProfile.BlockedHit.Prefab ?? feedbackProfile.NormalHit.Prefab,
                HitType.Immune => feedbackProfile.MissHit.Prefab ?? feedbackProfile.NormalHit.Prefab,
                _ => feedbackProfile.NormalHit.Prefab
            };
        }

        private float CullDistance => feedbackProfile != null ? feedbackProfile.CullDistance : 50f;
        private int MaxActiveNumbers => feedbackProfile != null ? feedbackProfile.MaxActiveNumbers : 50;

        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        public override void ShowDamageNumber(float damage, float3 worldPosition, HitType hitType, DamageType damageType)
        {
            ShowDamageNumber(damage, worldPosition, hitType, damageType, ResultFlags.None);
        }

        /// <summary>
        /// EPIC 15.22: Display a damage number with contextual flags.
        /// </summary>
        public void ShowDamageNumber(float damage, float3 worldPosition, HitType hitType, DamageType damageType, ResultFlags flags)
        {
            // Defensive types (Blocked, Parried, Immune) bypass stacking - show immediately
            if (hitType == HitType.Blocked || hitType == HitType.Parried || hitType == HitType.Immune)
            {
                SpawnDefensiveText(worldPosition, hitType, damage);
                return;
            }

            // Skip tiny damage (unless critical or execute)
            if (damage < minDisplayThreshold && hitType != HitType.Critical && hitType != HitType.Execute)
                return;

            // Check for damage stacking
            float timeSinceLast = Time.time - _lastDamageTime;
            if (timeSinceLast < stackWindow && math.distance(worldPosition, _lastPosition) < 1f)
            {
                _stackedDamage += damage;
                // Upgrade hit type if this one is better
                if (hitType == HitType.Critical || hitType == HitType.Execute)
                    _lastHitType = hitType;
                // Merge flags
                _lastFlags |= flags;
                return;
            }

            // Flush any stacked damage first
            if (_stackedDamage > 0)
            {
                SpawnDamageNumber(_stackedDamage, _lastPosition, _lastHitType, _lastDamageType, _lastFlags);
                _stackedDamage = 0;
            }

            // Start new stack or spawn immediately
            _lastDamageTime = Time.time;
            _lastPosition = worldPosition;
            _lastHitType = hitType;
            _lastDamageType = damageType;
            _lastFlags = flags;
            _stackedDamage = damage;

            // Reset flush timer — will fire in LateUpdate after stackWindow elapses
            _flushTimer = stackWindow;
        }

        private void FlushStackedDamage()
        {
            if (_stackedDamage > 0)
            {
                SpawnDamageNumber(_stackedDamage, _lastPosition, _lastHitType, _lastDamageType, _lastFlags);
                _stackedDamage = 0;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // EPIC 15.22: Culling
        // ─────────────────────────────────────────────────────────────────

        private static int GetHitPriority(HitType hitType)
        {
            return hitType switch
            {
                HitType.Graze => 0,       // Lowest - culled first
                HitType.Hit => 1,
                HitType.Miss => 1,
                HitType.Blocked => 2,
                HitType.Parried => 2,
                HitType.Immune => 2,
                HitType.Critical => 3,    // High - rarely culled
                HitType.Execute => 4,     // Highest - never culled
                _ => 1
            };
        }

        private bool ShouldCull(Vector3 worldPosition, HitType hitType)
        {
            int priority = GetHitPriority(hitType);

            // Critical and Execute are never culled
            if (priority >= 3)
                return false;

            Camera cam = GetCamera();
            if (cam == null)
                return false;

            // 1. Distance culling
            float cullDist = CullDistance;
            if (cullDist > 0f)
            {
                float sqrDist = (cam.transform.position - worldPosition).sqrMagnitude;
                if (sqrDist > cullDist * cullDist)
                    return true;
            }

            // 2. Frustum culling
            if (enableFrustumCulling)
            {
                UpdateFrustumPlanes(cam);
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, new Bounds(worldPosition, Vector3.one * 0.1f)))
                    return true;
            }

            // 3. Priority count culling
            int maxActive = MaxActiveNumbers;
            if (maxActive > 0 && _activeNumberCount >= maxActive)
            {
                if (priority < 2)
                    return true;
            }

            return false;
        }

        private Camera GetCamera()
        {
            if (_cachedCamera == null || !_cachedCamera.isActiveAndEnabled)
                _cachedCamera = Camera.main;
            return _cachedCamera;
        }

        private void UpdateFrustumPlanes(Camera cam)
        {
            if (Mathf.Approximately(_lastFrustumUpdateTime, Time.time))
                return;

            GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanes);
            _lastFrustumUpdateTime = Time.time;
        }

        private void LateUpdate()
        {
            if (_activeNumberCount > 0)
                _activeNumberCount = 0;

            if (_flushTimer > 0f)
            {
                _flushTimer -= Time.deltaTime;
                if (_flushTimer <= 0f)
                {
                    _flushTimer = -1f;
                    FlushStackedDamage();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Spawning
        // ─────────────────────────────────────────────────────────────────

        private void SpawnDamageNumber(float damage, float3 worldPosition, HitType hitType, DamageType damageType, ResultFlags flags)
        {
            DamageNumber prefab = GetPrefabForHitType(hitType);
            if (prefab == null)
                return;

            Vector3 position = (Vector3)worldPosition + spawnOffset + GetRandomOffset();

            if (ShouldCull(position, hitType))
                return;

            Color color = GetFinalColor(hitType, damageType);

            DamageNumber popup = prefab.Spawn(position, damage);
            popup.SetColor(color);
            _activeNumberCount++;

            // Right text: hit type suffix
            switch (hitType)
            {
                case HitType.Critical:
                    popup.rightText = CritText;
                    popup.enableRightText = true;
                    break;
                case HitType.Execute:
                    popup.rightText = ExecuteText;
                    popup.enableRightText = true;
                    break;
                default:
                    popup.enableRightText = false;
                    break;
            }

            // Top text: elemental suffix or efficacy indicator
            if ((flags & ResultFlags.Weakness) != 0)
            {
                popup.enableTopText = true;
                popup.topText = WeaknessText;
            }
            else if ((flags & ResultFlags.Resistance) != 0)
            {
                popup.enableTopText = true;
                popup.topText = ResistText;
            }
            else if (damageType != DamageType.Physical)
            {
                popup.enableTopText = true;
                popup.topText = GetElementalSuffix(damageType);
            }
            else
            {
                popup.enableTopText = false;
            }

            // Left text: contextual flags (Headshot, Backstab, PoiseBreak)
            if ((flags & ResultFlags.Headshot) != 0)
            {
                popup.leftText = HeadshotPrefix;
                popup.enableLeftText = true;
            }
            else if ((flags & ResultFlags.Backstab) != 0)
            {
                popup.leftText = BackstabPrefix;
                popup.enableLeftText = true;
            }
            else if ((flags & ResultFlags.PoiseBreak) != 0)
            {
                popup.leftText = PoiseBreakPrefix;
                popup.enableLeftText = true;
            }
            else
            {
                popup.enableLeftText = false;
            }
        }

        private void SpawnDefensiveText(float3 worldPosition, HitType hitType, float mitigatedAmount)
        {
            DamageNumber prefab = GetPrefabForHitType(hitType);
            if (prefab == null)
                return;

            Vector3 position = (Vector3)worldPosition + spawnOffset;

            if (ShouldCull(position, hitType))
                return;

            Color color = GetFinalColor(hitType, DamageType.Physical);

            string prefix = hitType switch
            {
                HitType.Blocked => BlockedPrefix,
                HitType.Parried => ParryPrefix,
                HitType.Immune => ImmunePrefix,
                _ => ""
            };

            if (mitigatedAmount > 0)
            {
                DamageNumber popup = prefab.Spawn(position, mitigatedAmount);
                popup.leftText = prefix;
                popup.enableLeftText = true;
                popup.enableRightText = false;
                popup.enableTopText = false;
                popup.SetColor(color);
            }
            else
            {
                DamageNumber popup = prefab.Spawn(position);
                popup.enableNumber = false;
                popup.leftText = prefix;
                popup.enableLeftText = true;
                popup.enableRightText = false;
                popup.enableTopText = false;
                popup.SetColor(color);
            }
            _activeNumberCount++;
        }

        public void ShowDefensiveText(float3 worldPosition, HitType hitType, float mitigatedAmount)
        {
            SpawnDefensiveText(worldPosition, hitType, mitigatedAmount);
        }

        public override void ShowMiss(float3 worldPosition)
        {
            DamageNumber prefab = GetPrefabForHitType(HitType.Miss);
            if (prefab == null)
                return;

            Vector3 position = (Vector3)worldPosition + spawnOffset + GetRandomOffset();

            if (ShouldCull(position, HitType.Miss))
                return;

            DamageNumber popup = prefab.Spawn(position);
            popup.enableNumber = false;
            popup.leftText = MissText;
            popup.enableLeftText = true;
            popup.SetColor(GetHitTypeColor(HitType.Miss));
            _activeNumberCount++;
        }

        public override void ShowHealNumber(float amount, float3 worldPosition)
        {
            if (amount < minDisplayThreshold) return;

            DamageNumber prefab = feedbackProfile != null ? feedbackProfile.HealPrefab : null;
            prefab = prefab != null ? prefab : GetPrefabForHitType(HitType.Hit);
            if (prefab == null) return;

            Vector3 position = (Vector3)worldPosition + spawnOffset + GetRandomOffset();

            if (ShouldCull(position, HitType.Hit))
                return;

            DamageNumber popup = prefab.Spawn(position, amount);
            popup.leftText = HealPrefix;
            popup.enableLeftText = true;
            popup.SetColor(Color.green);
            _activeNumberCount++;
        }

        public void ShowBlock(float3 worldPosition, float blockedAmount)
        {
            SpawnDefensiveText(worldPosition, HitType.Blocked, blockedAmount);
        }

        public void ShowAbsorb(float3 worldPosition, float absorbedAmount)
        {
            DamageNumber prefab = feedbackProfile != null ? feedbackProfile.AbsorbPrefab : null;
            prefab = prefab != null ? prefab : GetPrefabForHitType(HitType.Hit);
            if (prefab == null) return;

            Vector3 position = (Vector3)worldPosition + spawnOffset;

            if (ShouldCull(position, HitType.Hit))
                return;

            DamageNumber popup = prefab.Spawn(position, absorbedAmount);
            popup.leftText = ShieldIcon;
            popup.enableLeftText = true;
            popup.SetColor(Color.cyan);
            _activeNumberCount++;
        }

        public void ShowDOTTick(float3 worldPosition, float damage, DamageType damageType)
        {
            DamageNumber prefab = feedbackProfile != null ? feedbackProfile.DOTPrefab : null;
            prefab = prefab != null ? prefab : GetPrefabForHitType(HitType.Hit);
            if (prefab == null) return;

            Vector3 position = (Vector3)worldPosition + spawnOffset + GetRandomOffset();

            if (ShouldCull(position, HitType.Graze))
                return;

            Color color = GetDOTTickColor(damageType);

            DamageNumber popup = prefab.Spawn(position, damage);
            popup.SetColor(color);
            _activeNumberCount++;
        }

        /// <summary>
        /// DOT-specific color mapping. Physical DOTs are Bleed ticks (red),
        /// not generic white. Other elements use standard GetElementColor.
        /// </summary>
        private Color GetDOTTickColor(DamageType damageType)
        {
            if (damageType == DamageType.Physical)
                return new Color(0.8f, 0f, 0f); // Bleed red
            return GetElementColor(damageType);
        }

        public void ShowStatusApplied(float3 worldPosition, string statusName, Color statusColor)
        {
            DamageNumber prefab = GetPrefabForHitType(HitType.Hit);
            if (prefab == null) return;

            Vector3 position = (Vector3)worldPosition + spawnOffset + Vector3.up * 0.5f;

            if (ShouldCull(position, HitType.Hit))
                return;

            DamageNumber popup = prefab.Spawn(position);
            popup.enableNumber = false;
            popup.leftText = statusName.ToUpper();
            popup.enableLeftText = true;
            popup.SetColor(statusColor);
            _activeNumberCount++;
        }

        // ─────────────────────────────────────────────────────────────────
        // Utility
        // ─────────────────────────────────────────────────────────────────

        private Vector3 GetRandomOffset()
        {
            return new Vector3(
                UnityEngine.Random.Range(-randomOffsetRange, randomOffsetRange),
                UnityEngine.Random.Range(0, randomOffsetRange * 0.5f),
                UnityEngine.Random.Range(-randomOffsetRange, randomOffsetRange)
            );
        }

        private static string GetElementalSuffix(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Fire => FireSuffix,
                DamageType.Ice => IceSuffix,
                DamageType.Lightning => LightningSuffix,
                DamageType.Poison => PoisonSuffix,
                DamageType.Holy => HolySuffix,
                DamageType.Shadow => ShadowSuffix,
                DamageType.Arcane => ArcaneSuffix,
                _ => ""
            };
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _flushTimer = -1f;
            // Discard stacked damage — spawning during scene teardown creates
            // orphan clones ("DamageNumber_Normal(Clone)") that Unity warns about.
            _stackedDamage = 0;
        }
    }
}
