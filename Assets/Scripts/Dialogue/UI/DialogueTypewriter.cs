using System;
using System.Text;
using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 18.5: Character-by-character text reveal with punctuation pauses
    /// and rich text tag awareness. Driven per-frame by the dialogue UI view.
    /// </summary>
    public class DialogueTypewriter
    {
        private string _fullText;
        private readonly StringBuilder _revealed;
        private int _charIndex;
        private float _timer;
        private float _charsPerSecond;
        private float _pausePeriod;
        private float _pauseComma;
        private float _pauseExclamation;
        private bool _complete;
        private string _revealedTextCache;
        private bool _revealedTextDirty;

        // Optional per-character mumble SFX
        private AudioClip[] _voiceBank;
        private AudioSource _audioSource;

        public bool IsComplete => _complete;
        public string RevealedText
        {
            get
            {
                if (_revealedTextDirty)
                {
                    _revealedTextCache = _revealed.ToString();
                    _revealedTextDirty = false;
                }
                return _revealedTextCache;
            }
        }
        public string FullText => _fullText;

        public event Action OnCharacterRevealed;
        public event Action OnTextComplete;

        public DialogueTypewriter(int capacity = 512)
        {
            _revealed = new StringBuilder(capacity);
        }

        /// <summary>
        /// Begin revealing a new line of text.
        /// </summary>
        /// <param name="text">Full text to reveal.</param>
        /// <param name="charsPerSecond">Speed. 0 or negative = instant.</param>
        /// <param name="pausePeriod">Pause after period in seconds.</param>
        /// <param name="pauseComma">Pause after comma in seconds.</param>
        /// <param name="pauseExclamation">Pause after ! or ? in seconds.</param>
        /// <param name="voiceBank">Optional mumble clips for per-character SFX.</param>
        /// <param name="audioSource">AudioSource to play mumble clips on.</param>
        public void StartText(string text, float charsPerSecond,
            float pausePeriod = 0.3f, float pauseComma = 0.15f, float pauseExclamation = 0.25f,
            AudioClip[] voiceBank = null, AudioSource audioSource = null)
        {
            _fullText = text ?? string.Empty;
            _revealed.Clear();
            _charIndex = 0;
            _timer = 0f;
            _charsPerSecond = charsPerSecond;
            _pausePeriod = pausePeriod;
            _pauseComma = pauseComma;
            _pauseExclamation = pauseExclamation;
            _voiceBank = voiceBank;
            _audioSource = audioSource;
            _complete = false;

            _revealedTextDirty = true;

            if (_charsPerSecond <= 0f || _fullText.Length == 0)
            {
                _revealed.Append(_fullText);
                _charIndex = _fullText.Length;
                _complete = true;
                _revealedTextDirty = true;
                OnTextComplete?.Invoke();
            }
        }

        /// <summary>
        /// Call every frame to advance the reveal. Returns true if text changed.
        /// </summary>
        public bool Update(float deltaTime)
        {
            if (_complete)
                return false;

            _timer += deltaTime;
            float interval = 1f / _charsPerSecond;
            bool changed = false;

            while (_timer >= interval && _charIndex < _fullText.Length)
            {
                _timer -= interval;

                // Skip rich text tags entirely (reveal them as a block)
                if (_fullText[_charIndex] == '<')
                {
                    int tagEnd = _fullText.IndexOf('>', _charIndex);
                    if (tagEnd >= 0)
                    {
                        _revealed.Append(_fullText, _charIndex, tagEnd - _charIndex + 1);
                        _charIndex = tagEnd + 1;
                        changed = true;
                        continue;
                    }
                }

                char c = _fullText[_charIndex];
                _revealed.Append(c);
                _charIndex++;
                changed = true;

                OnCharacterRevealed?.Invoke();
                PlayMumble();

                // Punctuation pause — add time debt so we wait longer before next char
                if (_charIndex < _fullText.Length)
                {
                    float pause = GetPunctuationPause(c);
                    if (pause > 0f)
                        _timer -= pause;
                }

                if (_charIndex >= _fullText.Length)
                {
                    _complete = true;
                    OnTextComplete?.Invoke();
                    break;
                }
            }

            if (changed)
                _revealedTextDirty = true;

            return changed;
        }

        /// <summary>
        /// Skip to full text instantly.
        /// </summary>
        public void Skip()
        {
            if (_complete)
                return;

            _revealed.Clear();
            _revealed.Append(_fullText);
            _charIndex = _fullText.Length;
            _complete = true;
            _revealedTextDirty = true;
            OnTextComplete?.Invoke();
        }

        private float GetPunctuationPause(char c)
        {
            switch (c)
            {
                case '.': return _pausePeriod;
                case ',': return _pauseComma;
                case '!':
                case '?': return _pauseExclamation;
                default: return 0f;
            }
        }

        private void PlayMumble()
        {
            if (_voiceBank == null || _voiceBank.Length == 0 || _audioSource == null)
                return;

            // Play a random mumble clip — skip if already playing to avoid overlap
            if (_audioSource.isPlaying)
                return;

            int idx = UnityEngine.Random.Range(0, _voiceBank.Length);
            _audioSource.PlayOneShot(_voiceBank[idx]);
        }
    }
}
