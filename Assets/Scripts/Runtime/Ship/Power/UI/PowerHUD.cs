using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Entities;
using Unity.NetCode;
using DIG.Ship.LocalSpace;
using DIG.Survival.Environment;

namespace DIG.Ship.Power.UI
{
    /// <summary>
    /// Simple HUD showing power and life support status when player is in a ship.
    /// Shows warning indicators for brownout and life support failure.
    /// </summary>
    public class PowerHUD : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private TextMeshProUGUI lifeSupportText;
        [SerializeField] private TextMeshProUGUI powerText;
        [SerializeField] private TextMeshProUGUI environmentText;
        [SerializeField] private Image lifeSupportIcon;
        [SerializeField] private Image warningIcon;

        [Header("Colors")]
        [SerializeField] private Color onlineColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color offlineColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color safeColor = new Color(0.3f, 0.8f, 0.4f);
        [SerializeField] private Color dangerColor = new Color(0.9f, 0.3f, 0.2f);

        [Header("Events")]
        public UnityEngine.Events.UnityEvent OnPowerLost;
        public UnityEngine.Events.UnityEvent OnPowerRestored;
        public UnityEngine.Events.UnityEvent OnLifeSupportLost;
        public UnityEngine.Events.UnityEvent OnLifeSupportRestored;

        private World clientWorld;
        private EntityQuery playerQuery;
        private float blinkTimer;
        private bool blinkState;

        // State tracking for events
        private bool wasLifeSupportOnline = true;
        private bool wasBrownout = false;

        private void Start()
        {
            if (hudRoot != null)
                hudRoot.SetActive(false);
        }

        private void OnEnable()
        {
            // Reset state so events trigger if we spawn into a broken ship
            wasLifeSupportOnline = true;
            wasBrownout = false;
        }

        private void Update()
        {
            // Find client world
            if (clientWorld == null || !clientWorld.IsCreated)
            {
                clientWorld = GetClientWorld();
                if (clientWorld != null)
                {
                    var em = clientWorld.EntityManager;
                    playerQuery = em.CreateEntityQuery(
                        ComponentType.ReadOnly<PlayerState>(),
                        ComponentType.ReadOnly<GhostOwnerIsLocal>()
                    );
                }
            }

            if (clientWorld == null) return;

            UpdateHUD();
        }

        private World GetClientWorld()
        {
            foreach (var world in World.All)
            {
                if (world.IsClient())
                    return world;
            }
            return null;
        }

        private void UpdateHUD()
        {
            var em = clientWorld.EntityManager;
            if (playerQuery.IsEmpty)
            {
                if (hudRoot != null) hudRoot.SetActive(false);
                return;
            }

            var players = playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (players.Length == 0)
            {
                players.Dispose();
                if (hudRoot != null) hudRoot.SetActive(false);
                return;
            }

            Entity playerEntity = players[0];
            players.Dispose();

            // Check if player is in a ship
            if (!em.HasComponent<InShipLocalSpace>(playerEntity))
            {
                if (hudRoot != null) hudRoot.SetActive(false);
                return;
            }

            var inShip = em.GetComponentData<InShipLocalSpace>(playerEntity);
            Entity shipEntity = inShip.ShipEntity;

            if (shipEntity == Entity.Null || !em.Exists(shipEntity))
            {
                if (hudRoot != null) hudRoot.SetActive(false);
                return;
            }

            // Show HUD
            if (hudRoot != null) hudRoot.SetActive(true);

            // Get ship power state
            bool hasPowerState = em.HasComponent<ShipPowerState>(shipEntity);
            ShipPowerState powerState = hasPowerState ? 
                em.GetComponentData<ShipPowerState>(shipEntity) : 
                ShipPowerState.Default;

            // Find life support for this ship
            bool lifeSupportOnline = true;
            bool lifeSupportFound = false;

            // Query all life support entities
            var lifeSupportQuery = em.CreateEntityQuery(typeof(LifeSupport));
            var lifeSupportEntities = lifeSupportQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var lsEntity in lifeSupportEntities)
            {
                var ls = em.GetComponentData<LifeSupport>(lsEntity);
                if (ls.ShipEntity == shipEntity)
                {
                    lifeSupportOnline = ls.IsOnline;
                    lifeSupportFound = true;
                    break;
                }
            }
            lifeSupportEntities.Dispose();

