#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;

/// <summary>
/// Monitors Animator/SkinnedMeshRenderer/AnimatorRigBridge state for first N frames
/// and logs any changes. Attach alongside `AnimatorSpawnDebugger` for deeper runtime
/// diagnostics when spawned by NetCode.
/// </summary>
public class AnimatorVerboseLogger : MonoBehaviour
{
    [Tooltip("Number of frames to monitor after Start()")]
    public int monitorFrames = 10;

    Animator animator;
    SkinnedMeshRenderer smr;
    Player.Bridges.AnimatorRigBridge rigBridge;

    bool lastActive;
    bool lastAnimatorEnabled;
    int lastBonesCount;
    string lastRootBoneName;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);
        smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
        rigBridge = GetComponentInChildren<Player.Bridges.AnimatorRigBridge>(true);
    }

    void Start()
    {
        lastActive = gameObject.activeInHierarchy;
        lastAnimatorEnabled = animator != null ? animator.enabled : false;
        lastBonesCount = smr != null && smr.bones != null ? smr.bones.Length : 0;
        lastRootBoneName = smr != null && smr.rootBone != null ? smr.rootBone.name : "<none>";

        Debug.Log($"[AnimatorVerboseLogger:Start] GO='{gameObject.name}' active={lastActive} animatorEnabled={lastAnimatorEnabled} bones={lastBonesCount} rootBone={lastRootBoneName}", this);
        StartCoroutine(MonitorCoroutine());
    }

    IEnumerator MonitorCoroutine()
    {
        for (int i = 0; i < monitorFrames; i++)
        {
            yield return null;

            bool curActive = gameObject.activeInHierarchy;
            bool curAnimatorEnabled = animator != null ? animator.enabled : false;
            int curBones = smr != null && smr.bones != null ? smr.bones.Length : 0;
            string curRoot = smr != null && smr.rootBone != null ? smr.rootBone.name : "<none>";

            if (curActive != lastActive)
            {
                Debug.LogWarning($"[AnimatorVerboseLogger] active changed: {lastActive} -> {curActive} on '{gameObject.name}'", this);
                lastActive = curActive;
            }

            if (curAnimatorEnabled != lastAnimatorEnabled)
            {
                Debug.LogWarning($"[AnimatorVerboseLogger] animator.enabled changed: {lastAnimatorEnabled} -> {curAnimatorEnabled} on '{gameObject.name}'", this);
                lastAnimatorEnabled = curAnimatorEnabled;
            }

            if (curBones != lastBonesCount || curRoot != lastRootBoneName)
            {
                Debug.Log($"[AnimatorVerboseLogger] bones/root changed: bones {lastBonesCount}->{curBones} root '{lastRootBoneName}'->'{curRoot}' on '{gameObject.name}'", this);
                lastBonesCount = curBones;
                lastRootBoneName = curRoot;
            }

            // Also report rig bridge presence
            if (rigBridge == null)
            {
                Debug.Log($"[AnimatorVerboseLogger] AnimatorRigBridge NOT found on '{gameObject.name}' (child search)", this);
            }
        }
    }
}
#endif
