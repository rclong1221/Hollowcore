using UnityEngine;
using UnityEngine.UI;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Main trade window MonoBehaviour implementing ITradeUIProvider.
    /// Two panels: My Offer and Their Offer. Confirm/Cancel buttons.
    /// Registers with TradeUIRegistry on enable.
    /// </summary>
    public class TradeWindowView : MonoBehaviour, ITradeUIProvider
    {
        [Header("Panels")]
        [SerializeField] private TradeOfferPanel _myOfferPanel;
        [SerializeField] private TradeOfferPanel _theirOfferPanel;

        [Header("Buttons")]
        [SerializeField] private TradeConfirmButton _confirmButton;
        [SerializeField] private Button _cancelButton;

        [Header("Request Popup")]
        [SerializeField] private GameObject _requestPopup;
        [SerializeField] private Text _requestText;
        [SerializeField] private Button _acceptButton;
        [SerializeField] private Button _declineButton;

        [Header("Window")]
        [SerializeField] private GameObject _tradeWindow;

        private void OnEnable()
        {
            TradeUIRegistry.Register(this);

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnCancelClicked);
            if (_acceptButton != null)
                _acceptButton.onClick.AddListener(OnAcceptClicked);
            if (_declineButton != null)
                _declineButton.onClick.AddListener(OnDeclineClicked);

            if (_tradeWindow != null) _tradeWindow.SetActive(false);
            if (_requestPopup != null) _requestPopup.SetActive(false);
        }

        private void OnDisable()
        {
            TradeUIRegistry.Unregister(this);

            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(OnCancelClicked);
            if (_acceptButton != null)
                _acceptButton.onClick.RemoveListener(OnAcceptClicked);
            if (_declineButton != null)
                _declineButton.onClick.RemoveListener(OnDeclineClicked);
        }

        // ═══════ ITradeUIProvider ═══════

        public void OnTradeRequested(int requesterGhostId)
        {
            if (_requestPopup != null) _requestPopup.SetActive(true);
            if (_requestText != null) _requestText.text = $"Trade request from player #{requesterGhostId}";
        }

        public void OnTradeSessionStarted()
        {
            if (_requestPopup != null) _requestPopup.SetActive(false);
            if (_tradeWindow != null) _tradeWindow.SetActive(true);
            if (_myOfferPanel != null) _myOfferPanel.ClearOffers();
            if (_theirOfferPanel != null) _theirOfferPanel.ClearOffers();
            if (_confirmButton != null) _confirmButton.SetState(TradeConfirmButton.ConfirmState.Ready);
        }

        public void OnTradeSessionCancelled(TradeCancelReason reason)
        {
            CloseAll();
#if UNITY_EDITOR
            Debug.Log($"[TradeWindow] Trade cancelled: {reason}");
#endif
        }

        public void OnTradeCompleted(bool success)
        {
            CloseAll();
#if UNITY_EDITOR
            Debug.Log($"[TradeWindow] Trade completed: success={success}");
#endif
        }

        public void OnOfferUpdated(TradeOfferSnapshot[] myOffers, int myCount, TradeOfferSnapshot[] theirOffers, int theirCount)
        {
            if (_myOfferPanel != null) _myOfferPanel.DisplayOffers(myOffers, myCount);
            if (_theirOfferPanel != null) _theirOfferPanel.DisplayOffers(theirOffers, theirCount);
        }

        public void OnConfirmStateChanged(bool iConfirmed, bool theyConfirmed)
        {
            if (_confirmButton == null) return;

            if (iConfirmed && theyConfirmed)
                _confirmButton.SetState(TradeConfirmButton.ConfirmState.Trading);
            else if (iConfirmed)
                _confirmButton.SetState(TradeConfirmButton.ConfirmState.Waiting);
            else
                _confirmButton.SetState(TradeConfirmButton.ConfirmState.Ready);
        }

        // ═══════ Button Handlers ═══════

        private void OnCancelClicked()
        {
            // Send TradeCancelRpc via World
            TradeRpcHelper.SendCancel(TradeCancelReason.Voluntary);
            CloseAll();
        }

        private void OnAcceptClicked()
        {
            TradeRpcHelper.SendAccept();
            if (_requestPopup != null) _requestPopup.SetActive(false);
        }

        private void OnDeclineClicked()
        {
            TradeRpcHelper.SendCancel(TradeCancelReason.Voluntary);
            if (_requestPopup != null) _requestPopup.SetActive(false);
        }

        private void CloseAll()
        {
            if (_tradeWindow != null) _tradeWindow.SetActive(false);
            if (_requestPopup != null) _requestPopup.SetActive(false);
        }
    }

    /// <summary>
    /// EPIC 17.3: Static helper for sending trade RPCs from MonoBehaviours.
    /// Finds the client world and creates RPC entities.
    /// </summary>
    public static class TradeRpcHelper
    {
        public static void SendAccept()
        {
            var world = GetClientWorld();
            if (world == null) return;
            var em = world.EntityManager;
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new TradeAcceptRpc());
            em.AddComponentData(entity, new Unity.NetCode.SendRpcCommandRequest());
        }

        public static void SendConfirm()
        {
            var world = GetClientWorld();
            if (world == null) return;
            var em = world.EntityManager;
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new TradeConfirmRpc());
            em.AddComponentData(entity, new Unity.NetCode.SendRpcCommandRequest());
        }

        public static void SendCancel(TradeCancelReason reason)
        {
            var world = GetClientWorld();
            if (world == null) return;
            var em = world.EntityManager;
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new TradeCancelRpc { Reason = reason });
            em.AddComponentData(entity, new Unity.NetCode.SendRpcCommandRequest());
        }

        public static void SendOfferUpdate(TradeOfferAction action, TradeOfferType offerType,
            byte itemSlotIndex, int quantity, DIG.Economy.CurrencyType currencyType, int currencyAmount)
        {
            var world = GetClientWorld();
            if (world == null) return;
            var em = world.EntityManager;
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new TradeOfferUpdateRpc
            {
                Action = action,
                OfferType = offerType,
                ItemSlotIndex = itemSlotIndex,
                Quantity = quantity,
                CurrencyType = currencyType,
                CurrencyAmount = currencyAmount
            });
            em.AddComponentData(entity, new Unity.NetCode.SendRpcCommandRequest());
        }

        private static Unity.Entities.World GetClientWorld()
        {
            foreach (var world in Unity.Entities.World.All)
            {
                if (world.Name.Contains("Client") || world.Name == "Game")
                    return world;
            }
            // Fall back to local simulation world
            foreach (var world in Unity.Entities.World.All)
            {
                if (world.Name.Contains("Local"))
                    return world;
            }
            return null;
        }
    }
}
