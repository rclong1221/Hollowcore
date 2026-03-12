using Unity.Entities;
using UnityEngine;

namespace DIG.Core.Input
{
    /// <summary>
    /// Authoring component that adds InputParadigmState to the player entity.
    /// Add this to the player prefab alongside other input components.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public class InputParadigmStateAuthoring : MonoBehaviour
    {
        [Header("Default Values")]
        [Tooltip("Default paradigm when entity is created.")]
        public InputParadigm defaultParadigm = InputParadigm.Shooter;

        [Tooltip("Default facing mode when entity is created.")]
        public MovementFacingMode defaultFacingMode = MovementFacingMode.CameraForward;

        public class Baker : Baker<InputParadigmStateAuthoring>
        {
            public override void Bake(InputParadigmStateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new InputParadigmState
                {
                    ActiveParadigm = authoring.defaultParadigm,
                    FacingMode = authoring.defaultFacingMode,
                    IsClickToMoveEnabled = false,
                    ClickToMoveButton = ClickToMoveButton.None,
                    ActiveModeOverlay = InputModeOverlay.None,
                });
            }
        }
    }
}
