using UnityEngine;

namespace DIG.Items.Interfaces
{
    /// <summary>
    /// Abstracts animation system communication.
    /// Implement this interface to support different animation backends
    /// (Opsive UCC, Animancer, generic Mecanim, etc.)
    /// </summary>
    public interface IAnimatorBridge
    {
        /// <summary>
        /// The Unity Animator component being controlled.
        /// </summary>
        Animator Animator { get; }
        
        /// <summary>
        /// Initialize the bridge with the target animator.
        /// </summary>
        void Initialize(Animator animator);
        
        /// <summary>
        /// Notify the animator that an item has been equipped in a slot.
        /// </summary>
        /// <param name="slotIndex">Equipment slot index (0 = MainHand, 1 = OffHand, etc.)</param>
        /// <param name="itemId">Animator ItemID for the equipped item, or 0 if empty</param>
        void SetEquippedItem(int slotIndex, int itemId);
        
        /// <summary>
        /// Trigger an action animation state change.
        /// </summary>
        /// <param name="slotIndex">Equipment slot index</param>
        /// <param name="stateIndex">State index (0=Idle, 1=Equip, 2=Use, 3=Block, 5=Unequip, etc.)</param>
        void TriggerAction(int slotIndex, int stateIndex);
        
        /// <summary>
        /// Set the movement animation set ID.
        /// </summary>
        /// <param name="movementSetId">Movement set ID for the animator</param>
        void SetMovementSet(int movementSetId);
        
        /// <summary>
        /// Get the current animation state index for a slot.
        /// </summary>
        int GetCurrentState(int slotIndex);
        
        /// <summary>
        /// Check if the current action animation has completed.
        /// </summary>
        bool IsAnimationComplete(int slotIndex);
        
        /// <summary>
        /// Cancel/interrupt the current action animation.
        /// </summary>
        void CancelAction(int slotIndex);
        
        /// <summary>
        /// Toggle aim state (for ranged weapons).
        /// </summary>
        void SetAimActive(bool isAiming);
        
        /// <summary>
        /// Toggle block state (for shields/melee).
        /// </summary>
        void SetBlocking(bool isBlocking);
        
        /// <summary>
        /// Set animator layer weight.
        /// </summary>
        void SetLayerWeight(string layerName, float weight);
        
        /// <summary>
        /// Update called each frame for continuous updates.
        /// </summary>
        void OnUpdate();
    }
}
