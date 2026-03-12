using DIG.Tutorial.Bridge;
using DIG.UI.Tutorial;
using Unity.Entities;

namespace DIG.Tutorial.Systems
{
    /// <summary>
    /// EPIC 18.4: Managed SystemBase that reads triggered TutorialTriggerComponents
    /// and bridges them to TutorialService. Also drains TutorialVisualQueue.
    /// Runs in PresentationSystemGroup, after the Burst-compiled TutorialTriggerSystem.
    /// Does NOT modify the existing TutorialTriggerSystem.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class TutorialTriggerEventSystem : SystemBase
    {

        protected override void OnUpdate()
        {
            if (!TutorialService.HasInstance) return;
            var service = TutorialService.Instance;

            CompleteDependency();

            // Iterate triggered tutorial triggers without temp allocations
            Entities
                .WithoutBurst()
                .ForEach((Entity entity, ref TutorialTriggerComponent trigger) =>
                {
                    if (!trigger.Triggered || trigger.Processed) return;

                    string sequenceId = trigger.SequenceId.ToString();
                    if (!string.IsNullOrEmpty(sequenceId))
                        service.StartTutorial(sequenceId);

                    trigger.Processed = true;
                }).Run();

            // Drain TutorialVisualQueue (for ECS systems that enqueue tutorial starts)
            while (TutorialVisualQueue.TryDequeue(out var evt))
            {
                string seqId = evt.SequenceId.ToString();
                if (!string.IsNullOrEmpty(seqId))
                    service.StartTutorial(seqId);
            }
        }
    }
}
