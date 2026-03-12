using UnityEngine;
using UnityEditor;
using DIG.Weapons.Authoring;

namespace DIG.Editor.EquipmentWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 EW-02: Ranged Weapon Setup module.
    /// Configure hitscan vs projectile, spread patterns, penetration, and range falloff.
    /// </summary>
    public class RangedSetupModule : IEquipmentModule
    {
        private GameObject _selectedPrefab;
        private Vector2 _scrollPosition;

        // Firing mode
        private enum FiringType { Hitscan, Projectile }
        private FiringType _firingType = FiringType.Hitscan;
        
        // Fire rate
        private int _fireRateRPM = 600;
        private bool _isAutomatic = true;
        private int _burstCount = 3;
        private float _burstDelay = 0.1f;
        
        // Damage
        private float _baseDamage = 25f;
        private float _headshotMultiplier = 2.0f;
        private float _limbMultiplier = 0.75f;
        
        // Range
        private float _maxRange = 100f;
        private bool _useFalloff = true;
        private float _falloffStartRange = 30f;
        private AnimationCurve _falloffCurve = AnimationCurve.Linear(0, 1, 1, 0.3f);
        
        // Spread
        private float _baseSpread = 1.5f;
        private float _maxSpread = 8f;
        private float _spreadIncreasePerShot = 0.5f;
        private float _spreadRecoveryRate = 5f;
        private bool _firstShotAccurate = true;
        
        // Penetration
        private bool _canPenetrate = false;
        private int _maxPenetrations = 2;
        private float _penetrationDamageReduction = 0.3f;
        
        // Projectile-specific
        private float _projectileSpeed = 100f;
        private float _projectileGravity = 0f;
        private GameObject _projectilePrefab;
        
        // Ammo
        private int _clipSize = 30;
        private int _maxReserve = 120;
        private float _reloadTime = 2.5f;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Ranged Weapon Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure ranged weapon behavior: hitscan vs projectile, spread patterns, " +
                "penetration, and damage falloff.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Prefab selection
            DrawPrefabSelection();
            EditorGUILayout.Space(10);

            // Firing type
            DrawFiringType();
            EditorGUILayout.Space(10);

            // Fire rate
            DrawFireRate();
            EditorGUILayout.Space(10);

            // Damage
            DrawDamageSection();
            EditorGUILayout.Space(10);

            // Range & Falloff
            DrawRangeSection();
            EditorGUILayout.Space(10);

            // Spread
            DrawSpreadSection();
            EditorGUILayout.Space(10);

            // Penetration
            DrawPenetrationSection();
            EditorGUILayout.Space(10);

            // Projectile (if applicable)
            if (_firingType == FiringType.Projectile)
            {
                DrawProjectileSection();
                EditorGUILayout.Space(10);
            }

            // Ammo
            DrawAmmoSection();
            EditorGUILayout.Space(10);

            // Actions
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPrefabSelection()
        {
            EditorGUILayout.LabelField("Target Prefab", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _selectedPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Ranged Weapon", _selectedPrefab, typeof(GameObject), true);

            if (Selection.activeGameObject != null && _selectedPrefab != Selection.activeGameObject)
            {
                if (GUILayout.Button($"Use Selected: {Selection.activeGameObject.name}", EditorStyles.miniButton))
                {
                    _selectedPrefab = Selection.activeGameObject;
                    LoadFromPrefab();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFiringType()
        {
            EditorGUILayout.LabelField("Firing Type", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = _firingType == FiringType.Hitscan ? Color.cyan : Color.white;
            if (GUILayout.Button("Hitscan", GUILayout.Height(30)))
            {
                _firingType = FiringType.Hitscan;
            }
            
            GUI.backgroundColor = _firingType == FiringType.Projectile ? Color.cyan : Color.white;
            if (GUILayout.Button("Projectile", GUILayout.Height(30)))
            {
                _firingType = FiringType.Projectile;
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            string description = _firingType == FiringType.Hitscan
                ? "Instant raycast - hit detection is immediate. Best for bullets, lasers."
                : "Physical projectile - travels through world with speed/gravity. Best for rockets, arrows.";
            EditorGUILayout.HelpBox(description, MessageType.None);

            EditorGUILayout.EndVertical();
        }

        private void DrawFireRate()
        {
            EditorGUILayout.LabelField("Fire Rate", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _fireRateRPM = EditorGUILayout.IntSlider("RPM", _fireRateRPM, 60, 1200);
            
            float fireInterval = 60f / _fireRateRPM;
            EditorGUILayout.LabelField($"Fire Interval: {fireInterval:F3}s", EditorStyles.miniLabel);

            _isAutomatic = EditorGUILayout.Toggle("Automatic", _isAutomatic);

            if (!_isAutomatic)
            {
                EditorGUI.indentLevel++;
                _burstCount = EditorGUILayout.IntSlider("Burst Count", _burstCount, 1, 5);
                if (_burstCount > 1)
                {
                    _burstDelay = EditorGUILayout.Slider("Burst Delay", _burstDelay, 0.05f, 0.3f);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDamageSection()
        {
            EditorGUILayout.LabelField("Damage", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _baseDamage = EditorGUILayout.FloatField("Base Damage", _baseDamage);
            _headshotMultiplier = EditorGUILayout.Slider("Headshot Multiplier", _headshotMultiplier, 1f, 4f);
            _limbMultiplier = EditorGUILayout.Slider("Limb Multiplier", _limbMultiplier, 0.25f, 1f);

            // DPS preview
            float dps = _baseDamage * (_fireRateRPM / 60f);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Theoretical DPS: {dps:F1}", EditorStyles.boldLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawRangeSection()
        {
            EditorGUILayout.LabelField("Range & Falloff", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _maxRange = EditorGUILayout.FloatField("Max Range (m)", _maxRange);
            
            _useFalloff = EditorGUILayout.Toggle("Use Damage Falloff", _useFalloff);
            if (_useFalloff)
            {
                EditorGUI.indentLevel++;
                _falloffStartRange = EditorGUILayout.Slider("Falloff Start", _falloffStartRange, 5f, _maxRange);
                EditorGUILayout.LabelField($"Full damage until {_falloffStartRange}m, then falls off", EditorStyles.miniLabel);
                _falloffCurve = EditorGUILayout.CurveField("Falloff Curve", _falloffCurve, Color.yellow, new Rect(0, 0, 1, 1));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSpreadSection()
        {
            EditorGUILayout.LabelField("Spread & Accuracy", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _firstShotAccurate = EditorGUILayout.Toggle("First Shot Accurate", _firstShotAccurate);
            _baseSpread = EditorGUILayout.Slider("Base Spread (°)", _baseSpread, 0f, 5f);
            _maxSpread = EditorGUILayout.Slider("Max Spread (°)", _maxSpread, _baseSpread, 15f);
            _spreadIncreasePerShot = EditorGUILayout.Slider("Spread Per Shot", _spreadIncreasePerShot, 0f, 2f);
            _spreadRecoveryRate = EditorGUILayout.Slider("Recovery Rate (°/s)", _spreadRecoveryRate, 1f, 20f);

            // Preview
            float timeToMaxSpread = (_maxSpread - _baseSpread) / (_spreadIncreasePerShot * (_fireRateRPM / 60f));
            float timeToRecover = (_maxSpread - _baseSpread) / _spreadRecoveryRate;
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Time to max spread: {timeToMaxSpread:F2}s | Recovery: {timeToRecover:F2}s", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawPenetrationSection()
        {
            EditorGUILayout.LabelField("Penetration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _canPenetrate = EditorGUILayout.Toggle("Can Penetrate", _canPenetrate);
            if (_canPenetrate)
            {
                EditorGUI.indentLevel++;
                _maxPenetrations = EditorGUILayout.IntSlider("Max Penetrations", _maxPenetrations, 1, 5);
                _penetrationDamageReduction = EditorGUILayout.Slider("Damage Reduction Per", _penetrationDamageReduction, 0.1f, 0.5f);
                
                // Show damage chain
                EditorGUILayout.Space(5);
                string damageChain = $"Damage chain: {_baseDamage:F0}";
                float currentDamage = _baseDamage;
                for (int i = 0; i < _maxPenetrations; i++)
                {
                    currentDamage *= (1f - _penetrationDamageReduction);
                    damageChain += $" → {currentDamage:F0}";
                }
                EditorGUILayout.LabelField(damageChain, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawProjectileSection()
        {
            EditorGUILayout.LabelField("Projectile Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _projectilePrefab = (GameObject)EditorGUILayout.ObjectField(
                "Projectile Prefab", _projectilePrefab, typeof(GameObject), false);
            _projectileSpeed = EditorGUILayout.FloatField("Speed (m/s)", _projectileSpeed);
            _projectileGravity = EditorGUILayout.Slider("Gravity Multiplier", _projectileGravity, 0f, 2f);

            // Time to target preview
            float timeToMax = _maxRange / _projectileSpeed;
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Time to max range: {timeToMax:F2}s", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawAmmoSection()
        {
            EditorGUILayout.LabelField("Ammunition", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _clipSize = EditorGUILayout.IntField("Clip Size", _clipSize);
            _maxReserve = EditorGUILayout.IntField("Max Reserve", _maxReserve);
            _reloadTime = EditorGUILayout.FloatField("Reload Time (s)", _reloadTime);

            // Sustain preview
            float clipDuration = _clipSize / (_fireRateRPM / 60f);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Clip duration: {clipDuration:F2}s | Total mags: {(_maxReserve / _clipSize) + 1}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginDisabledGroup(_selectedPrefab == null);

            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Apply to Prefab", GUILayout.Height(30)))
            {
                ApplyToPrefab();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Load from Prefab", GUILayout.Height(30)))
            {
                LoadFromPrefab();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(5);

            // Quick presets
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Pistol")) ApplyQuickPreset("Pistol");
            if (GUILayout.Button("Rifle")) ApplyQuickPreset("Rifle");
            if (GUILayout.Button("SMG")) ApplyQuickPreset("SMG");
            if (GUILayout.Button("Sniper")) ApplyQuickPreset("Sniper");
            if (GUILayout.Button("Shotgun")) ApplyQuickPreset("Shotgun");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ApplyQuickPreset(string preset)
        {
            switch (preset)
            {
                case "Pistol":
                    _firingType = FiringType.Hitscan;
                    _fireRateRPM = 400;
                    _isAutomatic = false;
                    _burstCount = 1;
                    _baseDamage = 35f;
                    _maxRange = 50f;
                    _baseSpread = 0.5f;
                    _maxSpread = 4f;
                    _clipSize = 12;
                    _reloadTime = 1.5f;
                    _canPenetrate = false;
                    break;
                    
                case "Rifle":
                    _firingType = FiringType.Hitscan;
                    _fireRateRPM = 600;
                    _isAutomatic = true;
                    _baseDamage = 28f;
                    _maxRange = 100f;
                    _baseSpread = 1f;
                    _maxSpread = 6f;
                    _clipSize = 30;
                    _reloadTime = 2.5f;
                    _canPenetrate = true;
                    _maxPenetrations = 1;
                    break;
                    
                case "SMG":
                    _firingType = FiringType.Hitscan;
                    _fireRateRPM = 900;
                    _isAutomatic = true;
                    _baseDamage = 18f;
                    _maxRange = 40f;
                    _baseSpread = 2f;
                    _maxSpread = 10f;
                    _clipSize = 25;
                    _reloadTime = 2.0f;
                    _canPenetrate = false;
                    break;
                    
                case "Sniper":
                    _firingType = FiringType.Hitscan;
                    _fireRateRPM = 40;
                    _isAutomatic = false;
                    _burstCount = 1;
                    _baseDamage = 120f;
                    _maxRange = 200f;
                    _baseSpread = 0f;
                    _maxSpread = 1f;
                    _firstShotAccurate = true;
                    _clipSize = 5;
                    _reloadTime = 3.5f;
                    _canPenetrate = true;
                    _maxPenetrations = 3;
                    break;
                    
                case "Shotgun":
                    _firingType = FiringType.Hitscan;
                    _fireRateRPM = 80;
                    _isAutomatic = false;
                    _burstCount = 1;
                    _baseDamage = 15f; // Per pellet
                    _maxRange = 25f;
                    _baseSpread = 5f;
                    _maxSpread = 8f;
                    _clipSize = 8;
                    _reloadTime = 0.5f; // Per shell
                    _canPenetrate = false;
                    break;
            }
            
            Debug.Log($"[RangedSetup] Applied preset: {preset}");
        }

        private void ApplyToPrefab()
        {
            if (_selectedPrefab == null) return;

            Undo.RecordObject(_selectedPrefab, "Apply Ranged Setup");

            var authoring = _selectedPrefab.GetComponent<WeaponAuthoring>();
            if (authoring == null)
            {
                Debug.LogWarning("[RangedSetup] No WeaponAuthoring found on prefab.");
                return;
            }

            authoring.FireRate = _fireRateRPM;
            authoring.ClipSize = (int)_clipSize;
            authoring.ReloadTime = _reloadTime;
            authoring.Damage = _baseDamage;
            authoring.Range = _maxRange;
            authoring.SpreadAngle = _baseSpread;
            authoring.UseHitscan = _firingType == FiringType.Hitscan;

            EditorUtility.SetDirty(_selectedPrefab);
            Debug.Log($"[RangedSetup] Applied settings to {_selectedPrefab.name}");
        }

        private void LoadFromPrefab()
        {
            if (_selectedPrefab == null) return;

            var authoring = _selectedPrefab.GetComponent<WeaponAuthoring>();
            if (authoring == null)
            {
                Debug.LogWarning("[RangedSetup] No WeaponAuthoring found on prefab.");
                return;
            }

            _fireRateRPM = (int)authoring.FireRate;
            _clipSize = authoring.ClipSize;
            _reloadTime = authoring.ReloadTime;
            _baseDamage = authoring.Damage;
            _maxRange = authoring.Range;
            _baseSpread = authoring.SpreadAngle;
            _firingType = authoring.UseHitscan ? FiringType.Hitscan : FiringType.Projectile;

            Debug.Log($"[RangedSetup] Loaded settings from {_selectedPrefab.name}");
        }
    }
}
