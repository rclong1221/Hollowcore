using UnityEngine;
using Audio.Systems;
#if UNITY_EDITOR
using Unity.Entities;
using UnityEditor;
#endif

// Lightweight placeholder for voxel surface support.
// This file provides authoring markers and a Baker scaffold so teams can later add
// per-chunk SurfaceMaterial BlobAsset baking without changing gameplay code.

public class VoxelSurfaceAuthoring : MonoBehaviour
{
    [Tooltip("Optional default surface material id for this chunk")]
    public SurfaceMaterial DefaultSurfaceMaterial;

    // Future fields: per-voxel tags, blob baking settings, LOD ranges
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(VoxelSurfaceAuthoring))]
public class VoxelSurfaceAuthoringEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("This is a lightweight scaffold. Implement chunk blob baking in a Baker when voxel data exists.", MessageType.Info);
    }
}
#endif
