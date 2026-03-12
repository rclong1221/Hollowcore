using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Validation.Editor.Modules
{
    /// <summary>
    /// EPIC 17.11: Ban list management.
    /// List active bans, manual ban/kick/unban controls, import/export.
    /// </summary>
    public class BanManagerModule : IValidationWorkstationModule
    {
        public string ModuleName => "Ban Manager";

        private Vector2 _scroll;
        private int _banNetworkId;
        private int _banDurationMinutes = 30;
        private string _banReason = "Manual ban";
        private bool _permanentBan;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Ban List Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Manual ban controls
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Add Ban", EditorStyles.boldLabel);

            _banNetworkId = EditorGUILayout.IntField("Network ID", _banNetworkId);
            _banReason = EditorGUILayout.TextField("Reason", _banReason);
            _permanentBan = EditorGUILayout.Toggle("Permanent", _permanentBan);

            if (!_permanentBan)
                _banDurationMinutes = EditorGUILayout.IntField("Duration (minutes)", _banDurationMinutes);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Ban"))
            {
                if (_permanentBan)
                    BanListManager.AddPermaBan(_banNetworkId, _banReason);
                else
                    BanListManager.AddTempBan(_banNetworkId, _banDurationMinutes, _banReason);
            }

            if (GUILayout.Button("Initialize Ban List"))
                BanListManager.Initialize();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Active bans list
            EditorGUILayout.LabelField($"Active Bans ({BanListManager.ActiveBanCount})", EditorStyles.boldLabel);

            List<BanEntry> bans = BanListManager.GetActiveBans();

            if (bans.Count == 0)
            {
                EditorGUILayout.HelpBox("No active bans.", MessageType.Info);
                return;
            }

            // Header
            EditorGUILayout.BeginHorizontal("box");
            GUILayout.Label("Network ID", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label("Type", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label("Reason", EditorStyles.boldLabel, GUILayout.Width(150));
            GUILayout.Label("Score", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            int toRemove = -1;
            foreach (var ban in bans)
            {
                EditorGUILayout.BeginHorizontal("box");

                GUILayout.Label(ban.NetworkId.ToString(), GUILayout.Width(100));
                GUILayout.Label(ban.BanType == 1 ? "PERM" : "Temp", GUILayout.Width(60));
                GUILayout.Label(ban.Reason ?? "", GUILayout.Width(150));
                GUILayout.Label(ban.ViolationScore.ToString("F1"), GUILayout.Width(50));

                if (GUILayout.Button("Unban", GUILayout.Width(60)))
                    toRemove = ban.NetworkId;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (toRemove >= 0)
                BanListManager.RemoveBan(toRemove);
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
