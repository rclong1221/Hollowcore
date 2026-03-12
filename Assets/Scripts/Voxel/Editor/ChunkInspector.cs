using UnityEngine;
using UnityEditor;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Custom inspector for chunk GameObjects.
    /// Shows voxel data statistics when selected in scene.
    /// </summary>
    [CustomEditor(typeof(MeshFilter))]
    public class ChunkMeshInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            var meshFilter = (MeshFilter)target;
            // Only affect Chunk objects
            if (!meshFilter.gameObject.name.Contains("Chunk_"))
                return;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Voxel Chunk Info", EditorStyles.boldLabel);
            
            var mesh = meshFilter.sharedMesh;
            if (mesh != null)
            {
                EditorGUILayout.LabelField("Vertices:", mesh.vertexCount.ToString());
                EditorGUILayout.LabelField("Triangles:", (mesh.triangles.Length / 3).ToString());
                EditorGUILayout.LabelField("Bounds:", mesh.bounds.size.ToString());
            }
            
            // Check for MeshCollider stats
            var collider = meshFilter.GetComponent<MeshCollider>();
            if (collider != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Collider", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Enabled:", collider.enabled.ToString());
                EditorGUILayout.LabelField("Has Mesh:", (collider.sharedMesh != null).ToString());
                EditorGUILayout.LabelField("Convex:", collider.convex.ToString());
            }
        }
    }
}
