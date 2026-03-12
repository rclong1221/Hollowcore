using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.UI.Tutorial
{
    public struct TutorialTriggerComponent : IComponentData
    {
        public FixedString64Bytes Header;
        public FixedString512Bytes Message;
        public FixedString64Bytes SequenceId;
        public bool OneTime;
        public bool Triggered;
        public bool Processed;
    }

    public class TutorialTriggerAuthoring : MonoBehaviour
    {
        public string Header = "Tutorial Header";
        [TextArea]
        public string Message = "Tutorial Message";
        [Tooltip("Tutorial sequence ID to start when triggered. Links to TutorialSequenceSO.SequenceId.")]
        public string SequenceId;
        public bool OneTime = true;

        class Baker : Baker<TutorialTriggerAuthoring>
        {
            public override void Bake(TutorialTriggerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new TutorialTriggerComponent
                {
                    Header = new FixedString64Bytes(authoring.Header),
                    Message = new FixedString512Bytes(authoring.Message),
                    SequenceId = string.IsNullOrEmpty(authoring.SequenceId)
                        ? default
                        : new FixedString64Bytes(authoring.SequenceId),
                    OneTime = authoring.OneTime,
                    Triggered = false,
                    Processed = false,
                });
            }
        }
    }
}
