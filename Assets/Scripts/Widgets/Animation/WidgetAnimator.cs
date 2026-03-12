using UnityEngine;
using System;

namespace DIG.Widgets.Animation
{
    /// <summary>
    /// EPIC 15.26 Phase 7: Static utility for widget spawn/despawn/damage animations.
    /// Uses manual lerp (no DOTween dependency). All animations respect ReducedMotion.
    ///
    /// Animation Spec:
    ///   Spawn:        0.2s  Elastic out (scale 0→1)       ReducedMotion: instant
    ///   Despawn:      0.15s Ease-in (fade to 0)           ReducedMotion: instant
    ///   DamageFlash:  0.1s  Linear (white→original)       ReducedMotion: skip
    ///   DamageShake:  0.2s  Decay (5-10px random)         ReducedMotion: skip
    ///   HealthTrail:  0.5s  Ease-out (trail catches fill)  ReducedMotion: instant
    ///   HealPulse:    0.3s  Ease-out (green glow)          ReducedMotion: skip
    /// </summary>
    public static class WidgetAnimator
    {
        // ── Spawn Animation ────────────────────────────────────────

        /// <summary>
        /// Animate a widget spawning in. Elastic scale from 0 to 1 over 0.2s.
        /// Returns the WidgetAnimation handle for the coroutine.
        /// </summary>
        public static WidgetAnimation AnimateSpawn(Transform target)
        {
            if (target == null) return default;

            if (IsReducedMotion)
            {
                target.localScale = Vector3.one;
                return default;
            }

            var anim = new WidgetAnimation
            {
                Target = target,
                Type = AnimationType.Spawn,
                Duration = 0.2f,
                Elapsed = 0f,
                IsActive = true
            };
            target.localScale = Vector3.zero;
            return anim;
        }

        /// <summary>
        /// Animate a widget despawning. Fade/scale to 0 over 0.15s.
        /// Calls onComplete when done.
        /// </summary>
        public static WidgetAnimation AnimateDespawn(Transform target, Action onComplete = null)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return default;
            }

            if (IsReducedMotion)
            {
                target.localScale = Vector3.zero;
                onComplete?.Invoke();
                return default;
            }

            return new WidgetAnimation
            {
                Target = target,
                Type = AnimationType.Despawn,
                Duration = 0.15f,
                Elapsed = 0f,
                IsActive = true,
                OnComplete = onComplete
            };
        }

        /// <summary>
        /// Flash a renderer white then return to original color over 0.1s.
        /// </summary>
        public static WidgetAnimation AnimateDamageFlash(Renderer renderer)
        {
            if (renderer == null || IsReducedMotion) return default;

            return new WidgetAnimation
            {
                TargetRenderer = renderer,
                Type = AnimationType.DamageFlash,
                Duration = 0.1f,
                Elapsed = 0f,
                IsActive = true,
                OriginalColor = renderer.material.color
            };
        }

        /// <summary>
        /// Shake a transform with decaying intensity over 0.2s.
        /// </summary>
        public static WidgetAnimation AnimateShake(Transform target, float intensity = 5f)
        {
            if (target == null || IsReducedMotion) return default;

            return new WidgetAnimation
            {
                Target = target,
                Type = AnimationType.Shake,
                Duration = 0.2f,
                Elapsed = 0f,
                IsActive = true,
                Intensity = intensity,
                OriginalPosition = target.localPosition
            };
        }

        // ── Update ─────────────────────────────────────────────────

        /// <summary>
        /// Tick an animation forward. Call each frame. Returns true if still active.
        /// </summary>
        public static bool Update(ref WidgetAnimation anim, float deltaTime)
        {
            if (!anim.IsActive) return false;

            anim.Elapsed += deltaTime;
            float t = Mathf.Clamp01(anim.Elapsed / anim.Duration);

            switch (anim.Type)
            {
                case AnimationType.Spawn:
                    if (anim.Target != null)
                    {
                        float scale = ElasticEaseOut(t);
                        anim.Target.localScale = Vector3.one * scale;
                    }
                    break;

                case AnimationType.Despawn:
                    if (anim.Target != null)
                    {
                        float scale = 1f - EaseIn(t);
                        anim.Target.localScale = Vector3.one * Mathf.Max(0f, scale);
                    }
                    break;

                case AnimationType.DamageFlash:
                    if (anim.TargetRenderer != null)
                    {
                        Color c = Color.Lerp(Color.white, anim.OriginalColor, t);
                        anim.TargetRenderer.material.color = c;
                    }
                    break;

                case AnimationType.Shake:
                    if (anim.Target != null)
                    {
                        float decay = 1f - t;
                        float offsetX = (Mathf.PerlinNoise(anim.Elapsed * 50f, 0f) - 0.5f) * 2f * anim.Intensity * decay;
                        float offsetY = (Mathf.PerlinNoise(0f, anim.Elapsed * 50f) - 0.5f) * 2f * anim.Intensity * decay;
                        anim.Target.localPosition = anim.OriginalPosition + new Vector3(offsetX, offsetY, 0f);
                    }
                    break;
            }

            if (t >= 1f)
            {
                anim.IsActive = false;

                // Cleanup
                if (anim.Type == AnimationType.Shake && anim.Target != null)
                    anim.Target.localPosition = anim.OriginalPosition;

                anim.OnComplete?.Invoke();
                return false;
            }

            return true;
        }

        // ── Easing functions ───────────────────────────────────────

        private static float ElasticEaseOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            float p = 0.3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;
        }

        private static float EaseIn(float t)
        {
            return t * t;
        }

        private static bool IsReducedMotion =>
            Config.WidgetAccessibilityManager.HasInstance &&
            Config.WidgetAccessibilityManager.Instance.ReducedMotion;
    }

    /// <summary>
    /// Lightweight animation state. Stack-allocated, no GC.
    /// </summary>
    public struct WidgetAnimation
    {
        public Transform Target;
        public Renderer TargetRenderer;
        public AnimationType Type;
        public float Duration;
        public float Elapsed;
        public bool IsActive;
        public float Intensity;
        public Vector3 OriginalPosition;
        public Color OriginalColor;
        public Action OnComplete;
    }

    public enum AnimationType : byte
    {
        Spawn,
        Despawn,
        DamageFlash,
        Shake
    }
}
