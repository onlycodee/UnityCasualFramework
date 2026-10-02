using UnityEngine;

namespace HyperFrame.Audio
{
    /// <summary>
    /// Procedural placeholder sounds (AI-14) so gameplay never waits for audio assets.
    /// <see cref="FillMissing"/> adds a generated clip for every standard ID that has none.
    /// </summary>
    public static class PlaceholderSounds
    {
        const int SampleRate = 44100;

        public static void FillMissing(SoundTable table)
        {
            Add(table, SoundIds.UiClick, Blip("ph_click", 900f, 0.05f, 0.4f));
            Add(table, SoundIds.PopupOpen, Sweep("ph_popup", 500f, 900f, 0.12f, 0.3f));
            Add(table, SoundIds.Pop, Blip("ph_pop", 650f, 0.08f, 0.5f), new Vector2(0.9f, 1.15f));
            Add(table, SoundIds.Coin, Sweep("ph_coin", 1200f, 1800f, 0.1f, 0.35f), new Vector2(0.95f, 1.1f));
            Add(table, SoundIds.Error, Sweep("ph_error", 300f, 180f, 0.18f, 0.4f));
            Add(table, SoundIds.Win, Arpeggio("ph_win", new[] { 523f, 659f, 784f, 1047f }, 0.12f, 0.4f));
            Add(table, SoundIds.Lose, Arpeggio("ph_lose", new[] { 392f, 330f, 262f }, 0.18f, 0.4f));
        }

        static void Add(SoundTable table, string id, AudioClip clip, Vector2? pitch = null)
        {
            if (table.TryGet(id, out var existing) && existing.clips != null && existing.clips.Length > 0) return;
            table.Add(new SoundEntry { id = id, clips = new[] { clip }, pitch = pitch ?? Vector2.one, volume = 0.8f });
        }

        public static AudioClip Blip(string name, float freq, float seconds, float volume) =>
            Sweep(name, freq, freq, seconds, volume);

        public static AudioClip Sweep(string name, float from, float to, float seconds, float volume)
        {
            int n = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = Mathf.Lerp(from, to, t);
                phase += 2 * Mathf.PI * f / SampleRate;
                float env = Mathf.Min(1f, i / 200f) * (1f - t); // quick attack, linear decay
                data[i] = (float)System.Math.Sin(phase) * env * volume;
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip Arpeggio(string name, float[] notes, float noteSeconds, float volume)
        {
            int per = Mathf.CeilToInt(noteSeconds * SampleRate);
            var data = new float[per * notes.Length];
            for (int k = 0; k < notes.Length; k++)
            {
                double phase = 0;
                for (int i = 0; i < per; i++)
                {
                    float t = (float)i / per;
                    phase += 2 * Mathf.PI * notes[k] / SampleRate;
                    data[k * per + i] = (float)System.Math.Sin(phase) * Mathf.Min(1f, i / 200f) * (1f - t * 0.7f) * volume;
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
