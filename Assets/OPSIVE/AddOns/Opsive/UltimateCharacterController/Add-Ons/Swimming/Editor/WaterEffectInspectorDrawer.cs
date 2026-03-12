/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Swimming.Editor
{
    using Opsive.Shared.Audio;
    using Opsive.Shared.Editor.Inspectors;
    using Opsive.UltimateCharacterController.Editor.Inspectors.Utility;
    using Opsive.UltimateCharacterController.Editor.Inspectors.Audio;
    using System.Collections.Generic;
    using UnityEditorInternal;
    using UnityEngine;

    /// <summary>
    /// Draws a custom inspector for the base WaterEffect type.
    /// </summary>
    [InspectorDrawer(typeof(WaterEffect))]
    public class WaterEffectInspectorDrawer : InspectorDrawer
    {
        private Dictionary<object, ReorderableList> m_AudioClipSetReorderableList = new Dictionary<object, ReorderableList>();

        /// <summary>
        /// Called when the object should be drawn to the inspector.
        /// </summary>
        /// <param name="target">The object that is being drawn.</param>
        /// <param name="parent">The Unity Object that the object belongs to.</param>
        public override void OnInspectorGUI(object target, Object parent)
        {
            InspectorUtility.DrawField(target, "m_ParticlePrefab");
            if (target is WaterEffectVelocityEvent) {
                InspectorUtility.DrawField(target, "m_EventName");
            }
            if (target is WaterEffectVelocityEventDistance) {
                InspectorUtility.DrawField(target, "m_MaxSurfaceDistance"); 
            }
            if (target is WaterEffectVelocity) {
                InspectorUtility.DrawField(target, "m_MinVelocity");
            }
            // The swim ability only has one object of type WaterEffectVelocity (not inherited) and that object should not draw the location.
            if (target.GetType() != typeof(WaterEffectVelocity)) {
                InspectorUtility.DrawField(target, "m_Location");
            }

            // Draw the audio clips within a reorderable list.
            ReorderableList audioClipSetList;
            m_AudioClipSetReorderableList.TryGetValue(target, out audioClipSetList);
            var audioClipSet = InspectorUtility.GetFieldValue<AudioClipSet>(target, "m_AudioClipSet");
            audioClipSetList = AudioClipSetInspector.DrawAudioClipSet(audioClipSet, audioClipSetList,
                (Rect rect, int index, bool isActive, bool isFocused) => { AudioClipSetInspector.OnAudioClipDraw(audioClipSetList, rect, index, audioClipSet, null); },
                (ReorderableList list) => { AudioClipSetInspector.OnAudioClipListAdd(list, audioClipSet, null); },
                (ReorderableList list) => {
                    AudioClipSetInspector.OnAudioClipListRemove(list, audioClipSet, null);
                    InspectorUtility.SetFieldValue(audioClipSet, "m_AudioClips", (AudioClip[])list.list);
                });

            if (audioClipSetList != null && !m_AudioClipSetReorderableList.ContainsKey(target)) {
                m_AudioClipSetReorderableList.Add(target, audioClipSetList);
            }
        }
    }
}