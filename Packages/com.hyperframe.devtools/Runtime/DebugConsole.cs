using System.Collections.Generic;
using System.Linq;
using HyperFrame.App;
using HyperFrame.Core;
using HyperFrame.Services;
using UnityEngine;

namespace HyperFrame.DevTools
{
    /// <summary>
    /// In-game debug console (DV-01). Tap the small "DBG" button (top-left) or press F1 / backquote.
    /// Shows every command in <see cref="DebugCommands"/>, grouped by category, plus FPS and the live
    /// analytics log (AN-03). This assembly only compiles in the Editor and development builds (DV-02).
    /// </summary>
    public sealed class DebugConsole : MonoBehaviour
    {
        bool _open, _showFps = true, _showAnalytics;
        Vector2 _scroll;
        float _fps, _fpsTimer;
        int _frames;
        HyperFrameApp _registeredFor;
        GUIStyle _button, _label, _box;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<DebugConsole>() != null) return;
            var go = new GameObject("[DebugConsole]");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugConsole>();
        }

        void Update()
        {
            _frames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps = _frames / _fpsTimer;
                _frames = 0;
                _fpsTimer = 0f;
            }

            var app = HyperFrameApp.Instance;
            if (app != null && app.IsReady && _registeredFor != app)
            {
                _registeredFor = app;
                RegisterFrameworkCommands();
            }

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.f1Key.wasPressedThisFrame || kb.backquoteKey.wasPressedThisFrame)) _open = !_open;
#endif
        }

        static void RegisterFrameworkCommands()
        {
            DebugCommands.Register("Level", "Win", () => ServiceLocator.Get<GameFlow>().ForceFinish(true));
            DebugCommands.Register("Level", "Lose", () => ServiceLocator.Get<GameFlow>().ForceFinish(false));
            DebugCommands.Register("Level", "Next level", () => Jump(+1));
            DebugCommands.Register("Level", "Previous level", () => Jump(-1));
            DebugCommands.Register("Level", "+10 levels", () => Jump(+10));
            DebugCommands.Register("Economy", "+1000 coins", () => ServiceLocator.Get<IWallet>().Add(Currencies.Coins, 1000, "cheat"));
            DebugCommands.Register("Economy", "+100 gems", () => ServiceLocator.Get<IWallet>().Add(Currencies.Gems, 100, "cheat"));
            DebugCommands.Register("Ads", "Cycle mock result", CycleAdResult,
                () => ServiceLocator.TryGet<MockAdsService>(out var a) ? $"Mock ad result: {a.NextResult}" : "Mock ads: n/a");
            DebugCommands.Register("Ads", "Toggle No Ads", () =>
            {
                var ads = ServiceLocator.Get<IAdsService>();
                ads.AdsRemoved = !ads.AdsRemoved;
            }, () => $"No Ads: {(ServiceLocator.Get<IAdsService>().AdsRemoved ? "ON" : "OFF")}");
            DebugCommands.Register("Time", "Toggle slow-mo x0.25", () =>
            {
                var clock = ServiceLocator.Get<GameClock>();
                clock.TimeScale = clock.TimeScale < 1f ? 1f : 0.25f;
            });
            DebugCommands.Register("Save", "Save now", () => ServiceLocator.Get<ISaveService>().Save());
            DebugCommands.Register("Save", "Reset progress + reboot", () =>
            {
                ServiceLocator.Get<ISaveService>().DeleteAll();
                var console = FindAnyObjectByType<DebugConsole>();
                if (console != null) console.StartCoroutine(console.Reboot());
            });
        }

        /// <summary>Destroys the app and boots it again from the same definition (services rebuilt from the save).</summary>
        System.Collections.IEnumerator Reboot()
        {
            var app = HyperFrameApp.Instance;
            if (app == null) yield break;
            var definition = app.Definition;
            var options = app.Options;
            Destroy(app.gameObject);
            yield return null; // Destroy completes at the end of the frame
            _registeredFor = null;
            HyperFrameApp.Launch(definition, options);
        }

        static void Jump(int delta)
        {
            var progression = ServiceLocator.Get<IProgressionService>();
            int target = Mathf.Max(0, progression.CurrentLevelIndex + delta);
            progression.SetCurrentLevel(target);
            var flow = ServiceLocator.Get<GameFlow>();
            if (flow.State == GameFlowState.Gameplay) flow.Play(target);
            else flow.GoHome();
            ServiceLocator.Get<HyperFrame.UI.IUIService>().Toast($"Level {target + 1}");
        }

        static void CycleAdResult()
        {
            if (!ServiceLocator.TryGet<MockAdsService>(out var ads)) return;
            ads.NextResult = ads.NextResult == AdResult.Completed ? AdResult.Skipped
                : ads.NextResult == AdResult.Skipped ? AdResult.Failed : AdResult.Completed;
            ads.Presenter = null; // use the fixed result instead of the interactive popup
        }

        void EnsureStyles(float scale)
        {
            if (_button != null) return;
            int size = Mathf.RoundToInt(14 * scale);
            _button = new GUIStyle(GUI.skin.button) { fontSize = size };
            _label = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true };
            _box = new GUIStyle(GUI.skin.box) { fontSize = size, alignment = TextAnchor.UpperLeft };
        }

        void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / 960f);
            EnsureStyles(scale);
            float w = 64 * scale, h = 32 * scale;

            if (GUI.Button(new Rect(4, Screen.height - h - 4, w, h), _open ? "X" : "DBG", _button)) _open = !_open;
            if (_showFps) GUI.Label(new Rect(w + 12, Screen.height - h - 4, 200 * scale, h), $"{_fps:0} fps", _label);
            if (!_open) return;

            var area = new Rect(8, 8, Mathf.Min(Screen.width - 16, 520 * scale), Screen.height * 0.75f);
            GUI.Box(area, GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 8, area.width - 16, area.height - 16));
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label(HyperFrame.App.Testing.AppDriver.Describe(), _label);
            foreach (var group in DebugCommands.All.GroupBy(c => c.Category))
            {
                GUILayout.Label($"— {group.Key} —", _label);
                foreach (var command in group)
                {
                    var text = command.Label != null ? command.Label() : command.Name;
                    if (GUILayout.Button(text, _button, GUILayout.Height(h))) command.Run?.Invoke();
                }
            }

            GUILayout.Label("— Tools —", _label);
            if (GUILayout.Button($"FPS: {(_showFps ? "ON" : "OFF")}", _button, GUILayout.Height(h))) _showFps = !_showFps;
            if (GUILayout.Button($"Analytics log: {(_showAnalytics ? "ON" : "OFF")}", _button, GUILayout.Height(h))) _showAnalytics = !_showAnalytics;
            if (_showAnalytics && ServiceLocator.TryGet<InMemoryAnalyticsBackend>(out var log))
            {
                IEnumerable<AnalyticsEvent> recent = log.Events.Skip(Mathf.Max(0, log.Events.Count - 15)).Reverse();
                foreach (var e in recent) GUILayout.Label(e.ToString(), _label);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
