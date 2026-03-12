/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Swimming.Editor
{
    using Opsive.Shared.Editor.Managers;
    using Opsive.UltimateCharacterController.Editor.Managers;

    /// <summary>
    /// Draws the inspector for the swimming add-on.
    /// </summary>
    [OrderedEditorItem("Swimming Pack", 3)]
    public class SwimmingAddOnInspector : AbilityAddOnInspector
    {
        public override string AddOnName { get { return "Swimming"; } }
        public override string AbilityNamespace { get { return "Opsive.UltimateCharacterController.AddOns.Swimming"; } }
        public override bool ShowFirstPersonAnimatorController { get { return false; } }
    }
}