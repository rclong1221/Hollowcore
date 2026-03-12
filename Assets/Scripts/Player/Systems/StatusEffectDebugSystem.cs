using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using Player.Components;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Player.Systems
{
    /// <summary>
    /// Debug system to test applying Status Effects via keyboard input.
    /// Runs on Client/Sim (ServerSimulation for Host).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class StatusEffectDebugSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!UnityEngine.Application.isEditor) return;

            bool shiftPressed = false;
            bool inputHypoxia = false;
            bool inputRadiation = false;
            bool inputBurn = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                shiftPressed = kb.shiftKey.isPressed;
                inputHypoxia = kb.leftBracketKey.wasPressedThisFrame;
                inputRadiation = kb.rightBracketKey.wasPressedThisFrame;
                inputBurn = kb.backslashKey.wasPressedThisFrame;
                
                // Heal (H)
                if (kb.hKey.wasPressedThisFrame)
                {
                    ApplyHealToAllPlayers(25.0f);
                    Debug.Log("Debug: Applied Heal (25 HP)");
                }
            }
#else
            shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            inputHypoxia = Input.GetKeyDown(KeyCode.LeftBracket);
            inputRadiation = Input.GetKeyDown(KeyCode.RightBracket);
            inputBurn = Input.GetKeyDown(KeyCode.Backslash);
            
            // Heal (H)
            if (Input.GetKeyDown(KeyCode.H))
            {
                ApplyHealToAllPlayers(25.0f);
                Debug.Log("Debug: Applied Heal (25 HP)");
            }
#endif

            // Key [ : Hypoxia
            if (inputHypoxia)
            {
                if (shiftPressed)
                {
                    // Cure (Subtract 1.0)
                    ApplyEffectToAllPlayers(StatusEffectType.Hypoxia, -1.0f, 0f, true);
                    Debug.Log("Debug: Cured Hypoxia (-1.0)");
                }
                else
                {
                    // Add
                    ApplyEffectToAllPlayers(StatusEffectType.Hypoxia, 0.2f, 5.0f, true);
                    Debug.Log("Debug: Applied Hypoxia (0.2, 5s)");
                }
            }

            // Key ] : Radiation
            if (inputRadiation)
            {
                if (shiftPressed)
                {
                    // Cure
                    ApplyEffectToAllPlayers(StatusEffectType.RadiationPoisoning, -1.0f, 0f, true);
                    Debug.Log("Debug: Cured Radiation (-1.0)");
                }
                else
                {
                    // Max
                    ApplyEffectToAllPlayers(StatusEffectType.RadiationPoisoning, 0.5f, 10.0f, false);
                    Debug.Log("Debug: Applied Radiation (0.5, 10s)");
                }
            }

            // Key \ : Burn
            if (inputBurn)
            {
                if (shiftPressed)
                {
                    // Cure
                    ApplyEffectToAllPlayers(StatusEffectType.Burn, -1.0f, 0f, true);
                    Debug.Log("Debug: Cured Burn (-1.0)");
                }
                else
                {
                    // Add
                    ApplyEffectToAllPlayers(StatusEffectType.Burn, 1.0f, 3.0f, true);
                    Debug.Log("Debug: Applied Burn (1.0, 3s)");
                }
            }
        }

        private void ApplyEffectToAllPlayers(StatusEffectType type, float severity, float duration, bool additive)
        {
            // Iterate all entities with StatusEffectRequest buffer
            foreach (var (requests, entity) in SystemAPI.Query<DynamicBuffer<StatusEffectRequest>>().WithEntityAccess())
            {
                requests.Add(new StatusEffectRequest
                {
                    Type = type,
                    Severity = severity,
                    Duration = duration,
                    Additive = additive
                });
            }
        }
        private void ApplyHealToAllPlayers(float amount)
        {
            foreach (var (events, entity) in SystemAPI.Query<DynamicBuffer<HealEvent>>().WithEntityAccess())
            {
                events.Add(new HealEvent
                {
                    Amount = amount,
                    SourceEntity = Entity.Null,
                    Type = HealType.Generic,
                    Position = Unity.Mathematics.float3.zero,
                    ServerTick = 0 // Debug immediate
                });
            }
        }
    }
}
