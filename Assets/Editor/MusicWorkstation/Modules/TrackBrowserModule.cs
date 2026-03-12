#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace DIG.Music.Editor
{
    /// <summary>
    /// EPIC 17.5: Browse, search, and filter all MusicTrackSO assets.
    /// Preview individual stems with waveform visualization.
    /// </summary>
    public class TrackBrowserModule : IMusicWorkstationModule
    {
        public string ModuleName => "Track Browser";

        private List<MusicTrackSO> _tracks = new List<MusicTrackSO>();
        private string _searchFilter = "";
        private MusicTrackCategory? _categoryFilter;
        private Vector2 _listScroll;
        private MusicTrackSO _selectedTrack;
        private bool _needsRefresh = true;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Track Browser", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Toolbar
            EditorGUILayout.BeginHorizontal();
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter);
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                _needsRefresh = true;
            EditorGUILayout.EndHorizontal();

            // Category filter
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Category", GUILayout.Width(60));
            if (GUILayout.Button("All", _categoryFilter == null ? EditorStyles.toolbarButton : EditorStyles.miniButton, GUILayout.Width(50)))
                _categoryFilter = null;
            foreach (MusicTrackCategory cat in System.Enum.GetValues(typeof(MusicTrackCategory)))
            {
                if (GUILayout.Button(cat.ToString(), _categoryFilter == cat ? EditorStyles.toolbarButton : EditorStyles.miniButton, GUILayout.Width(80)))
                    _categoryFilter = cat;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (_needsRefresh)
            {
                RefreshTracks();
                _needsRefresh = false;
            }

            EditorGUILayout.Space(8);

            // Track list
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(200));
            for (int i = 0; i < _tracks.Count; i++)
            {
                var track = _tracks[i];
                if (track == null) continue;

                // Filter
                if (!string.IsNullOrEmpty(_searchFilter) &&
                    !track.TrackName.ToLower().Contains(_searchFilter.ToLower()))
                    continue;

                if (_categoryFilter.HasValue && track.Category != _categoryFilter.Value)
                    continue;

                bool isSelected = _selectedTrack == track;
                var style = isSelected ? EditorStyles.toolbarButton : EditorStyles.label;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"[{track.TrackId}] {track.TrackName}", style))
                    _selectedTrack = track;
                EditorGUILayout.LabelField(track.Category.ToString(), GUILayout.Width(80));
                EditorGUILayout.LabelField($"{track.BPM:F0} BPM", GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            // Selected track details
            if (_selectedTrack != null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Track Details", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Name: {_selectedTrack.TrackName}");
                EditorGUILayout.LabelField($"ID: {_selectedTrack.TrackId} | Category: {_selectedTrack.Category} | BPM: {_selectedTrack.BPM:F0}");
                EditorGUILayout.LabelField($"Base Volume: {_selectedTrack.BaseVolume:F2}");
                EditorGUILayout.LabelField($"Thresholds: Perc={_selectedTrack.CombatIntensityThresholds.x:F2} Mel={_selectedTrack.CombatIntensityThresholds.y:F2} Int={_selectedTrack.CombatIntensityThresholds.z:F2}");

                EditorGUILayout.Space(4);
                StemRow("Base Stem", _selectedTrack.BaseStem);
                StemRow("Percussion", _selectedTrack.PercussionStem);
                StemRow("Melody", _selectedTrack.MelodyStem);
                StemRow("Intensity", _selectedTrack.IntensityStem);
                StemRow("Intro", _selectedTrack.IntroClip);

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Select in Project", GUILayout.Width(150)))
                    Selection.activeObject = _selectedTrack;
            }
            else
            {
                EditorGUILayout.HelpBox("Select a track above to view details.", MessageType.Info);
            }
        }

        private void StemRow(string label, AudioClip clip)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(100));
            if (clip != null)
            {
                EditorGUILayout.LabelField($"{clip.name} ({clip.length:F1}s, {clip.channels}ch)", GUILayout.Width(250));
                if (GUILayout.Button("Preview", GUILayout.Width(60)))
                    PlayClipPreview(clip);
            }
            else
            {
                EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void PlayClipPreview(AudioClip clip)
        {
            // Use Unity's internal clip preview utility
            var unityEditorAssembly = typeof(AudioImporter).Assembly;
            var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilClass != null)
            {
                var method = audioUtilClass.GetMethod("PlayPreviewClip",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                    null, new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
                method?.Invoke(null, new object[] { clip, 0, false });
            }
        }

        private void RefreshTracks()
        {
            _tracks.Clear();
            var guids = AssetDatabase.FindAssets("t:MusicTrackSO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var track = AssetDatabase.LoadAssetAtPath<MusicTrackSO>(path);
                if (track != null)
                    _tracks.Add(track);
            }
            _tracks.Sort((a, b) => a.TrackId.CompareTo(b.TrackId));
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
