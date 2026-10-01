using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperFrame.Audio
{
    public enum SoundBus { Sfx, Music }

    [Serializable]
    public sealed class SoundEntry
    {
        [Tooltip("ID used in code, e.g. \"ui_click\". Lowercase snake_case.")]
        public string id;
        public SoundBus bus = SoundBus.Sfx;
        [Tooltip("One is picked at random each play.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random pitch range (min, max). (1,1) = no variance.")]
        public Vector2 pitch = Vector2.one;
        [Tooltip("Max simultaneous voices of this sound (SFX only).")]
        public int maxVoices = 4;
        [Tooltip("Ignore new plays within this many seconds of the last one.")]
        public float cooldown = 0.03f;
    }

    /// <summary>All sounds of a game, addressed by ID (AU-02). Register new sounds here, not in code.</summary>
    [CreateAssetMenu(menuName = "HyperFrame/Audio/Sound Table", fileName = "SoundTable")]
    public sealed class SoundTable : ScriptableObject
    {
        public List<SoundEntry> entries = new List<SoundEntry>();

        Dictionary<string, SoundEntry> _lookup;

        public bool TryGet(string id, out SoundEntry entry)
        {
            if (_lookup == null || _lookup.Count != entries.Count)
            {
                _lookup = new Dictionary<string, SoundEntry>();
                foreach (var e in entries)
                    if (e != null && !string.IsNullOrEmpty(e.id)) _lookup[e.id] = e;
            }
            return _lookup.TryGetValue(id, out entry);
        }

        public void Add(SoundEntry entry)
        {
            entries.Add(entry);
            _lookup = null;
        }
    }

    /// <summary>Standard sound IDs used by framework UI and flow. Games add their own.</summary>
    public static class SoundIds
    {
        public const string UiClick = "ui_click";
        public const string PopupOpen = "popup_open";
        public const string Win = "win";
        public const string Lose = "lose";
        public const string Coin = "coin";
        public const string Pop = "pop";
        public const string Error = "error";
        public const string MusicHome = "music_home";
        public const string MusicGameplay = "music_gameplay";
    }
}
