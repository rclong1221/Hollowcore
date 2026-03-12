/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Swimming
{
    using Opsive.Shared.StateSystem;
    using System;
    using UnityEngine;

    // See Opsive.UltimateCharacterController.StateSystem.AOTLinker for an explanation of this class.
    public class AOTLinker : MonoBehaviour
    {
        public void Linker()
        {
#pragma warning disable 0219
            var waterHeightDetectionGenericDelegate = new Preset.GenericDelegate<Swim.WaterHeightDetection>();
            var waterHeightDetectionFuncDelegate = new Func<Swim.WaterHeightDetection>(() => { return 0; });
            var waterHeightDetectionActionDelegate = new Action<Swim.WaterHeightDetection>((Swim.WaterHeightDetection value) => { });
#pragma warning restore 0219
        }
    }
}
