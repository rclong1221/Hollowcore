using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Collections;
using DIG.Items;
using System.Collections.Generic;

namespace DIG.Items.Editor.Wizards
{
    /// <summary>
    /// EPIC 14.6 Phase 6 - Runtime Equipment Debugger
    /// Provides live visualization of equipment system ECS data during Play Mode.
    /// </summary>
    public class RuntimeEquipmentDebugger : EditorWindow
    {
        private Entity _selectedPlayer = Entity.Null;
        private Vector2 _slotScrollPos;
        private Vector2 _logScrollPos;
        private List<string> _eventLog = new List<string>();

        [MenuItem("DIG/Wizards/6. Debug: Runtime Monitor")]
        public static void ShowWindow()
        {
            var window = GetWindow<RuntimeEquipmentDebugger>("Runtime Monitor");
            window.minSize = new Vector2(500, 400);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChange;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChange;
        }

        private void OnPlayModeChange(PlayModeStateChange state)
        {
            _selectedPlayer = Entity.Null;
            _eventLog.Clear();
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            // Repaint in Play Mode to show live data
            if (Application.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use the Runtime Monitor.", MessageType.Warning);
                return;
            }

            DrawHeader();
            DrawSlotView();
            DrawEventLog();
            DrawOverrideControls();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Runtime Equipment Monitor", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Find Player", EditorStyles.toolbarButton))
            {
                FindPlayerEntity();
            }
            EditorGUILayout.EndHorizontal();

            if (_selectedPlayer == Entity.Null)
            {
                EditorGUILayout.HelpBox("No player entity selected. Click 'Find Player' or spawn a character.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"Monitoring Entity: {_selectedPlayer.Index}:{_selectedPlayer.Version}");
            }
        }

        private void DrawSlotView()
        {
            if (_selectedPlayer == Entity.Null) return;

            EditorGUILayout.LabelField("Equipped Items (ECS Buffer)", EditorStyles.boldLabel);
            _slotScrollPos = EditorGUILayout.BeginScrollView(_slotScrollPos, GUILayout.Height(150));

            var em = World.DefaultGameObjectInjectionWorld?.EntityManager;
            if (em == null || !em.Value.Exists(_selectedPlayer))
            {
                EditorGUILayout.HelpBox("Entity no longer exists.", MessageType.Error);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (em.Value.HasBuffer<EquippedItemElement>(_selectedPlayer))
            {
                var buffer = em.Value.GetBuffer<EquippedItemElement>(_selectedPlayer);
                for (int i = 0; i < buffer.Length; i++)
                {
                    var element = buffer[i];
                    DrawSlotRow(i, element, em.Value);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Player entity does not have EquippedItemElement buffer.", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSlotRow(int index, EquippedItemElement element, EntityManager em)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            string slotName = index switch
            {
                0 => "Main Hand",
                1 => "Off Hand",
                _ => $"Slot {index}"
            };
            EditorGUILayout.LabelField(slotName, EditorStyles.boldLabel, GUILayout.Width(100));

            if (element.ItemEntity == Entity.Null)
            {
                EditorGUILayout.LabelField("[Empty]", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                string itemName = "Item";
                if (em.HasComponent<ItemDefinition>(element.ItemEntity))
                {
                    var def = em.GetComponentData<ItemDefinition>(element.ItemEntity);
                    itemName = def.DisplayName.ToString();
                }
                EditorGUILayout.LabelField($"{itemName} (QuickSlot: {element.QuickSlot})", EditorStyles.label);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEventLog()
        {
            EditorGUILayout.LabelField("Event Log", EditorStyles.boldLabel);
            _logScrollPos = EditorGUILayout.BeginScrollView(_logScrollPos, GUILayout.Height(100));
            
            for (int i = _eventLog.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.LabelField(_eventLog[i], EditorStyles.miniLabel);
            }

            if (_eventLog.Count == 0)
            {
                EditorGUILayout.LabelField("(No events recorded yet)", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawOverrideControls()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Override Controls", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Force Unequip All"))
            {
                ForceUnequipAll();
            }

            if (GUILayout.Button("Log Refresh"))
            {
                AddLogEntry("Manual refresh triggered.");
            }

            EditorGUILayout.EndHorizontal();
        }

        private void FindPlayerEntity()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            // Simple query: Find first entity with EquippedItemElement buffer
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(typeof(EquippedItemElement));
            
            if (query.CalculateEntityCount() > 0)
            {
                var entities = query.ToEntityArray(Allocator.Temp);
                _selectedPlayer = entities[0]; // Pick first
                entities.Dispose();
                AddLogEntry($"Found Player Entity: {_selectedPlayer.Index}:{_selectedPlayer.Version}");
            }
            else
            {
                EditorUtility.DisplayDialog("Not Found", "No entity with EquippedItemElement buffer found.", "OK");
            }
        }

        private void ForceUnequipAll()
        {
            if (_selectedPlayer == Entity.Null) return;

            var em = World.DefaultGameObjectInjectionWorld?.EntityManager;
            if (em == null || !em.Value.HasBuffer<EquippedItemElement>(_selectedPlayer)) return;

            var buffer = em.Value.GetBuffer<EquippedItemElement>(_selectedPlayer);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = new EquippedItemElement { ItemEntity = Entity.Null, QuickSlot = 0 };
            }
            AddLogEntry("Forced unequip of all items.");
        }

        private void AddLogEntry(string msg)
        {
            _eventLog.Add($"[{System.DateTime.Now:HH:mm:ss}] {msg}");
            if (_eventLog.Count > 50) // Cap log size
                _eventLog.RemoveAt(0);
        }
    }
}
