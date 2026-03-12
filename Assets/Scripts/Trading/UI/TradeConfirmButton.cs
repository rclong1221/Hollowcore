using UnityEngine;
using UnityEngine.UI;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Confirm/Cancel button for trade window.
    /// States: Ready ("Confirm"), Waiting ("Waiting..."), Trading ("Trading...").
    /// </summary>
    public class TradeConfirmButton : MonoBehaviour
    {
        public enum ConfirmState
        {
            Ready,
            Waiting,
            Trading
        }

        [SerializeField] private Button _confirmButton;
        [SerializeField] private Text _confirmText;
        [SerializeField] private string _readyLabel = "Confirm";
        [SerializeField] private string _waitingLabel = "Waiting...";
        [SerializeField] private string _tradingLabel = "Trading...";

        private ConfirmState _state = ConfirmState.Ready;

        private void OnEnable()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);
            SetState(ConfirmState.Ready);
        }

        private void OnDisable()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }

        public void SetState(ConfirmState state)
        {
            _state = state;
            if (_confirmButton != null)
                _confirmButton.interactable = state == ConfirmState.Ready;

            if (_confirmText != null)
            {
                _confirmText.text = state switch
                {
                    ConfirmState.Ready => _readyLabel,
                    ConfirmState.Waiting => _waitingLabel,
                    ConfirmState.Trading => _tradingLabel,
                    _ => _readyLabel
                };
            }
        }

        private void OnConfirmClicked()
        {
            if (_state != ConfirmState.Ready) return;
            TradeRpcHelper.SendConfirm();
            SetState(ConfirmState.Waiting);
        }
    }
}