            // Update blink timer for warnings
            blinkTimer += Time.deltaTime;
            if (blinkTimer > 0.5f)
            {
                blinkTimer = 0f;
                blinkState = !blinkState;
            }

            // Update Life Support text
            if (lifeSupportText != null)
            {
                if (!lifeSupportFound)
                {
                    lifeSupportText.text = "LIFE SUPPORT: N/A";
                    lifeSupportText.color = warningColor;
                }
                else if (lifeSupportOnline)
                {
                    lifeSupportText.text = "LIFE SUPPORT: ONLINE";
                    lifeSupportText.color = onlineColor;
                }
                else
                {
                    lifeSupportText.text = blinkState ? "LIFE SUPPORT: OFFLINE" : "⚠ LIFE SUPPORT: OFFLINE ⚠";
                    lifeSupportText.color = blinkState ? offlineColor : warningColor;
                }
            }

            // Update Life Support icon
            if (lifeSupportIcon != null)
            {
                lifeSupportIcon.color = lifeSupportOnline ? onlineColor : 
                    (blinkState ? offlineColor : warningColor);
            }

            // Update Power text
            if (powerText != null)
            {
                if (!hasPowerState)
                {
                    powerText.text = "POWER: N/A";
                    powerText.color = warningColor;
                }
                else if (powerState.IsBrownout)
                {
                    powerText.text = $"POWER: {powerState.TotalConsumed:F0}/{powerState.TotalProduced:F0}W ⚠";
                    powerText.color = blinkState ? warningColor : offlineColor;
                }
                else
                {
                    powerText.text = $"POWER: {powerState.TotalConsumed:F0}/{powerState.TotalProduced:F0}W";
                    powerText.color = onlineColor;
                }
            }

            // Update Warning icon
            if (warningIcon != null)
            {
                bool showWarning = !lifeSupportOnline || powerState.IsBrownout;
                warningIcon.gameObject.SetActive(showWarning);
                if (showWarning)
                {
                    warningIcon.color = blinkState ? warningColor : offlineColor;
                }
            }

            // Event Triggers
            if (lifeSupportFound)
            {
                if (wasLifeSupportOnline && !lifeSupportOnline)
                {
                    OnLifeSupportLost?.Invoke();
                }
                else if (!wasLifeSupportOnline && lifeSupportOnline)
                {
                    OnLifeSupportRestored?.Invoke();
                }
                wasLifeSupportOnline = lifeSupportOnline;
            }

            if (hasPowerState)
            {
                if (!wasBrownout && powerState.IsBrownout)
                {
                    OnPowerLost?.Invoke();
                }
                else if (wasBrownout && !powerState.IsBrownout)
                {
                    OnPowerRestored?.Invoke();
                }
                wasBrownout = powerState.IsBrownout;
            }

            // Update Environment text (based on current zone)
            if (environmentText != null)
            {
                if (em.HasComponent<CurrentEnvironmentZone>(playerEntity))
                {
                    var zone = em.GetComponentData<CurrentEnvironmentZone>(playerEntity);
                    if (zone.OxygenRequired)
                    {
                        environmentText.text = "⚠ OXYGEN REQUIRED";
                        environmentText.color = blinkState ? dangerColor : warningColor;
                        environmentText.gameObject.SetActive(true);
                    }
                    else
                    {
                        environmentText.text = "PRESSURIZED";
                        environmentText.color = safeColor;
                        environmentText.gameObject.SetActive(true);
                    }
                }
                else
                {
                    environmentText.gameObject.SetActive(false);
                }
            }
        }
    }
}
