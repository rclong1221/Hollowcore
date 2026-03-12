using System.Collections.Generic;
using UnityEngine;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: MonoBehaviour placed on crafting station GameObjects.
    /// Registers with static dictionary by SessionID for ECS bridge access.
    /// UI implementations override virtual methods.
    /// </summary>
    public class CraftingUILink : MonoBehaviour
    {
        [Tooltip("Must match InteractionSession.SessionID on this station.")]
        [SerializeField] private int _sessionId;

        private static readonly Dictionary<int, CraftingUILink> s_Registry = new();

        public static CraftingUILink Get(int sessionId)
        {
            s_Registry.TryGetValue(sessionId, out var link);
            return link;
        }

        private void OnEnable()
        {
            s_Registry[_sessionId] = this;
        }

        private void OnDisable()
        {
            if (s_Registry.TryGetValue(_sessionId, out var link) && link == this)
                s_Registry.Remove(_sessionId);
        }

        public virtual void OpenCraftingUI(StationType stationType, byte stationTier)
        {
            Debug.Log($"[CraftingUI] Opening crafting UI for {stationType} T{stationTier} (SessionID={_sessionId})");
        }

        public virtual void CloseCraftingUI()
        {
            Debug.Log($"[CraftingUI] Closing crafting UI (SessionID={_sessionId})");
        }

        public virtual void RefreshQueue(CraftQueueEntry[] entries) { }

        public virtual void RefreshOutputs(CraftOutputEntry[] entries) { }
    }

    /// <summary>
    /// EPIC 16.13: UI-friendly queue entry data.
    /// </summary>
    public struct CraftQueueEntry
    {
        public int RecipeId;
        public string DisplayName;
        public CraftState State;
        public float Progress; // 0-1
        public float TimeRemaining;
    }

    /// <summary>
    /// EPIC 16.13: UI-friendly output entry data.
    /// </summary>
    public struct CraftOutputEntry
    {
        public int RecipeId;
        public string DisplayName;
        public int Quantity;
        public int OutputIndex;
    }
}
