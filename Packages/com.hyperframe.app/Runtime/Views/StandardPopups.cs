using System.Threading.Tasks;
using HyperFrame.Core;
using HyperFrame.Feedback;
using HyperFrame.Services;
using HyperFrame.UI;
using UnityEngine;

namespace HyperFrame.App
{
    /// <summary>Level complete: stars, reward, Continue or watch an ad to multiply (UI-04).</summary>
    public sealed class WinPopup : UIPopup
    {
        public const string ActionContinue = "continue";
        public const string ActionMultiply = "multiply";

        public sealed class Args
        {
            public int LevelIndex;
            public int Stars;
            public int Reward;
            public int Multiplier = 2;
        }

        public WinPopup() => closeOnBack = false;

        protected override void BuildPlaceholder(UIBuilder ui)
        {
            var panel = ui.Panel(transform, new Vector2(880f, 1000f));
            ui.Title(panel, "txt_Title", "Level Complete!");
            ui.Label(panel, "txt_Stars", "***", 110, ThemeColor.Accent);
            ui.Label(panel, "txt_Reward", "+0");
            ui.Button(panel, "btn_Multiply", "x2 (watch ad)", ThemeColor.Secondary);
            ui.Button(panel, "btn_Continue", "Continue");
        }

        protected override void OnCreated()
        {
            Bind("btn_Continue", () => Close(ActionContinue));
            Bind("btn_Multiply", () => Close(ActionMultiply));
        }

        protected override void OnShow(object args)
        {
            var a = args as Args ?? new Args();
            SetText("txt_Title", $"Level {a.LevelIndex + 1} Complete!");
            SetText("txt_Stars", new string('*', Mathf.Clamp(a.Stars, 0, 3)) + new string('-', 3 - Mathf.Clamp(a.Stars, 0, 3)));
            SetText("txt_Reward", $"+{a.Reward}");
            SetText("btn_Multiply", $"x{a.Multiplier} (watch ad)");
            bool canMultiply = a.Reward > 0 && a.Multiplier > 1 &&
                               ServiceLocator.TryGet<IAdsService>(out var ads) && ads.IsReady(AdFormat.Rewarded);
            SetActive("btn_Multiply", canMultiply);
        }
    }

    /// <summary>Level failed: Retry or Home (UI-04).</summary>
    public sealed class LosePopup : UIPopup
    {
        public const string ActionRetry = "retry";
        public const string ActionHome = "home";

        protected override void BuildPlaceholder(UIBuilder ui)
        {
            var panel = ui.Panel(transform, new Vector2(880f, 820f));
            ui.Title(panel, "txt_Title", "Level Failed");
            ui.Label(panel, "txt_Reason", "");
            ui.Button(panel, "btn_Retry", "Retry");
            ui.Button(panel, "btn_Home", "Home", ThemeColor.Secondary);
        }

        protected override void OnCreated()
        {
            Bind("btn_Retry", () => Close(ActionRetry));
            Bind("btn_Home", () => Close(ActionHome));
        }

        protected override void OnShow(object args)
        {
            var reason = args is LevelOutcome o ? o.Reason : null;
            SetText("txt_Reason", string.IsNullOrEmpty(reason) ? "" : reason.Replace('_', ' '));
        }

        public override bool OnBack()
        {
            Close(ActionHome);
            return true;
        }
    }

    /// <summary>Pause: Resume / Restart / Settings / Home (UI-04). Back = Resume.</summary>
    public sealed class PausePopup : UIPopup
    {
        public const string ActionResume = "resume";
        public const string ActionRestart = "restart";
        public const string ActionHome = "home";

        protected override void BuildPlaceholder(UIBuilder ui)
        {
            var panel = ui.Panel(transform, new Vector2(880f, 1080f));
            ui.Title(panel, "txt_Title", "Paused");
            ui.Button(panel, "btn_Resume", "Resume");
            ui.Button(panel, "btn_Restart", "Restart", ThemeColor.Secondary);
            ui.Button(panel, "btn_Settings", "Settings", ThemeColor.Secondary);
            ui.Button(panel, "btn_Home", "Home", ThemeColor.Secondary);
        }

        protected override void OnCreated()
        {
            Bind("btn_Resume", () => Close(ActionResume));
            Bind("btn_Restart", () => Close(ActionRestart));
            Bind("btn_Home", () => Close(ActionHome));
            Bind("btn_Settings", () => UI.ShowPopup<SettingsPopup>(null, PopupOptions.Stacked).Forget("Pause.Settings"));
        }

        public override bool OnBack()
        {
            Close(ActionResume);
            return true;
        }
    }

    /// <summary>Settings: music, SFX, haptics, privacy, restore purchases, version (ST-01, UI-04).</summary>
    public sealed class SettingsPopup : UIPopup
    {
        ISettingsService _settings;

