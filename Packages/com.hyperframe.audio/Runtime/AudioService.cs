using System.Collections.Generic;
using HyperFrame.Core;
using UnityEngine;

namespace HyperFrame.Audio
{
    public interface IAudioService
    {
        /// <summary>Plays a sound effect by ID. Returns false if unknown, muted, or voice-limited.</summary>
        bool PlaySfx(string id, float volumeScale = 1f);
        /// <summary>Crossfades to a music track by ID. Same track = no-op.</summary>
        void PlayMusic(string id, float crossfadeSeconds = 0.8f);
        void StopMusic(float fadeSeconds = 0.5f);
        string CurrentMusic { get; }

        float MusicVolume { get; set; }
        float SfxVolume { get; set; }
        bool MusicMuted { get; set; }
        bool SfxMuted { get; set; }
    }

    /// <summary>
    /// Unity audio implementation (AU-01): a pool of SFX sources with per-sound voice limits and pitch
    /// variance, and two music sources for crossfades. Unknown IDs log once and stay silent.
    /// </summary>
    public sealed class AudioService : IAudioService
    {
        readonly SoundTable _table;
        readonly TweenEngine _tweens;
        readonly List<AudioSource> _sfx = new List<AudioSource>();
        readonly Dictionary<string, int> _voices = new Dictionary<string, int>();
        readonly Dictionary<AudioSource, string> _sourceIds = new Dictionary<AudioSource, string>();
        readonly Dictionary<string, float> _lastPlay = new Dictionary<string, float>();
        readonly HashSet<string> _warned = new HashSet<string>();
        readonly AudioSource[] _music = new AudioSource[2];
        int _activeMusic;
        float _musicVolume = 1f, _sfxVolume = 1f;
        bool _musicMuted, _sfxMuted;
        float _currentMusicEntryVolume = 1f;
        TweenHandle _fadeIn, _fadeOut;

        public string CurrentMusic { get; private set; }

        public AudioService(SoundTable table, TweenEngine tweens, Transform parent, int sfxVoices = 16)
        {
            _table = table != null ? table : ScriptableObject.CreateInstance<SoundTable>();
            _tweens = tweens;
            var root = new GameObject("Audio");
            root.transform.SetParent(parent, false);
            for (int i = 0; i < sfxVoices; i++)
            {
                var src = root.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _sfx.Add(src);
            }
            for (int i = 0; i < 2; i++)
            {
                var src = root.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.volume = 0f;
                _music[i] = src;
            }
        }

        public float MusicVolume { get => _musicVolume; set { _musicVolume = Mathf.Clamp01(value); ApplyMusicVolume(); } }
        public float SfxVolume { get => _sfxVolume; set => _sfxVolume = Mathf.Clamp01(value); }
        public bool MusicMuted { get => _musicMuted; set { _musicMuted = value; ApplyMusicVolume(); } }
        public bool SfxMuted { get => _sfxMuted; set => _sfxMuted = value; }

        float MusicTarget => _musicMuted ? 0f : _musicVolume * _currentMusicEntryVolume;

        public bool PlaySfx(string id, float volumeScale = 1f)
        {
            if (_sfxMuted || string.IsNullOrEmpty(id)) return false;
            if (!_table.TryGet(id, out var entry) || entry.clips == null || entry.clips.Length == 0)
            {
                if (_warned.Add(id)) HFLog.Warn("Audio", $"Sound '{id}' is not in the SoundTable (or has no clips).");
                return false;
            }

            float now = Time.unscaledTime;
            if (_lastPlay.TryGetValue(id, out var last) && now - last < entry.cooldown) return false;
            RefreshVoices();
            _voices.TryGetValue(id, out var playing);
            if (playing >= Mathf.Max(1, entry.maxVoices)) return false;

            var src = FreeSource();
            if (src == null) return false;
            src.clip = entry.clips[Random.Range(0, entry.clips.Length)];
            src.pitch = Random.Range(entry.pitch.x, entry.pitch.y);
            src.volume = entry.volume * _sfxVolume * volumeScale;
            src.Play();
            _sourceIds[src] = id;
            _voices[id] = playing + 1;
            _lastPlay[id] = now;
            return true;
        }

        void RefreshVoices()
        {
            _voices.Clear();
            foreach (var src in _sfx)
            {
                if (!src.isPlaying || !_sourceIds.TryGetValue(src, out var id)) continue;
                _voices.TryGetValue(id, out var n);
                _voices[id] = n + 1;
            }
        }

        AudioSource FreeSource()
        {
            foreach (var src in _sfx)
                if (!src.isPlaying) return src;
            return null; // all voices busy: drop the sound rather than cut another
        }

        public void PlayMusic(string id, float crossfadeSeconds = 0.8f)
        {
            if (id == CurrentMusic) return;
            if (!_table.TryGet(id, out var entry) || entry.clips == null || entry.clips.Length == 0)
            {
                if (_warned.Add(id)) HFLog.Warn("Audio", $"Music '{id}' is not in the SoundTable (or has no clips).");
                StopMusic(crossfadeSeconds);
                CurrentMusic = id;
                return;
            }
            CurrentMusic = id;
            var outgoing = _music[_activeMusic];
            _activeMusic = 1 - _activeMusic;
            var incoming = _music[_activeMusic];
            _currentMusicEntryVolume = entry.volume;

            incoming.clip = entry.clips[Random.Range(0, entry.clips.Length)];
            incoming.volume = 0f;
            incoming.Play();

            _fadeIn.Kill();
            _fadeOut.Kill();
            float outStart = outgoing.volume;
            _fadeOut = _tweens.To(outStart, 0f, crossfadeSeconds, v => outgoing.volume = v, Ease.Linear, TimeMode.Unscaled)
                .OnComplete(() => outgoing.Stop());
            _fadeIn = _tweens.To(0f, 1f, crossfadeSeconds, v => incoming.volume = v * MusicTarget, Ease.Linear, TimeMode.Unscaled);
        }

        public void StopMusic(float fadeSeconds = 0.5f)
        {
            CurrentMusic = null;
            var src = _music[_activeMusic];
            _fadeIn.Kill();
            _fadeOut.Kill();
            _fadeOut = _tweens.To(src.volume, 0f, fadeSeconds, v => src.volume = v, Ease.Linear, TimeMode.Unscaled)
                .OnComplete(() => src.Stop());
        }

        void ApplyMusicVolume()
        {
            if (_fadeIn.IsActive) return; // the running fade reads MusicTarget every frame
            var src = _music[_activeMusic];
            if (src.isPlaying) src.volume = MusicTarget;
        }
    }

    /// <summary>Records calls instead of playing audio. Default in tests.</summary>
    public sealed class MockAudioService : IAudioService
    {
        public readonly List<string> Played = new List<string>();
        public string CurrentMusic { get; private set; }
        public float MusicVolume { get; set; } = 1f;
        public float SfxVolume { get; set; } = 1f;
        public bool MusicMuted { get; set; }
        public bool SfxMuted { get; set; }

        public bool PlaySfx(string id, float volumeScale = 1f)
        {
            if (SfxMuted) return false;
            Played.Add(id);
            return true;
        }

        public void PlayMusic(string id, float crossfadeSeconds = 0.8f) => CurrentMusic = id;
        public void StopMusic(float fadeSeconds = 0.5f) => CurrentMusic = null;
    }
}
