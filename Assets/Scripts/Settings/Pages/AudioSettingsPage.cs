using System.Collections.Generic;
using DIG.Settings.Core;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;

namespace DIG.Settings.Pages
{
    /// <summary>
    /// EPIC 18.2: Audio settings page.
    /// Bridges to AudioManager.MasterMixer for volume controls.
    /// Volume sliders operate in linear 0–1 space; converted to dB for the mixer.
    /// </summary>
    public class AudioSettingsPage : ISettingsPage
    {
        public string PageId => "Audio";
        public string DisplayName => "Audio";
        public int SortOrder => 1;

        // Exposed AudioMixer parameter names
        private static readonly string[] ParamNames = { "MasterVolume", "MusicVolume", "SFXVolume", "VoiceVolume", "AmbientVolume" };
        private static readonly string[] ParamLabels = { "Master Volume", "Music Volume", "SFX Volume", "Voice Volume", "Ambient Volume" };
        private static readonly string PrefPrefix = "Audio_";

        // Snapshot + current values (linear 0–1)
        private readonly float[] _snapValues = new float[5];
        private readonly float[] _currentValues = new float[5];
        private readonly bool[] _paramAvailable = new bool[5];

        // Cached mixer reference (avoids FindAnyObjectByType per slider drag)
        private AudioMixer _cachedMixer;
        private bool _mixerCached;

        public bool HasUnsavedChanges
        {
            get
            {
                for (int i = 0; i < _currentValues.Length; i++)
                {
                    if (_paramAvailable[i] && !Mathf.Approximately(_currentValues[i], _snapValues[i]))
                        return true;
                }
                return false;
            }
        }

        public void TakeSnapshot()
        {
            var mixer = FindMixer();
            for (int i = 0; i < ParamNames.Length; i++)
            {
                float linear = PlayerPrefs.GetFloat(PrefPrefix + ParamNames[i], 0.8f);

                // Read live value from mixer if available, otherwise use PlayerPrefs
                if (mixer != null && mixer.GetFloat(ParamNames[i], out float dB))
                {
                    linear = DbToLinear(dB);
                }
                _paramAvailable[i] = true;

                _snapValues[i] = linear;
                _currentValues[i] = linear;
            }
        }

        public void BuildUI(VisualElement container)
        {
            container.Add(SettingsScreenController.CreateSectionHeader("Volume"));

            for (int i = 0; i < ParamNames.Length; i++)
            {
                if (!_paramAvailable[i]) continue;

                int idx = i; // Capture for lambda
                container.Add(SettingsScreenController.CreateSliderRow(
                    ParamLabels[i], 0f, 1f, _currentValues[i],
                    val =>
                    {
                        _currentValues[idx] = val;
                        PreviewVolume(idx, val);
                    },
                    "P0")); // Percentage format
            }
        }

        public void OnPageShown() { }

        public void ApplyChanges()
        {
            var mixer = FindMixer();
            for (int i = 0; i < ParamNames.Length; i++)
            {
                if (!_paramAvailable[i]) continue;

                PlayerPrefs.SetFloat(PrefPrefix + ParamNames[i], _currentValues[i]);

                if (mixer != null)
                    mixer.SetFloat(ParamNames[i], LinearToDb(_currentValues[i]));
            }
            PlayerPrefs.Save();
        }

        public void RevertChanges()
        {
            var mixer = FindMixer();
            for (int i = 0; i < ParamNames.Length; i++)
            {
                _currentValues[i] = _snapValues[i];
                if (_paramAvailable[i] && mixer != null)
                    mixer.SetFloat(ParamNames[i], LinearToDb(_snapValues[i]));
            }
        }

        public void ResetToDefaults()
        {
            for (int i = 0; i < _currentValues.Length; i++)
                _currentValues[i] = 0.8f;

            var mixer = FindMixer();
            if (mixer != null)
            {
                for (int i = 0; i < ParamNames.Length; i++)
                {
                    if (_paramAvailable[i])
                        mixer.SetFloat(ParamNames[i], LinearToDb(0.8f));
                }
            }
        }

        // === Helpers ===

        private void PreviewVolume(int index, float linear)
        {
            var mixer = FindMixer();
            if (mixer != null && _paramAvailable[index])
                mixer.SetFloat(ParamNames[index], LinearToDb(linear));
        }

        private AudioMixer FindMixer()
        {
            if (_mixerCached) return _cachedMixer;
            var audioMgr = Object.FindAnyObjectByType<Audio.Systems.AudioManager>();
            _cachedMixer = audioMgr != null ? audioMgr.MasterMixer : null;
            _mixerCached = true;
            return _cachedMixer;
        }

        /// <summary>Convert linear 0–1 to dB (-80 to 0).</summary>
        private static float LinearToDb(float linear)
        {
            return Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f;
        }

        /// <summary>Convert dB (-80 to 0) to linear 0–1.</summary>
        private static float DbToLinear(float dB)
        {
            return Mathf.Pow(10f, dB / 20f);
        }
    }
}
