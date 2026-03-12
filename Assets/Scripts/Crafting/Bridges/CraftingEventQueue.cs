using System.Collections.Generic;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: UI event types for crafting feedback.
    /// </summary>
    public enum CraftingUIEventType : byte
    {
        CraftStarted = 0,
        CraftCompleted = 1,
        CraftFailed = 2,
        InsufficientIngredients = 3,
        RecipeUnlocked = 4,
        OutputCollected = 5
    }

    /// <summary>
    /// EPIC 16.13: A crafting UI event.
    /// </summary>
    public struct CraftingUIEvent
    {
        public CraftingUIEventType Type;
        public int RecipeId;
    }

    /// <summary>
    /// EPIC 16.13: Static queue bridging ECS crafting systems to managed UI.
    /// Follows QuestEventQueue / DamageVisualQueue pattern.
    /// </summary>
    public static class CraftingEventQueue
    {
        private static readonly Queue<CraftingUIEvent> _queue = new(8);

        public static void Enqueue(CraftingUIEventType type, int recipeId)
        {
            _queue.Enqueue(new CraftingUIEvent { Type = type, RecipeId = recipeId });
        }

        public static bool TryDequeue(out CraftingUIEvent evt)
        {
            if (_queue.Count > 0)
            {
                evt = _queue.Dequeue();
                return true;
            }
            evt = default;
            return false;
        }

        public static int Count => _queue.Count;

        public static void Clear() => _queue.Clear();
    }
}
