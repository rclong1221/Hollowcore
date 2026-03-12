using UnityEngine;
using UnityEditor;
using Unity.Entities;
using DIG.Items;
using System.Collections.Generic;

namespace DIG.Items.Editor
{
    /// <summary>
    /// Editor window for debugging the Equipment System at runtime.
    /// Shows provider state, ECS comparison, and allows force-equip testing.
    /// </summary>
    public class EquipmentSystemDebuggerWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private DIGEquipmentProvider _selectedProvider;
        private List<string> _eventLog = new List<string>();
        private const int MAX_LOG_ENTRIES = 50;
        private bool _autoRefresh = true;
        private float _lastRefreshTime;
        private const float REFRESH_INTERVAL = 0.1f;

        [MenuItem("DIG/Equipment/System Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<EquipmentSystemDebuggerWindow>("Equipment Debugger");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnsubscribeFromProvider();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _selectedProvider = null;
                _eventLog.Clear();
            }
        }

        private void OnInspectorUpdate()
        {
            if (_autoRefresh && Application.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Equipment System Debugger", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to debug equipment state.", MessageType.Info);
                return;
            }

            DrawProviderSelection();
            
            if (_selectedProvider == null)
            {
                EditorGUILayout.HelpBox("Select a DIGEquipmentProvider to inspect.", MessageType.Info);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            DrawSlotStates();
            EditorGUILayout.Space();
            DrawECSComparison();
            EditorGUILayout.Space();
            DrawForceEquipSection();
            EditorGUILayout.Space();
            DrawEventLog();

            EditorGUILayout.EndScrollView();
        }

        private void DrawProviderSelection()
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField("Provider:", GUILayout.Width(60));
            
            var oldProvider = _selectedProvider;
            _selectedProvider = (DIGEquipmentProvider)EditorGUILayout.ObjectField(
                _selectedProvider, 
                typeof(DIGEquipmentProvider), 
                true);
            
            if (_selectedProvider != oldProvider)
            {
                UnsubscribeFromProvider();
                SubscribeToProvider();
            }
            
            if (GUILayout.Button("Find", GUILayout.Width(50)))
            {
                FindProvider();
            }
            
            EditorGUILayout.EndHorizontal();
            
