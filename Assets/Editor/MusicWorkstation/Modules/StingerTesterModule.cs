#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace DIG.Music.Editor
{
    /// <summary>
    /// EPIC 17.5: List all stingers with Play button, priority/overlap testing, duck preview.
    /// </summary>
    public class StingerTesterModule : IMusicWorkstationModule
    {
        public string ModuleName => "Stinger Tester";

        private MusicDatabaseSO _database;
        private Vector2 _scroll;
        private bool _needsRefresh = true;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Stinger Tester", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _database = EditorGUILayout.ObjectField("Database", _database, typeof(MusicDatabaseSO), false) as MusicDatabaseSO;

            if (_database == null)
            {
                // Try auto-load
                _database = Resources.Load<MusicDatabaseSO>("MusicDatabase");
                if (_database == null)
                {
                    EditorGUILayout.HelpBox("Assign a MusicDatabaseSO or create one at Resources/MusicDatabase.", MessageType.Info);
                    return;
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Stingers: {_database.Stingers.Count}", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _database.Stingers.Count; i++)
            {
                var stinger = _database.Stingers[i];
                if (stinger == null) continue;

                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"[{stinger.StingerId}]", GUILayout.Width(40));
                EditorGUILayout.LabelField(stinger.StingerName, GUILayout.Width(150));
                EditorGUILayout.LabelField(stinger.Category.ToString(), GUILayout.Width(100));
                EditorGUILayout.LabelField($"P:{stinger.DefaultPriority}", GUILayout.Width(40));
                EditorGUILayout.LabelField($"Duck:{stinger.DuckMusicDB:F0}dB", GUILayout.Width(80));

                if (stinger.Clip != null)
                {
                    EditorGUILayout.LabelField($"{stinger.Clip.length:F1}s", GUILayout.Width(40));
                    if (GUILayout.Button("Play", GUILayout.Width(50)))
                        PlayClipPreview(stinger.Clip);
                    if (GUILayout.Button("Stop", GUILayout.Width(40)))
                        StopPreview();
                }
                else
                {
                    EditorGUILayout.LabelField("(no clip)", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            // Priority reference
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Priority Reference", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Death={StingerPriority.Death} BossIntro={StingerPriority.BossIntro} LevelUp={StingerPriority.LevelUp} Quest={StingerPriority.QuestComplete} Achievement={StingerPriority.Achievement} RareItem={StingerPriority.RareItem} Discovery={StingerPriority.Discovery}",
                EditorStyles.miniLabel);
        }

        private void PlayClipPreview(AudioClip clip)
        {
            var asm = typeof(AudioImporter).Assembly;
            var util = asm.GetType("UnityEditor.AudioUtil");
            if (util != null)
            {
                var method = util.GetMethod("PlayPreviewClip",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                    null, new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
                method?.Invoke(null, new object[] { clip, 0, false });
            }
        }

        private void StopPreview()
        {
            var asm = typeof(AudioImporter).Assembly;
            var util = asm.GetType("UnityEditor.AudioUtil");
            if (util != null)
            {
                var method = util.GetMethod("StopAllPreviewClips",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                method?.Invoke(null, null);
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
