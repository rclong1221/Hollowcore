using System;
using DIG.Identity;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Individual player slot widget in the lobby room.
    /// Shows player info, ready state, and host kick button.
    /// </summary>
    public class LobbyPlayerSlotUI : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _levelText;
        [SerializeField] private Image _readyIndicator;
        [SerializeField] private Image _hostCrown;
        [SerializeField] private Text _pingText;
        [SerializeField] private GameObject _emptyState;
        [SerializeField] private GameObject _occupiedState;

        [Header("Avatar (EPIC 17.14)")]
        [SerializeField] private RawImage _avatar;

        [Header("Host Controls")]
        [SerializeField] private Button _kickButton;

        [Header("Colors")]
        [SerializeField] private Color _readyColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _notReadyColor = new Color(0.8f, 0.2f, 0.2f);

        private int _slotIndex;
        private string _loadingAvatarId;

        private void OnEnable()
        {
            if (_kickButton != null) _kickButton.onClick.AddListener(OnKickClicked);
        }

        private void OnDisable()
        {
            if (_kickButton != null) _kickButton.onClick.RemoveListener(OnKickClicked);
        }

        public void SetSlot(LobbyPlayerSlot slot, bool showKick)
        {
            _slotIndex = slot.SlotIndex;

            if (_emptyState != null) _emptyState.SetActive(false);
            if (_occupiedState != null) _occupiedState.SetActive(true);

            if (_nameText != null) _nameText.text = slot.DisplayName ?? "Unknown";
            if (_levelText != null) _levelText.text = $"Lv.{slot.Level}";
            if (_pingText != null) _pingText.text = slot.ConnectionId >= 0 ? $"{slot.PingMs}ms" : "Local";

            if (_readyIndicator != null)
            {
                _readyIndicator.color = slot.IsReady ? _readyColor : _notReadyColor;
                _readyIndicator.gameObject.SetActive(!slot.IsHost);
            }

            if (_hostCrown != null) _hostCrown.gameObject.SetActive(slot.IsHost);
            if (_kickButton != null) _kickButton.gameObject.SetActive(showKick);

            // EPIC 17.14: Avatar
            if (_avatar != null && !string.IsNullOrEmpty(slot.PlayerId))
            {
                if (AvatarCache.TryGet(slot.PlayerId, out var tex))
                {
                    _avatar.texture = tex;
                    _avatar.gameObject.SetActive(true);
                }
                else if (_loadingAvatarId != slot.PlayerId)
                {
                    _avatar.gameObject.SetActive(false);
                    LoadAvatarAsync(slot.PlayerId);
                }
            }
            else if (_avatar != null)
            {
                _avatar.gameObject.SetActive(false);
            }
        }

        private async void LoadAvatarAsync(string platformId)
        {
            _loadingAvatarId = platformId;

            var mgr = IdentityManager.Instance;
            if (mgr == null || !mgr.IsReady || !mgr.ActiveProvider.SupportsAvatars) return;

            var tex = await AvatarCache.GetOrLoadAsync(mgr.ActiveProvider, platformId);

            if (_loadingAvatarId != platformId) return;

            if (tex != null && _avatar != null)
            {
                _avatar.texture = tex;
                _avatar.gameObject.SetActive(true);
            }
            _loadingAvatarId = null;
        }

        public void SetEmpty()
        {
            if (_emptyState != null) _emptyState.SetActive(true);
            if (_occupiedState != null) _occupiedState.SetActive(false);
            if (_kickButton != null) _kickButton.gameObject.SetActive(false);
            if (_avatar != null) _avatar.gameObject.SetActive(false);
        }

        private void OnKickClicked()
        {
            LobbyManager.Instance?.KickPlayer(_slotIndex);
        }
    }
}