            _autoRefresh = EditorGUILayout.Toggle("Auto Refresh", _autoRefresh);
        }

        private void FindProvider()
        {
            _selectedProvider = Object.FindFirstObjectByType<DIGEquipmentProvider>();
            if (_selectedProvider != null)
            {
                SubscribeToProvider();
            }
        }

        private void SubscribeToProvider()
        {
            if (_selectedProvider != null)
            {
                _selectedProvider.OnEquipmentChanged += OnEquipmentChanged;
            }
        }

        private void UnsubscribeFromProvider()
        {
            if (_selectedProvider != null)
            {
                _selectedProvider.OnEquipmentChanged -= OnEquipmentChanged;
            }
        }

        private void OnEquipmentChanged(object sender, EquipmentChangedEventArgs args)
        {
            string log = $"[{Time.time:F2}] Slot {args.SlotIndex}: " +
                         $"{args.OldItem.AnimatorItemID} → {args.NewItem.AnimatorItemID}";
            _eventLog.Insert(0, log);
            
            while (_eventLog.Count > MAX_LOG_ENTRIES)
            {
                _eventLog.RemoveAt(_eventLog.Count - 1);
            }
            
            Repaint();
        }

        private void DrawSlotStates()
        {
            EditorGUILayout.LabelField("Slot States", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            for (int i = 0; i < _selectedProvider.SlotCount; i++)
            {
                var item = _selectedProvider.GetEquippedItem(i);
                string slotName = i == 0 ? "Main Hand" : i == 1 ? "Off Hand" : $"Slot {i}";
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(slotName, GUILayout.Width(80));
                
                if (item.IsEmpty)
                {
                    EditorGUILayout.LabelField("(Empty)", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField($"ID: {item.AnimatorItemID}", GUILayout.Width(60));
                    EditorGUILayout.LabelField($"Cat: {item.CategoryID}", GUILayout.Width(100));
                    EditorGUILayout.LabelField($"MovSet: {item.MovementSetID}", GUILayout.Width(70));
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawECSComparison()
        {
            EditorGUILayout.LabelField("ECS State Comparison", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var world = _selectedProvider.EntityWorld;
            var entity = _selectedProvider.PlayerEntity;

            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.LabelField("World: Not available");
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField($"World: {world.Name}");
            EditorGUILayout.LabelField($"Entity: {entity.Index}:{entity.Version}");

            var em = world.EntityManager;
            
            if (em.Exists(entity))
            {
                if (em.HasComponent<ActiveSlotIndex>(entity))
                {
                    EditorGUILayout.LabelField($"Active Slot Index: {em.GetComponentData<ActiveSlotIndex>(entity).Value}");
                }

                if (em.HasBuffer<EquippedItemElement>(entity))
                {
                    var buffer = em.GetBuffer<EquippedItemElement>(entity);
                    for (int i = 0; i < buffer.Length; i++)
                    {
                         EditorGUILayout.LabelField($"Buffer[{i}]: Item={buffer[i].ItemEntity.Index} QSlot={buffer[i].QuickSlot}");
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No EquippedItemElement buffer");
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawForceEquipSection()
        {
            EditorGUILayout.LabelField("Force Equip (Testing)", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Get inventory items from ECS
            var inventoryItems = GetInventoryQuickSlots();
            
            if (inventoryItems.Count == 0)
            {
                EditorGUILayout.HelpBox("No items in inventory buffer.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"Inventory: {inventoryItems.Count} items");
                
                // Iterate dynamic slots
                for (int i = 0; i < _selectedProvider.SlotCount; i++)
                {
                    string slotName = i == 0 ? "Main Hand" : i == 1 ? "Off Hand" : $"Slot {i}";
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{slotName}:", GUILayout.Width(80));

                    // Show IsSuppressed status
                    bool suppressed = _selectedProvider.IsSlotSuppressed(i);
                    if (suppressed)
                    {
                         var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.red } };
                         EditorGUILayout.LabelField("(Suppressed)", style, GUILayout.Width(80));
                    }

                    foreach (var item in inventoryItems)
                    {
                        if (GUILayout.Button($"{item.QuickSlot}", GUILayout.Width(30)))
                        {
                            ForceEquip(i, item.QuickSlot);
                        }
                    }
                    if (GUILayout.Button("Clear", GUILayout.Width(50)))
                    {
                        ForceEquip(i, 0);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        private struct InventoryItemInfo
        {
            public int QuickSlot;
            public Entity ItemEntity;
        }

        private List<InventoryItemInfo> GetInventoryQuickSlots()
        {
            var result = new List<InventoryItemInfo>();
            
            if (_selectedProvider == null) return result;
            
            var world = _selectedProvider.EntityWorld;
            var entity = _selectedProvider.PlayerEntity;
            
            if (world == null || !world.IsCreated || !world.EntityManager.Exists(entity))
                return result;
            
            var em = world.EntityManager;
            
            if (!em.HasBuffer<ItemSetEntry>(entity))
                return result;
            
            var buffer = em.GetBuffer<ItemSetEntry>(entity);
            for (int i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i];
                if (entry.QuickSlot > 0)
                {
                    result.Add(new InventoryItemInfo
                    {
                        QuickSlot = entry.QuickSlot,
                        ItemEntity = entry.ItemEntity
                    });
                }
            }
            
            // Sort by QuickSlot
            result.Sort((a, b) => a.QuickSlot.CompareTo(b.QuickSlot));
            
            return result;
        }

        private void ForceEquip(int slotIndex, int quickSlot)
        {
            Debug.Log($"[EquipmentDebugger] ForceEquip called: slotIndex={slotIndex}, quickSlot={quickSlot}");

            var world = _selectedProvider.EntityWorld;
            var entity = _selectedProvider.PlayerEntity;

            if (world == null || !world.IsCreated || !world.EntityManager.Exists(entity))
            {
                Debug.LogWarning("[EquipmentDebugger] No valid world or entity");
                return;
            }

            var em = world.EntityManager;

            // Use EquipRequest component to trigger the normal equip flow
            // This ensures all systems (visual, animation, etc.) are properly notified
            if (em.HasComponent<EquipRequest>(entity))
            {
                Entity itemEntity = Entity.Null;

                // Find item entity for logging
                if (quickSlot > 0 && em.HasBuffer<ItemSetEntry>(entity))
                {
                    var itemBuffer = em.GetBuffer<ItemSetEntry>(entity);
                    for (int k = 0; k < itemBuffer.Length; k++)
                    {
                        if (itemBuffer[k].QuickSlot == quickSlot)
                        {
                            itemEntity = itemBuffer[k].ItemEntity;
                            break;
                        }
                    }
                }

                em.SetComponentData(entity, new EquipRequest
                {
                    QuickSlot = quickSlot,
                    SlotId = slotIndex,
                    Pending = true,
                    ItemEntity = itemEntity
                });

                Debug.Log($"[EquipmentDebugger] EquipRequest set: Slot {slotIndex}, QuickSlot {quickSlot}, Item {itemEntity.Index}");
            }
            else
            {
                Debug.LogWarning("[EquipmentDebugger] Entity missing EquipRequest component - falling back to direct buffer manipulation");

                // Fallback: Direct buffer manipulation (less reliable but works for testing)
                if (em.HasBuffer<EquippedItemElement>(entity))
                {
                     var buffer = em.GetBuffer<EquippedItemElement>(entity);
                     if (slotIndex < buffer.Length)
                     {
                         Entity itemEntity = Entity.Null;

                         // Find item entity
                         if (quickSlot > 0 && em.HasBuffer<ItemSetEntry>(entity))
                         {
                             var itemBuffer = em.GetBuffer<ItemSetEntry>(entity);
                             for (int k = 0; k < itemBuffer.Length; k++)
                             {
                                 if (itemBuffer[k].QuickSlot == quickSlot)
                                 {
                                     itemEntity = itemBuffer[k].ItemEntity;
                                     break;
                                 }
                             }
                         }

                         var elem = buffer[slotIndex];
                         elem.ItemEntity = itemEntity;
                         elem.QuickSlot = quickSlot;
                         buffer[slotIndex] = elem;

                         // Also update ActiveSlotIndex component if exists
                         if (em.HasComponent<ActiveSlotIndex>(entity) && itemEntity != Entity.Null)
                         {
                             em.SetComponentData(entity, new ActiveSlotIndex { Value = slotIndex });
                         }

                         Debug.Log($"[EquipmentDebugger] Direct buffer update: Slot {slotIndex} -> Item {itemEntity.Index}");
                     }
                     else
                     {
                         Debug.LogWarning($"[EquipmentDebugger] Buffer too small ({buffer.Length}) for Slot {slotIndex}");
                     }
                }
            }
        }

        private void DrawEventLog()
        {
            EditorGUILayout.LabelField($"Event Log ({_eventLog.Count})", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(100));
            
            if (_eventLog.Count == 0)
            {
                EditorGUILayout.LabelField("No events yet", EditorStyles.miniLabel);
            }
            else
            {
                foreach (var entry in _eventLog)
                {
                    EditorGUILayout.LabelField(entry, EditorStyles.miniLabel);
                }
            }
            
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Clear Log"))
            {
                _eventLog.Clear();
            }
        }
    }
}
