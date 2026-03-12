using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DIG.Shared;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DIG.Ship.Cargo.UI
{
    /// <summary>
    /// UI Panel for cargo transfer between player inventory and ship cargo.
    /// Shows two columns: player inventory (left) and ship cargo (right).
    /// </summary>
    public class CargoUIPanel : MonoBehaviour
    {
        [Header("Panel Settings")]
        [SerializeField] private float transferCooldown = 0.2f;

        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform inventoryListContent;
        [SerializeField] private Transform cargoListContent;
        [SerializeField] private TextMeshProUGUI inventoryWeightText;
        [SerializeField] private TextMeshProUGUI cargoWeightText;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button closeButton;

        [Header("Item Prefab")]
        [SerializeField] private GameObject itemRowPrefab;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private Color warningColor = new Color(0.8f, 0.4f, 0.1f, 0.9f);
        [SerializeField] private Color overCapacityColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);

        private float lastTransferTime;
        private bool isOpen;
        private CargoUIDataBridge dataBridge;

        private List<CargoItemRow> inventoryRows = new List<CargoItemRow>();
        private List<CargoItemRow> cargoRows = new List<CargoItemRow>();

        private void Start()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseUI);

            // Find or create data bridge
            // Find or create data bridge
            dataBridge = FindFirstObjectByType<CargoUIDataBridge>();
            if (dataBridge == null)
            {
                var bridgeGO = new GameObject("CargoUIDataBridge");
                dataBridge = bridgeGO.AddComponent<CargoUIDataBridge>();
            }
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Check for T key to open (only when near cargo terminal)
            if (keyboard.tKey.wasPressedThisFrame && !isOpen)
            {
                if (dataBridge != null && dataBridge.IsNearCargoTerminal)
                {
                    OpenUI();
                }
            }

            // Check for ESC to close
            if (keyboard.escapeKey.wasPressedThisFrame && isOpen)
            {
                CloseUI();
            }
#else
            // Legacy input fallback
            if (Input.GetKeyDown(KeyCode.T) && !isOpen)
            {
                if (dataBridge != null && dataBridge.IsNearCargoTerminal)
                {
                    OpenUI();
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape) && isOpen)
            {
                CloseUI();
            }
#endif

            // Update UI if open
            if (isOpen && dataBridge != null)
            {
                RefreshUI();
                
                // Auto-close if player walks away
                if (!dataBridge.IsNearCargoTerminal)
                {
                    CloseUI();
                }
            }
        }

        public void OpenUI()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                isOpen = true;
                DIG.UI.MenuState.RegisterMenu(this, true);
                RefreshUI();
            }
        }

        public void CloseUI()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
                isOpen = false;
                DIG.UI.MenuState.RegisterMenu(this, false);
            }
        }

        private void RefreshUI()
        {
            if (dataBridge == null) return;

            // Update title
            if (titleText != null)
            {
                titleText.text = "Ship Cargo";
            }

            // Update inventory list
            RefreshInventoryList();

            // Update cargo list
            RefreshCargoList();

            // Update weight displays
            UpdateWeightDisplays();
        }

        private void RefreshInventoryList()
        {
            var items = dataBridge.GetPlayerInventory();
            
            // Clear old rows
            foreach (var row in inventoryRows)
            {
                if (row != null && row.gameObject != null)
                    Destroy(row.gameObject);
            }
            inventoryRows.Clear();

            // Create new rows
            if (inventoryListContent != null && itemRowPrefab != null)
            {
                foreach (var item in items)
                {
                    var rowGO = Instantiate(itemRowPrefab, inventoryListContent);
                    var row = rowGO.GetComponent<CargoItemRow>();
                    if (row != null)
                    {
                        row.Setup(item.ResourceType.ToString(), item.Quantity, true, () => OnDepositClicked(item.ResourceType));
                        inventoryRows.Add(row);
                    }
                }
            }
        }

        private void RefreshCargoList()
        {
            var items = dataBridge.GetShipCargo();
            
            // Clear old rows
            foreach (var row in cargoRows)
            {
                if (row != null && row.gameObject != null)
                    Destroy(row.gameObject);
            }
            cargoRows.Clear();

            // Create new rows
            if (cargoListContent != null && itemRowPrefab != null)
            {
                foreach (var item in items)
                {
                    var rowGO = Instantiate(itemRowPrefab, cargoListContent);
                    var row = rowGO.GetComponent<CargoItemRow>();
                    if (row != null)
                    {
                        row.Setup(item.ResourceType.ToString(), item.Quantity, false, () => OnWithdrawClicked(item.ResourceType));
                        cargoRows.Add(row);
                    }
                }
            }
        }

        private void UpdateWeightDisplays()
        {
            // Player inventory weight
            if (inventoryWeightText != null)
            {
                var invWeight = dataBridge.GetPlayerInventoryWeight();
                var invMaxWeight = dataBridge.GetPlayerMaxWeight();
                inventoryWeightText.text = $"Weight: {invWeight:F1} / {invMaxWeight:F0} kg";
            }

            // Ship cargo weight
            if (cargoWeightText != null)
            {
                var cargoWeight = dataBridge.GetShipCargoWeight();
                var cargoMaxWeight = dataBridge.GetShipMaxWeight();
                bool isOverCapacity = dataBridge.IsShipOverCapacity();

                cargoWeightText.text = $"Weight: {cargoWeight:F1} / {cargoMaxWeight:F0} kg";
                
                if (isOverCapacity)
                {
                    cargoWeightText.color = overCapacityColor;
                }
                else if (cargoWeight > cargoMaxWeight * 0.9f)
                {
                    cargoWeightText.color = warningColor;
                }
                else
                {
                    cargoWeightText.color = Color.white;
                }
            }
        }

        private void OnDepositClicked(ResourceType resourceType)
        {
            if (Time.time - lastTransferTime < transferCooldown) return;
            lastTransferTime = Time.time;

            dataBridge?.RequestTransfer(resourceType, 1); // Deposit 1
            RefreshUI();
        }

        private void OnWithdrawClicked(ResourceType resourceType)
        {
            if (Time.time - lastTransferTime < transferCooldown) return;
            lastTransferTime = Time.time;

            dataBridge?.RequestTransfer(resourceType, -1); // Withdraw 1
            RefreshUI();
        }

        // Static helper to create UI at runtime
        public static CargoUIPanel CreateDefaultUI()
        {
            // Create Canvas
            var canvasGO = new GameObject("CargoUICanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Create Panel
            var panelGO = new GameObject("CargoPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.2f, 0.15f);
            panelRect.anchorMax = new Vector2(0.8f, 0.85f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            var uiPanel = canvasGO.AddComponent<CargoUIPanel>();
            uiPanel.panelRoot = panelGO;

            return uiPanel;
        }
    }
}