        protected override void BuildPlaceholder(UIBuilder ui)
        {
            var panel = ui.Panel(transform, new Vector2(880f, 1300f));
            ui.Title(panel, "txt_Title", "Settings");
            ui.Button(panel, "btn_Music", "Music: ON", ThemeColor.Secondary);
            ui.Button(panel, "btn_Sfx", "Sound: ON", ThemeColor.Secondary);
            ui.Button(panel, "btn_Haptics", "Vibration: ON", ThemeColor.Secondary);
            ui.Button(panel, "btn_Privacy", "Privacy Policy", ThemeColor.Secondary);
            ui.Button(panel, "btn_Restore", "Restore Purchases", ThemeColor.Secondary);
            ui.Label(panel, "txt_Version", "v0.0.0", 32);
            ui.Button(panel, "btn_Close", "Close");
        }

        protected override void OnCreated()
        {
            _settings = ServiceLocator.Get<ISettingsService>();
            Bind("btn_Music", () => _settings.MusicOn.Value = !_settings.MusicOn.Value);
            Bind("btn_Sfx", () => _settings.SfxOn.Value = !_settings.SfxOn.Value);
            Bind("btn_Haptics", () =>
            {
                _settings.HapticsOn.Value = !_settings.HapticsOn.Value;
                if (_settings.HapticsOn.Value && ServiceLocator.TryGet<IFeedbackService>(out var fx)) fx.Haptic(HapticType.Medium);
            });
            Bind("btn_Privacy", () =>
            {
                if (!string.IsNullOrEmpty(_settings.PrivacyPolicyUrl)) Application.OpenURL(_settings.PrivacyPolicyUrl);
                else UI.Toast("No privacy policy URL set in GameDefinition");
            });
            Bind("btn_Restore", () => RestoreAsync().Forget("Settings.Restore"));
            Bind("btn_Close", () => Close());
            _settings.MusicOn.Changed += _ => Refresh();
            _settings.SfxOn.Changed += _ => Refresh();
            _settings.HapticsOn.Changed += _ => Refresh();
        }

        async Task RestoreAsync()
        {
            if (!ServiceLocator.TryGet<IIAPService>(out var iap)) return;
            bool ok = await iap.RestorePurchases();
            UI.Toast(ok ? "Purchases restored" : "Restore failed");
        }

        protected override void OnShow(object args) => Refresh();

        void Refresh()
        {
            if (_settings == null) return;
            SetText("btn_Music", "Music: " + (_settings.MusicOn.Value ? "ON" : "OFF"));
            SetText("btn_Sfx", "Sound: " + (_settings.SfxOn.Value ? "ON" : "OFF"));
            SetText("btn_Haptics", "Vibration: " + (_settings.HapticsOn.Value ? "ON" : "OFF"));
            SetText("txt_Version", "v" + _settings.AppVersion);
        }
    }

    /// <summary>Editor/dev mock ad with Complete / Skip / Fail buttons (AD-03).</summary>
    public sealed class MockAdPopup : UIPopup
    {
        public MockAdPopup() => transition = ViewTransition.Fade;

        protected override void BuildPlaceholder(UIBuilder ui)
        {
            ui.Background(transform, ThemeColor.Background, "img_AdBackground");
            var column = ui.Column(transform);
            ui.Title(column, "txt_Title", "MOCK AD", ThemeColor.TextOnPrimary);
            ui.Label(column, "txt_Placement", "", 0, ThemeColor.TextOnPrimary);
            ui.Button(column, "btn_Complete", "Complete");
            ui.Button(column, "btn_Skip", "Skip", ThemeColor.Secondary);
            ui.Button(column, "btn_Fail", "Fail", ThemeColor.Negative);
        }

        protected override void OnCreated()
        {
            Bind("btn_Complete", () => Close(nameof(AdResult.Completed)));
            Bind("btn_Skip", () => Close(nameof(AdResult.Skipped)));
            Bind("btn_Fail", () => Close(nameof(AdResult.Failed)));
        }

        protected override void OnShow(object args) => SetText("txt_Placement", args as string ?? "");

        public override bool OnBack()
        {
            Close(nameof(AdResult.Skipped));
            return true;
        }

        /// <summary>Use as MockAdsService.Presenter.</summary>
        public static async Task<AdResult> Present(IUIService ui, AdFormat format, string placement)
        {
            var result = await ui.ShowPopup<MockAdPopup>($"{format} · {placement}", new PopupOptions { Stack = true, Priority = 100 });
            return System.Enum.TryParse<AdResult>(result.Action, out var parsed) ? parsed : AdResult.Skipped;
        }
    }
}
