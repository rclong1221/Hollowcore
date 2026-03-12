using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace DIG.Ship.Cargo.UI
{
    /// <summary>
    /// Individual row in the cargo UI showing resource type, quantity, and transfer button.
    /// </summary>
    public class CargoItemRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI resourceNameText;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private Button transferButton;
        [SerializeField] private TextMeshProUGUI buttonText;

        private Action onClick;

        public void Setup(string resourceName, int quantity, bool isInventory, Action onTransferClick)
        {
            onClick = onTransferClick;

            if (resourceNameText != null)
                resourceNameText.text = resourceName;

            if (quantityText != null)
                quantityText.text = quantity.ToString();

            if (buttonText != null)
                buttonText.text = isInventory ? "→" : "←"; // Deposit or Withdraw arrow

            if (transferButton != null)
            {
                transferButton.onClick.RemoveAllListeners();
                transferButton.onClick.AddListener(() => onClick?.Invoke());
            }
        }

        /// <summary>
        /// Creates a default row with all required components.
        /// </summary>
        public static GameObject CreateDefaultRowPrefab()
        {
            var rowGO = new GameObject("CargoItemRow");
            var rowRect = rowGO.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 40);
            
            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var rowBG = rowGO.AddComponent<Image>();
            rowBG.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);

            // Resource Name
            var nameGO = new GameObject("ResourceName");
            nameGO.transform.SetParent(rowGO.transform, false);
            var nameRect = nameGO.AddComponent<RectTransform>();
            var nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.text = "Resource";
            nameText.fontSize = 16;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.color = Color.white;
            var nameLayout = nameGO.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;

            // Quantity
            var qtyGO = new GameObject("Quantity");
            qtyGO.transform.SetParent(rowGO.transform, false);
            var qtyRect = qtyGO.AddComponent<RectTransform>();
            var qtyText = qtyGO.AddComponent<TextMeshProUGUI>();
            qtyText.text = "0";
            qtyText.fontSize = 16;
            qtyText.alignment = TextAlignmentOptions.Center;
            qtyText.color = Color.white;
            var qtyLayout = qtyGO.AddComponent<LayoutElement>();
            qtyLayout.preferredWidth = 60;

            // Transfer Button
            var btnGO = new GameObject("TransferButton");
            btnGO.transform.SetParent(rowGO.transform, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            var btnImage = btnGO.AddComponent<Image>();
            btnImage.color = new Color(0.3f, 0.5f, 0.3f, 1f);
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImage;
            var btnLayout = btnGO.AddComponent<LayoutElement>();
            btnLayout.preferredWidth = 40;

            var btnTextGO = new GameObject("ButtonText");
            btnTextGO.transform.SetParent(btnGO.transform, false);
            var btnTextRect = btnTextGO.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
            var btnText = btnTextGO.AddComponent<TextMeshProUGUI>();
            btnText.text = "→";
            btnText.fontSize = 20;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            // Add row component
            var row = rowGO.AddComponent<CargoItemRow>();
            row.resourceNameText = nameText;
            row.quantityText = qtyText;
            row.transferButton = btn;
            row.buttonText = btnText;

            return rowGO;
        }
    }
}
