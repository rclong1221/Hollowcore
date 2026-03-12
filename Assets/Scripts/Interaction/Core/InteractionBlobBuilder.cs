using Unity.Collections;
using Unity.Entities;

namespace DIG.Interaction
{
    /// <summary>
    /// EPIC 16.1 Phase 3: Static builder for phase sequence and input sequence BlobAssets.
    /// Follows the same pattern as Voxel BlobAssetBuilder.
    /// </summary>
    public static class InteractionBlobBuilder
    {
        /// <summary>
        /// Create a PhaseSequenceBlob from an array of phase definitions.
        /// </summary>
        public static BlobAssetReference<PhaseSequenceBlob> CreatePhaseBlob(
            PhaseDefinition[] phases, bool resetOnFail, float totalTimeLimit)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PhaseSequenceBlob>();

            root.ResetOnFail = resetOnFail;
            root.TotalTimeLimit = totalTimeLimit;

            var blobPhases = builder.Allocate(ref root.Phases, phases.Length);
            for (int i = 0; i < phases.Length; i++)
            {
                blobPhases[i] = phases[i];
            }

            var result = builder.CreateBlobAssetReference<PhaseSequenceBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }

        /// <summary>
        /// Authoring data for a single input sequence (used by MultiPhaseAuthoring).
        /// </summary>
        public struct SequenceAuthoringData
        {
            public InputStep[] Steps;
            public float DefaultStepTimeout;
        }

        /// <summary>
        /// Create an InputSequenceBlob from authoring data.
        /// Handles nested BlobArray allocation (outer sequences, then inner steps per sequence).
        /// </summary>
        public static BlobAssetReference<InputSequenceBlob> CreateInputSequenceBlob(
            SequenceAuthoringData[] sequences)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<InputSequenceBlob>();

            var blobSequences = builder.Allocate(ref root.Sequences, sequences.Length);
            for (int i = 0; i < sequences.Length; i++)
            {
                blobSequences[i].DefaultStepTimeout = sequences[i].DefaultStepTimeout;

                var steps = builder.Allocate(ref blobSequences[i].Steps, sequences[i].Steps.Length);
                for (int j = 0; j < sequences[i].Steps.Length; j++)
                {
                    steps[j] = sequences[i].Steps[j];
                }
            }

            var result = builder.CreateBlobAssetReference<InputSequenceBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    }
}
