using UnityEngine;
using UnityEditor;
using DIG.Voxel.Rendering;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Custom editor for VoxelVisualMaterial with texture auto-detection and preview.
    /// </summary>
    [CustomEditor(typeof(VoxelVisualMaterial))]
    public class MaterialVisualEditor : UnityEditor.Editor
    {
        private const int PREVIEW_SIZE = 64;
        private PreviewRenderUtility _previewUtility;
        private Material _previewMaterial;
        private Mesh _previewMesh;
        
        public override void OnInspectorGUI()
        {
            var mat = (VoxelVisualMaterial)target;
            
            // Drag-drop area for texture auto-assignment
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Quick Texture Setup", EditorStyles.boldLabel);
            
            var dropArea = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drop textures here - auto-assigned by suffix\n(_albedo, _normal, _height, _detail)", EditorStyles.helpBox);
            
            HandleTextureDrop(dropArea, mat);
            
            EditorGUILayout.Space(10);
            
            // Texture preview grid
            DrawTexturePreviewGrid(mat);
            
            EditorGUILayout.Space(10);
            
            // Standard inspector
            base.OnInspectorGUI();
            
            EditorGUILayout.Space(10);
            
            // Actions
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All Textures"))
            {
                ClearTextures(mat);
            }
            if (GUILayout.Button("Generate Preview Icon"))
            {
                GeneratePreviewIcon(mat);
            }
            EditorGUILayout.EndHorizontal();
            
            // Validation
            if (!mat.IsValid)
            {
                EditorGUILayout.HelpBox("Albedo texture is required!", MessageType.Warning);
            }
        }
        
        private void DrawTexturePreviewGrid(VoxelVisualMaterial mat)
        {
            EditorGUILayout.LabelField("Texture Previews", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            DrawTexturePreview("Albedo", mat.Albedo, true);
            DrawTexturePreview("Normal", mat.Normal, false);
            DrawTexturePreview("Height", mat.HeightMap, false);
            DrawTexturePreview("Detail", mat.DetailAlbedo, false);
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawTexturePreview(string label, Texture2D tex, bool required)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(PREVIEW_SIZE + 10));
            
            var rect = GUILayoutUtility.GetRect(PREVIEW_SIZE, PREVIEW_SIZE);
            
            if (tex != null)
            {
                EditorGUI.DrawPreviewTexture(rect, tex);
            }
            else
            {
                var color = required ? new Color(0.5f, 0.2f, 0.2f) : new Color(0.2f, 0.2f, 0.2f);
                EditorGUI.DrawRect(rect, color);
                GUI.Label(rect, "?", new GUIStyle(GUI.skin.label) 
                { 
                    alignment = TextAnchor.MiddleCenter, 
                    fontSize = 16,
                    normal = { textColor = Color.gray }
                });
            }
            
            var labelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField(label, labelStyle, GUILayout.Width(PREVIEW_SIZE));
            
            EditorGUILayout.EndVertical();
        }
        
        private void HandleTextureDrop(Rect dropArea, VoxelVisualMaterial mat)
        {
            Event evt = Event.current;
            
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (!dropArea.Contains(evt.mousePosition)) return;
                
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    Undo.RecordObject(mat, "Assign Textures");
                    
                    int assignedCount = 0;
                    
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Texture2D tex)
                        {
                            string name = tex.name.ToLower();
                            
                            if (name.Contains("albedo") || name.Contains("diffuse") || name.Contains("color") || name.Contains("_c"))
                            {
                                mat.Albedo = tex;
                                assignedCount++;
                            }
                            else if (name.Contains("normal") || name.Contains("nrm") || name.Contains("_n"))
                            {
                                mat.Normal = tex;
                                assignedCount++;
                            }
                            else if (name.Contains("height") || name.Contains("displacement") || name.Contains("bump") || name.Contains("_h"))
                            {
                                mat.HeightMap = tex;
                                assignedCount++;
                            }
                            else if (name.Contains("detail"))
                            {
                                if (name.Contains("normal") || name.Contains("nrm"))
                                    mat.DetailNormal = tex;
                                else
                                    mat.DetailAlbedo = tex;
                                assignedCount++;
                            }
                            else if (mat.Albedo == null)
                            {
                                // Default to albedo if no suffix detected and albedo is empty
                                mat.Albedo = tex;
                                assignedCount++;
                            }
                        }
                    }
                    
                    if (assignedCount > 0)
                    {
                        EditorUtility.SetDirty(mat);
                        UnityEngine.Debug.Log($"[MaterialVisualEditor] Auto-assigned {assignedCount} textures");
                    }
                    
                    evt.Use();
                }
            }
        }
        
        private void ClearTextures(VoxelVisualMaterial mat)
        {
            Undo.RecordObject(mat, "Clear Textures");
            mat.Albedo = null;
            mat.Normal = null;
            mat.HeightMap = null;
            mat.DetailAlbedo = null;
            mat.DetailNormal = null;
            EditorUtility.SetDirty(mat);
        }
        
        private void GeneratePreviewIcon(VoxelVisualMaterial mat)
        {
            if (mat.Albedo == null)
            {
                UnityEngine.Debug.LogWarning("[MaterialVisualEditor] Cannot generate preview without Albedo texture");
                return;
            }
            
            // Create a 64x64 preview from the center of the albedo
            int size = 64;
            var preview = new Texture2D(size, size, TextureFormat.RGBA32, false);
            
            // Sample from center of albedo
            RenderTexture rt = RenderTexture.GetTemporary(size, size);
            Graphics.Blit(mat.Albedo, rt);
            
            RenderTexture.active = rt;
            preview.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            preview.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            
            // Apply tint
            var pixels = preview.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] *= mat.Tint;
            }
            preview.SetPixels(pixels);
            preview.Apply();
            
            mat.PreviewIcon = preview;
            EditorUtility.SetDirty(mat);
            
            UnityEngine.Debug.Log("[MaterialVisualEditor] Preview icon generated");
        }
        
        private void OnDisable()
        {
            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }
        }
    }
}
