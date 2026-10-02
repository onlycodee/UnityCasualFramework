using System;
using HyperFrame.Audio;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Sound IDs used by Pixel Loop, and procedural clips for them so the game is fully playable before
    /// real audio arrives. Drop real clips into a SoundTable asset with the same IDs to replace any of them.
    /// </summary>
    public static class PixelLoopSounds
    {
        public const string Shot = "pl_shot";
        public const string Launch = "pl_launch";
        public const string Dock = "pl_dock";
        public const string Spent = "pl_spent";
        public const string Deny = "pl_deny";
        public const string Warn = "pl_warn";
        public const string Reveal = "pl_reveal";
        public const int HitSteps = 8;

        /// <summary>Pixel hit, rising in pitch with the combo (0 … HitSteps-1).</summary>
        public static string Hit(int combo) => HitIds[Mathf.Clamp(combo, 0, HitSteps - 1)];
        static readonly string[] HitIds = { "pl_hit_0", "pl_hit_1", "pl_hit_2", "pl_hit_3", "pl_hit_4", "pl_hit_5", "pl_hit_6", "pl_hit_7" };

        const int Rate = 44100;
        static readonly float[] Pentatonic = { 1f, 9f / 8f, 5f / 4f, 3f / 2f, 5f / 3f, 2f, 9f / 4f, 5f / 2f };

        public static void Configure(SoundTable table)
        {
            Add(table, Shot, Make("pl_shot", 0.07f, (t, i) => Square(Mathf.Lerp(1500f, 520f, t / 0.07f), i) * Decay(t, 0.05f) * 0.18f),
                0.9f, new Vector2(0.94f, 1.08f), voices: 6);
            for (int k = 0; k < HitSteps; k++)
            {
                float f = 587.33f * Pentatonic[k];
                Add(table, HitIds[k], Make($"pl_hit_{k}", 0.22f, (t, i) =>
                        (Sine(f, t) * 0.7f + Sine(f * 2f, t) * 0.25f + Sine(f * 3.01f, t) * 0.08f) * Decay(t, 0.09f) * 0.42f
                        + Noise(i) * Decay(t, 0.006f) * 0.25f),
                    0.9f, new Vector2(0.99f, 1.01f), voices: 3);
            }
            Add(table, Launch, Make("pl_launch", 0.22f, (t, i) =>
                Noise(i) * Mathf.Sin(Mathf.PI * t / 0.22f) * 0.22f * (0.4f + t / 0.22f)
                + Sine(Mathf.Lerp(140f, 320f, t / 0.22f), t) * Decay(t, 0.08f) * 0.35f), 0.8f, new Vector2(0.95f, 1.05f));
            Add(table, Dock, Make("pl_dock", 0.16f, (t, i) =>
                Sine(Mathf.Lerp(220f, 90f, t / 0.16f), t) * Decay(t, 0.06f) * 0.6f + Noise(i) * Decay(t, 0.01f) * 0.2f), 0.9f, Vector2.one);
            Add(table, Spent, Make("pl_spent", 0.3f, (t, i) =>
                (t < 0.08f ? Sine(1318.5f, t) : Sine(1975.5f, t)) * Decay(t < 0.08f ? t : t - 0.08f, 0.1f) * 0.3f), 0.8f, new Vector2(0.98f, 1.03f));
            Add(table, Deny, Make("pl_deny", 0.14f, (t, i) => Square(150f, i) * Decay(t, 0.08f) * 0.18f), 0.8f, Vector2.one);
            Add(table, Warn, Make("pl_warn", 0.32f, (t, i) => Sine(t < 0.16f ? 880f : 660f, t) * Mathf.Sin(Mathf.PI * (t % 0.16f) / 0.16f) * 0.35f), 0.8f, Vector2.one);
            Add(table, Reveal, Make("pl_reveal", 1.1f, (t, i) =>
            {
                float v = 0f;
                for (int n = 0; n < 6; n++)
                {
                    float start = n * 0.09f;
                    if (t < start) break;
                    v += Sine(523.25f * Pentatonic[n + 2] / Pentatonic[2], t - start) * Decay(t - start, 0.35f) * 0.16f;
                }
                return v;
            }), 0.9f, Vector2.one, voices: 1);

            var music = MakeMusic();
            if (!table.TryGet(SoundIds.MusicGameplay, out _))
                table.Add(new SoundEntry { id = SoundIds.MusicGameplay, bus = SoundBus.Music, clips = new[] { music }, volume = 0.32f });
            if (!table.TryGet(SoundIds.MusicHome, out _))
                table.Add(new SoundEntry { id = SoundIds.MusicHome, bus = SoundBus.Music, clips = new[] { music }, volume = 0.25f });
        }

        static void Add(SoundTable table, string id, AudioClip clip, float volume, Vector2 pitch, int voices = 4)
        {
            if (table.TryGet(id, out var existing) && existing.clips != null && existing.clips.Length > 0) return;
            table.Add(new SoundEntry { id = id, clips = new[] { clip }, volume = volume, pitch = pitch, maxVoices = voices, cooldown = 0.02f });
        }

        static AudioClip Make(string name, float seconds, Func<float, int, float> sample)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float fade = Mathf.Min(1f, i / 64f) * Mathf.Min(1f, (n - i) / 256f); // no clicks at the ends
                data[i] = Mathf.Clamp(sample(t, i) * fade, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sine(float freq, float t) => Mathf.Sin(2f * Mathf.PI * freq * t);
        static float Square(float freq, int i) => ((i * freq / Rate) % 1f) < 0.5f ? 1f : -1f;
        static float Decay(float t, float tau) => Mathf.Exp(-t / tau);

        static uint _noise = 22222;
        static float Noise(int i)
        {
            _noise ^= _noise << 13;
            _noise ^= _noise >> 17;
            _noise ^= _noise << 5;
            return (_noise & 0xFFFF) / 32768f - 1f;
        }

        /// <summary>A calm 8-second loop: soft pad chords (C – Am – F – G) under a plucked arpeggio, 120 bpm.</summary>
        static AudioClip MakeMusic()
        {
            const float bar = 2f;
            float[][] chords =
            {
                new[] { 261.63f, 329.63f, 392.00f, 523.25f },
                new[] { 220.00f, 261.63f, 329.63f, 440.00f },
                new[] { 174.61f, 220.00f, 261.63f, 349.23f },
                new[] { 196.00f, 246.94f, 293.66f, 392.00f },
            };
            int[] pattern = { 0, 1, 2, 3, 2, 1, 2, 3 };
            int n = Mathf.CeilToInt(bar * 4 * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                int c = Mathf.Min(3, (int)(t / bar));
                float inBar = t - c * bar;
                var chord = chords[c];
                // pad: slow swell per bar, gentle detune
                float swell = Mathf.Sin(Mathf.PI * inBar / bar);
                float pad = 0f;
                for (int k = 0; k < 3; k++) pad += Sine(chord[k] * 0.5f, t) + Sine(chord[k] * 0.5f * 1.003f, t) * 0.6f;
                pad *= 0.025f * (0.35f + 0.65f * swell);
                // pluck: eighth notes
                float step = 0.25f;
                int s = (int)(inBar / step);
                float ts = inBar - s * step;
                float f = chord[pattern[s % pattern.Length]] * 2f;
                float pluck = (Sine(f, ts) * 0.8f + Sine(f * 2f, ts) * 0.2f) * Decay(ts, 0.12f) * 0.07f;
                // soft kick on beats
                float tb = inBar % 0.5f;
                float kick = Sine(Mathf.Lerp(110f, 45f, Mathf.Clamp01(tb / 0.12f)), tb) * Decay(tb, 0.06f) * 0.09f;
                float edge = Mathf.Min(1f, i / 512f) * Mathf.Min(1f, (n - i) / 512f); // seamless loop point
                data[i] = (pad + pluck + kick) * edge;
            }
            var clip = AudioClip.Create("pl_music", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
