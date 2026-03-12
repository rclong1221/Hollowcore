#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace DIG.Roguelite.Editor.Modules
{
    /// <summary>
    /// EPIC 23.7: Content Coverage Analyzer module.
    /// Automated QA for data completeness: finds gaps, orphans, and misconfigured assets.
    /// Displays completeness score, filterable issue list, and one-click fix buttons.
    /// </summary>
    public class ContentCoverageModule : IRunWorkstationModule
    {
        public string TabName => "Content Coverage";

        private RogueliteDataContext _context;
        private ContentCoverageReport _report;
        private Vector2 _scrollPos;
        private CoverageSeverity _filterSeverity = (CoverageSeverity)(-1); // -1 = show all
        private string _filterCategory = "All";
        private bool _showErrors = true;
        private bool _showWarnings = true;
        private bool _showInfo = true;

        // Cached category list
        private static readonly string[] Categories = { "All", "Zones", "Encounters", "Rewards", "Meta", "Economy" };

        // Cached GUIStyles (initialized lazily — EditorStyles unavailable before first OnGUI)
        private static GUIStyle _scoreStyle;
        private static GUIStyle _barLabelStyle;
        private static GUIStyle _iconStyleError;
        private static GUIStyle _iconStyleWarning;
        private static GUIStyle _iconStyleInfo;
        private static bool _stylesCached;

        public void OnEnable() { }
        public void OnDisable() { }

        public void SetContext(RogueliteDataContext context)
        {
            _context = context;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Content Coverage Analyzer", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_context == null)
            {
                EditorGUILayout.HelpBox("Data context not available. Re-open Run Workstation.", MessageType.Warning);
                return;
            }

            // Scan button
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan Project", GUILayout.Height(28)))
            {
                _context.EnsureBuilt();
                _report = ContentCoverageAnalyzer.Analyze(_context);
            }
            if (_report != null)
            {
                GUILayout.Label($"Last scan: {_report.GeneratedTimestamp:F0}s ago", EditorStyles.miniLabel, GUILayout.Width(150));
            }
            EditorGUILayout.EndHorizontal();

            if (_report == null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "Click 'Scan Project' to analyze all roguelite ScriptableObjects for issues.\n" +
                    "Checks for orphans, missing references, duplicate IDs, configuration errors, and more.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Completeness score
            DrawCompletenessScore();
            EditorGUILayout.Space(8);

            // Summary
            DrawSummary();
            EditorGUILayout.Space(4);

            // Filters
            DrawFilters();
            EditorGUILayout.Space(4);

            // Issue list
            DrawIssueList();

            EditorGUILayout.EndScrollView();
        }

        private static void EnsureStyles()
        {
            if (_stylesCached) return;
            _scoreStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            _barLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            _iconStyleError = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.9f, 0.3f, 0.3f) } };
            _iconStyleWarning = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.9f, 0.7f, 0.2f) } };
            _iconStyleInfo = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.4f, 0.7f, 0.9f) } };
            _stylesCached = true;
        }

        private void DrawCompletenessScore()
        {
            EnsureStyles();
            float score = _report.CompletenessScore;
            Color scoreColor = score >= 80f ? new Color(0.3f, 0.8f, 0.3f)
                             : score >= 50f ? new Color(0.8f, 0.8f, 0.3f)
                             : new Color(0.8f, 0.3f, 0.3f);

            var rect = EditorGUILayout.GetControlRect(false, 50);
            var scoreRect = new Rect(rect.x, rect.y, 100, 50);
            var barBgRect = new Rect(rect.x + 110, rect.y + 10, rect.width - 120, 30);
            var barRect = new Rect(barBgRect.x, barBgRect.y, barBgRect.width * (score / 100f), barBgRect.height);

            // Score number
            _scoreStyle.normal.textColor = scoreColor;
            EditorGUI.LabelField(scoreRect, $"{score:F0}%", _scoreStyle);

            // Score bar
            EditorGUI.DrawRect(barBgRect, new Color(0.2f, 0.2f, 0.2f));
            EditorGUI.DrawRect(barRect, scoreColor * 0.8f);
            EditorGUI.LabelField(barBgRect, "Completeness", _barLabelStyle);
        }

        private void DrawSummary()
        {
            EditorGUILayout.BeginHorizontal();
            DrawCountBadge("Errors", _report.ErrorCount, new Color(0.9f, 0.3f, 0.3f));
            DrawCountBadge("Warnings", _report.WarningCount, new Color(0.9f, 0.7f, 0.2f));
            DrawCountBadge("Info", _report.InfoCount, new Color(0.4f, 0.7f, 0.9f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Total issues: {_report.Issues.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static readonly GUIStyle _badgeStyle = new GUIStyle();
        private static bool _badgeStyleInit;

        private static void DrawCountBadge(string label, int count, Color color)
        {
            if (!_badgeStyleInit)
            {
                _badgeStyleInit = true;
                var src = EditorStyles.miniButton;
                _badgeStyle.normal = new GUIStyleState { background = src.normal.background };
                _badgeStyle.padding = src.padding;
                _badgeStyle.margin = src.margin;
                _badgeStyle.alignment = src.alignment;
                _badgeStyle.fontSize = src.fontSize;
            }
            _badgeStyle.normal.textColor = count > 0 ? color : Color.gray;
            _badgeStyle.fontStyle = count > 0 ? FontStyle.Bold : FontStyle.Normal;
            GUILayout.Label($"{count} {label}", _badgeStyle, GUILayout.Width(90));
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(40));

            _showErrors = GUILayout.Toggle(_showErrors, "Errors", EditorStyles.miniButton, GUILayout.Width(60));
            _showWarnings = GUILayout.Toggle(_showWarnings, "Warnings", EditorStyles.miniButton, GUILayout.Width(70));
            _showInfo = GUILayout.Toggle(_showInfo, "Info", EditorStyles.miniButton, GUILayout.Width(50));

            GUILayout.Space(10);
            int catIndex = System.Array.IndexOf(Categories, _filterCategory);
            if (catIndex < 0) catIndex = 0;
            int newCat = EditorGUILayout.Popup(catIndex, Categories, GUILayout.Width(120));
            _filterCategory = Categories[newCat];

            EditorGUILayout.EndHorizontal();
        }

        private void DrawIssueList()
        {
            for (int i = 0; i < _report.Issues.Count; i++)
            {
                var issue = _report.Issues[i];

                // Filter
                if (issue.Severity == CoverageSeverity.Error && !_showErrors) continue;
                if (issue.Severity == CoverageSeverity.Warning && !_showWarnings) continue;
                if (issue.Severity == CoverageSeverity.Info && !_showInfo) continue;
                if (_filterCategory != "All" && issue.Category != _filterCategory) continue;

                EditorGUILayout.BeginHorizontal("box");

                // Severity icon (cached styles)
                EnsureStyles();
                string icon;
                GUIStyle iconStyle;
                switch (issue.Severity)
                {
                    case CoverageSeverity.Error: icon = "E"; iconStyle = _iconStyleError; break;
                    case CoverageSeverity.Warning: icon = "W"; iconStyle = _iconStyleWarning; break;
                    default: icon = "i"; iconStyle = _iconStyleInfo; break;
                }
                EditorGUILayout.LabelField(icon, iconStyle, GUILayout.Width(16));

                // Category
                EditorGUILayout.LabelField(issue.Category, EditorStyles.miniLabel, GUILayout.Width(75));

                // Message
                EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);

                // Ping button
                if (issue.Asset != null)
                {
                    if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40)))
                    {
                        EditorGUIUtility.PingObject(issue.Asset);
                        Selection.activeObject = issue.Asset;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            if (_report.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No issues found. All content is properly configured.", MessageType.None);
            }
        }
    }
}
#endif
