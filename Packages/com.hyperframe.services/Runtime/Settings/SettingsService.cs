using System;
using HyperFrame.Core;

namespace HyperFrame.Services
{
    [Serializable]
    public sealed class SettingsData
    {
        public bool musicOn = true;
        public bool sfxOn = true;
        public bool hapticsOn = true;
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
        /// <summary>Empty = follow the device language.</summary>
        public string language = "";
    }

    /// <summary>Player settings (ST-01), persisted in the "settings" save section. Bind UI to the observables.</summary>
    public interface ISettingsService
    {
        Observable<bool> MusicOn { get; }
        Observable<bool> SfxOn { get; }
        Observable<bool> HapticsOn { get; }
        Observable<float> MusicVolume { get; }
        Observable<float> SfxVolume { get; }
        Observable<string> Language { get; }
        string AppVersion { get; }
        string PrivacyPolicyUrl { get; }
    }

    public sealed class SettingsService : ISettingsService
    {
        public const string Section = "settings";
        readonly ISaveService _save;

        public Observable<bool> MusicOn { get; } = new Observable<bool>(true);
        public Observable<bool> SfxOn { get; } = new Observable<bool>(true);
        public Observable<bool> HapticsOn { get; } = new Observable<bool>(true);
        public Observable<float> MusicVolume { get; } = new Observable<float>(1f);
        public Observable<float> SfxVolume { get; } = new Observable<float>(1f);
        public Observable<string> Language { get; } = new Observable<string>("");
        public string AppVersion { get; }
        public string PrivacyPolicyUrl { get; }

        public SettingsService(ISaveService save, string appVersion = "0.0.0", string privacyPolicyUrl = "")
        {
            _save = save;
            AppVersion = appVersion;
            PrivacyPolicyUrl = privacyPolicyUrl;

            var d = save.Get<SettingsData>(Section);
            MusicOn.SetSilently(d.musicOn);
            SfxOn.SetSilently(d.sfxOn);
            HapticsOn.SetSilently(d.hapticsOn);
            MusicVolume.SetSilently(d.musicVolume);
            SfxVolume.SetSilently(d.sfxVolume);
            Language.SetSilently(d.language ?? "");

            MusicOn.Changed += _ => Persist();
            SfxOn.Changed += _ => Persist();
            HapticsOn.Changed += _ => Persist();
            MusicVolume.Changed += _ => Persist();
            SfxVolume.Changed += _ => Persist();
            Language.Changed += _ => Persist();
        }

        void Persist() => _save.Set(Section, new SettingsData
        {
            musicOn = MusicOn.Value,
            sfxOn = SfxOn.Value,
            hapticsOn = HapticsOn.Value,
            musicVolume = MusicVolume.Value,
            sfxVolume = SfxVolume.Value,
            language = Language.Value
        });
    }
}
