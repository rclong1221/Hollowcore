using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Combat.Knockback;

namespace DIG.Combat.Editor
{
    /// <summary>
    /// EPIC 16.9: Editor window for testing knockback on entities during play mode.
    /// DIG > Combat > Knockback Tester
    /// </summary>
    public class KnockbackTesterWindow : EditorWindow
    {
        private KnockbackType _type = KnockbackType.Push;
        private float _force = 500f;
        private KnockbackEasing _easing = KnockbackEasing.EaseOut;
        private KnockbackFalloff _falloff = KnockbackFalloff.None;
        private float _launchVerticalRatio = 0.4f;
        private bool _ignoreSuperArmor;
        private bool _triggersInterrupt;
        private bool _debugOverlay;

        private double _nextRepaintTime;
        private const double RepaintInterval = 0.25; // 4 Hz

        [MenuItem("DIG/Combat/Knockback Tester")]
        public static void ShowWindow()
        {
            GetWindow<KnockbackTesterWindow>("Knockback Tester");
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying && EditorApplication.timeSinceStartup >= _nextRepaintTime)
            {
                _nextRepaintTime = EditorApplication.timeSinceStartup + RepaintInterval;
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Knockback Tester", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test knockback.", MessageType.Info);
                return;
            }

            // Configuration
            _type = (KnockbackType)EditorGUILayout.EnumPopup("Type", _type);
            _force = EditorGUILayout.Slider("Force (N)", _force, 50f, 5000f);
            _easing = (KnockbackEasing)EditorGUILayout.EnumPopup("Easing", _easing);
            _falloff = (KnockbackFalloff)EditorGUILayout.EnumPopup("Falloff", _falloff);

            if (_type == KnockbackType.Launch)
            {
                _launchVerticalRatio = EditorGUILayout.Slider("Vertical Ratio", _launchVerticalRatio, 0f, 1f);
            }

            _ignoreSuperArmor = EditorGUILayout.Toggle("Ignore SuperArmor", _ignoreSuperArmor);
            _triggersInterrupt = EditorGUILayout.Toggle("Triggers Interrupt", _triggersInterrupt);

            EditorGUILayout.Space(8);

            // Debug overlay toggle
            bool newDebug = EditorGUILayout.Toggle("Debug Overlay", _debugOverlay);
            if (newDebug != _debugOverlay)
            {
                _debugOverlay = newDebug;
                KnockbackDebugSystem.Enabled = _debugOverlay;
            }

            EditorGUILayout.Space(8);

            // Fire button — applies knockback to all entities with KnockbackState from camera direction
            if (GUILayout.Button("Fire Knockback (All Entities)", GUILayout.Height(30)))
            {
                FireKnockbackToAll();
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Reset All Knockback"))
            {
                ResetAllKnockback();
            }
        }

        private void FireKnockbackToAll()
        {
            // Get camera direction for knockback direction
            var sceneView = SceneView.lastActiveSceneView;
            float3 direction = sceneView != null
                ? (float3)sceneView.camera.transform.forward
                : new float3(0, 0, 1);
            direction.y = 0;
            direction = math.normalizesafe(direction, new float3(0, 0, 1));

            // Find a world with KnockbackState entities
            foreach (var world in World.All)
            {
                if (world.IsCreated && !world.Flags.HasFlag(WorldFlags.Editor))
                {
                    var entityManager = world.EntityManager;
                    var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<KnockbackState>());
                    var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

                    for (int i = 0; i < entities.Length; i++)
                    {
                        // Create KnockbackRequest entity in this world
                        var requestEntity = entityManager.CreateEntity();
                        entityManager.AddComponentData(requestEntity, new KnockbackRequest
                        {
                            TargetEntity = entities[i],
                            SourceEntity = Entity.Null,
                            Direction = direction,
                            Force = _force,
                            Type = _type,
                            Falloff = _falloff,
                            Easing = _easing,
                            LaunchVerticalRatio = _launchVerticalRatio,
                            IgnoreSuperArmor = _ignoreSuperArmor,
                            TriggersInterrupt = _triggersInterrupt
                        });
                    }

                    entities.Dispose();
                    query.Dispose();
                    break; // Only first game world
                }
            }
        }

        private void ResetAllKnockback()
        {
            foreach (var world in World.All)
            {
                if (world.IsCreated && !world.Flags.HasFlag(WorldFlags.Editor))
                {
                    var entityManager = world.EntityManager;
                    var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<KnockbackState>());
                    var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

                    for (int i = 0; i < entities.Length; i++)
                    {
                        entityManager.SetComponentData(entities[i], new KnockbackState());
                    }

                    entities.Dispose();
                    query.Dispose();
                    break;
                }
            }
        }
    }
}
