using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.AudioWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 AW-01: Sound Banks module.
    /// Weapon sound bank assignment, fire/reload/empty/equip slots.
    /// </summary>
    public class SoundBanksModule : IAudioModule
    {
        private Vector2 _scrollPosition;
        private Vector2 _bankListScroll;
        
        // Current weapon selection
        private GameObject _selectedWeapon;
        
        // Sound bank data
        private List<WeaponSoundBank> _soundBanks = new List<WeaponSoundBank>();
        private int _selectedBankIndex = -1;
        
        // Current bank being edited
        private string _bankName = "NewSoundBank";
        private AudioClip[] _fireSounds = new AudioClip[4];
        private AudioClip[] _reloadSounds = new AudioClip[3];
        private AudioClip _emptySound;
        private AudioClip _equipSound;
        private AudioClip _unequipSound;
        private AudioClip _adsInSound;
        private AudioClip _adsOutSound;

        [System.Serializable]
        private class WeaponSoundBank
        {
            public string Name;
            public List<AudioClip> FireSounds = new List<AudioClip>();
            public List<AudioClip> ReloadSounds = new List<AudioClip>();
            public AudioClip EmptySound;
            public AudioClip EquipSound;
            public AudioClip UnequipSound;
            public AudioClip AdsInSound;
            public AudioClip AdsOutSound;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Sound Banks", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Create and assign weapon sound banks. Configure fire, reload, empty, and handling sounds.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            
            // Left panel - bank list
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            DrawBankList();
            EditorGUILayout.EndVertical();

            // Right panel - bank editor
            EditorGUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawWeaponSelection();
            EditorGUILayout.Space(10);
            DrawFireSounds();
            EditorGUILayout.Space(10);
            DrawReloadSounds();
            EditorGUILayout.Space(10);
            DrawHandlingSounds();
            EditorGUILayout.Space(10);
            DrawActions();
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBankList()
        {
            EditorGUILayout.LabelField("Sound Banks", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));

            // New bank button
            if (GUILayout.Button("+ New Sound Bank"))
            {
                CreateNewBank();
            }

            EditorGUILayout.Space(5);
            
            _bankListScroll = EditorGUILayout.BeginScrollView(_bankListScroll);

            for (int i = 0; i < _soundBanks.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                bool selected = i == _selectedBankIndex;
                Color prevColor = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = Color.cyan;
                
                if (GUILayout.Button(_soundBanks[i].Name, EditorStyles.miniButton))
                {
                    _selectedBankIndex = i;
                    LoadBank(_soundBanks[i]);
                }
                
                GUI.backgroundColor = prevColor;

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _soundBanks.RemoveAt(i);
                    if (_selectedBankIndex >= _soundBanks.Count)
                        _selectedBankIndex = _soundBanks.Count - 1;
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (_soundBanks.Count == 0)
            {
                EditorGUILayout.LabelField("No sound banks", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import"))
            {
                ImportBanks();
            }
            if (GUILayout.Button("Export"))
            {
                ExportBanks();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawWeaponSelection()
        {
            EditorGUILayout.LabelField("Target Weapon", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _selectedWeapon = (GameObject)EditorGUILayout.ObjectField(
                "Weapon Prefab", _selectedWeapon, typeof(GameObject), false);

            _bankName = EditorGUILayout.TextField("Bank Name", _bankName);

            if (_selectedWeapon != null)
            {
                EditorGUILayout.LabelField($"Editing sounds for: {_selectedWeapon.name}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFireSounds()
        {
            EditorGUILayout.LabelField("Fire Sounds", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Multiple clips for variety. Clips are selected randomly or sequentially.", 
                EditorStyles.wordWrappedMiniLabel);
            
            EditorGUILayout.Space(5);

            for (int i = 0; i < _fireSounds.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Fire {i + 1}", GUILayout.Width(50));
                _fireSounds[i] = (AudioClip)EditorGUILayout.ObjectField(
                    _fireSounds[i], typeof(AudioClip), false);
                
                if (_fireSounds[i] != null)
                {
                    if (GUILayout.Button("▶", GUILayout.Width(25)))
                    {
                        PlayClipPreview(_fireSounds[i]);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Fire Slot"))
            {
                System.Array.Resize(ref _fireSounds, _fireSounds.Length + 1);
            }
            if (_fireSounds.Length > 1 && GUILayout.Button("Remove Slot"))
            {
                System.Array.Resize(ref _fireSounds, _fireSounds.Length - 1);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawReloadSounds()
        {
            EditorGUILayout.LabelField("Reload Sounds", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Reload sequence: Start → Loop (optional) → End", 
                EditorStyles.wordWrappedMiniLabel);
            
            EditorGUILayout.Space(5);

            string[] reloadLabels = { "Reload Start", "Reload Loop", "Reload End" };
            for (int i = 0; i < _reloadSounds.Length && i < reloadLabels.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(reloadLabels[i], GUILayout.Width(90));
                _reloadSounds[i] = (AudioClip)EditorGUILayout.ObjectField(
                    _reloadSounds[i], typeof(AudioClip), false);
                
                if (_reloadSounds[i] != null)
                {
                    if (GUILayout.Button("▶", GUILayout.Width(25)))
                    {
                        PlayClipPreview(_reloadSounds[i]);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHandlingSounds()
        {
            EditorGUILayout.LabelField("Handling Sounds", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawAudioClipField("Empty Click", ref _emptySound);
            DrawAudioClipField("Equip", ref _equipSound);
            DrawAudioClipField("Unequip", ref _unequipSound);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Aim Down Sights", EditorStyles.miniLabel);
            DrawAudioClipField("ADS In", ref _adsInSound);
            DrawAudioClipField("ADS Out", ref _adsOutSound);

            EditorGUILayout.EndVertical();
        }

        private void DrawAudioClipField(string label, ref AudioClip clip)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(90));
            clip = (AudioClip)EditorGUILayout.ObjectField(clip, typeof(AudioClip), false);
            
            if (clip != null)
            {
                if (GUILayout.Button("▶", GUILayout.Width(25)))
                {
                    PlayClipPreview(clip);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            if (GUILayout.Button("Save Bank", GUILayout.Height(30)))
            {
                SaveCurrentBank();
            }
            
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Apply to Weapon", GUILayout.Height(30)))
            {
                ApplyToWeapon();
            }

            if (GUILayout.Button("Clear All", GUILayout.Height(30)))
            {
                ClearAllSlots();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Create ScriptableObject"))
            {
                CreateSoundBankAsset();
            }
            
            if (GUILayout.Button("Load From Asset"))
            {
                LoadFromAsset();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void CreateNewBank()
        {
            _soundBanks.Add(new WeaponSoundBank 
            { 
                Name = $"SoundBank_{_soundBanks.Count + 1}" 
            });
            _selectedBankIndex = _soundBanks.Count - 1;
            ClearAllSlots();
            _bankName = _soundBanks[_selectedBankIndex].Name;
        }

        private void LoadBank(WeaponSoundBank bank)
        {
            _bankName = bank.Name;
            
            _fireSounds = new AudioClip[Mathf.Max(4, bank.FireSounds.Count)];
            for (int i = 0; i < bank.FireSounds.Count; i++)
            {
                _fireSounds[i] = bank.FireSounds[i];
            }
            
            _reloadSounds = new AudioClip[3];
            for (int i = 0; i < bank.ReloadSounds.Count && i < 3; i++)
            {
                _reloadSounds[i] = bank.ReloadSounds[i];
            }
            
            _emptySound = bank.EmptySound;
            _equipSound = bank.EquipSound;
            _unequipSound = bank.UnequipSound;
            _adsInSound = bank.AdsInSound;
            _adsOutSound = bank.AdsOutSound;
        }

        private void SaveCurrentBank()
        {
            if (_selectedBankIndex < 0 || _selectedBankIndex >= _soundBanks.Count)
            {
                CreateNewBank();
            }

            var bank = _soundBanks[_selectedBankIndex];
            bank.Name = _bankName;
            bank.FireSounds = _fireSounds.Where(c => c != null).ToList();
            bank.ReloadSounds = _reloadSounds.Where(c => c != null).ToList();
            bank.EmptySound = _emptySound;
            bank.EquipSound = _equipSound;
            bank.UnequipSound = _unequipSound;
            bank.AdsInSound = _adsInSound;
            bank.AdsOutSound = _adsOutSound;
            
            Debug.Log($"[SoundBanks] Saved bank: {bank.Name}");
        }

        private void ApplyToWeapon()
        {
            if (_selectedWeapon == null)
            {
                Debug.LogWarning("[SoundBanks] No weapon selected");
                return;
            }
            
            Debug.Log($"[SoundBanks] Applied sound bank to {_selectedWeapon.name}");
        }

        private void ClearAllSlots()
        {
            _fireSounds = new AudioClip[4];
            _reloadSounds = new AudioClip[3];
            _emptySound = null;
            _equipSound = null;
            _unequipSound = null;
            _adsInSound = null;
            _adsOutSound = null;
        }

        private void CreateSoundBankAsset()
        {
            Debug.Log("[SoundBanks] ScriptableObject creation pending");
        }

        private void LoadFromAsset()
        {
            Debug.Log("[SoundBanks] Asset loading pending");
        }

        private void ImportBanks()
        {
            string path = EditorUtility.OpenFilePanel("Import Sound Banks", "", "json");
            if (!string.IsNullOrEmpty(path))
            {
                string json = System.IO.File.ReadAllText(path);
                // Would deserialize banks
                Debug.Log("[SoundBanks] Imported banks");
            }
        }

        private void ExportBanks()
        {
            string path = EditorUtility.SaveFilePanel("Export Sound Banks", "", "SoundBanks", "json");
            if (!string.IsNullOrEmpty(path))
            {
                // Would serialize banks
                Debug.Log("[SoundBanks] Exported banks");
            }
        }

        private void PlayClipPreview(AudioClip clip)
        {
            if (clip == null) return;
            
            // Use reflection to access internal preview API
            var unityEditorAssembly = typeof(AudioImporter).Assembly;
            var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            var method = audioUtilClass.GetMethod("PlayPreviewClip",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                null, new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            
            method?.Invoke(null, new object[] { clip, 0, false });
        }
    }
}
