using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HyperFrame.Audio;
using HyperFrame.Core;
using HyperFrame.Feedback;
using HyperFrame.Input;
using HyperFrame.Services;
using HyperFrame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperFrame.App
{
    /// <summary>Knobs for tests and special builds. Defaults match a normal game run.</summary>
    public sealed class AppOptions
    {
        /// <summary>Keep saves in memory (tests). Otherwise files under persistentDataPath/hyperframe.</summary>
        public bool InMemorySave;
        /// <summary>Explicit storage (tests that reboot the app over the same data). Overrides InMemorySave.</summary>
        public ISaveStorage SaveStorage;
        /// <summary>Use MockAudioService (silent, records calls).</summary>
        public bool MockAudio;
        /// <summary>Show the Complete/Skip/Fail popup for mock ads. Off = return MockAdResult immediately.</summary>
        public bool InteractiveMockAds = true;
        public AdResult MockAdResult = AdResult.Completed;
        /// <summary>Seed data written before services load (tests: start at level N, with coins...).</summary>
        public Action<ISaveService> PrepareSave;

        public static AppOptions ForTests() => new AppOptions { InMemorySave = true, MockAudio = true, InteractiveMockAds = false };
    }

    /// <summary>
    /// Entry point (CORE-01). Boots every framework service in a declared order with timeouts, then
    /// starts the <see cref="GameFlow"/>. Starts automatically in the scene named "Boot" when
    /// Assets/_Game/Resources/GameDefinition.asset exists; tests call <see cref="Launch"/>.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class HyperFrameApp : MonoBehaviour
    {
        public const string DefinitionResourcePath = "GameDefinition";
        public const string BootSceneName = "Boot";

        [SerializeField] GameDefinition definition;

        public static HyperFrameApp Instance { get; private set; }
        public GameDefinition Definition => definition;
        public AppOptions Options { get; private set; } = new AppOptions();
        public BootReport BootReport { get; private set; }
        public GameFlow Flow { get; private set; }
        public bool IsReady { get; private set; }
        public bool BootFailed { get; private set; }

        readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        HyperFrameRunner _runner;
        UIRoot _uiRoot;
        Camera _camera;

        /// <summary>Creates and boots the app from code (tests, custom bootstraps).</summary>
        public static HyperFrameApp Launch(GameDefinition definition, AppOptions options = null)
        {
            if (Instance != null) throw new InvalidOperationException("HyperFrameApp is already running. Destroy it first.");
            var go = new GameObject("HyperFrameApp");
            go.SetActive(false);
            var app = go.AddComponent<HyperFrameApp>();
            app.definition = definition;
            app.Options = options ?? new AppOptions();
            go.SetActive(true);
            return app;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (Instance != null || FindAnyObjectByType<HyperFrameApp>() != null) return;
            var scene = SceneManager.GetActiveScene().name;
            if (scene.StartsWith("InitTestScene", StringComparison.Ordinal)) return; // Test Runner
            var def = Resources.Load<GameDefinition>(DefinitionResourcePath);
            if (def == null) return;
            if (scene != BootSceneName && !def.autoBootInAnyScene) return;
            Launch(def);
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (Instance != this) return;
            if (definition == null)
            {
                HFLog.Error("App", "No GameDefinition assigned. Run 'HyperFrame/Setup Project' or assign one.");
                BootFailed = true;
                return;
            }
            BootAsync().Forget("App.Boot");
        }

        async Task BootAsync()
        {
            var boot = BuildBootSequence();
            BootReport = await boot.RunAsync(_lifetime.Token);
            if (this == null) return;
            if (!BootReport.Success)
            {
                BootFailed = true;
                HFLog.Error("App", "Boot failed:\n" + BootReport);
                if (ServiceLocator.TryGet<IUIService>(out var ui)) ui.SetLoading(true, "Something went wrong.\nPlease restart.");
                return;
            }
            IsReady = true;
            Flow.Start();
        }

        BootSequence BuildBootSequence()
        {
            var def = definition;
            var boot = new BootSequence();
            EventBus bus = null;
            GameClock clock = null;
            TweenEngine tweens = null;
            SaveService save = null;
            AnalyticsDispatcher analytics = null;
            InputService input = null;

            boot.Add("Validate definition", () =>
            {
                if (def.gameplay == null) throw new InvalidOperationException($"GameDefinition '{def.name}' has no GameplayModule.");
            });

            boot.Add("Core", () =>
            {
                ServiceLocator.Reset();
                bus = new EventBus();
                clock = new GameClock();
                tweens = new TweenEngine(clock);
                _runner = HyperFrameRunner.Create(clock, tweens, bus);
                _runner.transform.SetParent(transform, false);
                ServiceLocator.Register<IEventBus>(bus);
                ServiceLocator.Register<IGameClock>(clock);
                ServiceLocator.Register(clock);
                ServiceLocator.Register(tweens);
                ServiceLocator.Register<IPoolService>(new PoolService(_runner.transform, tweens));
                EnsureCamera();
            });

            boot.Add("Save", () =>
            {
                ISaveStorage storage = Options.SaveStorage ?? (Options.InMemorySave
                    ? new InMemorySaveStorage()
                    : new FileSaveStorage(System.IO.Path.Combine(Application.persistentDataPath, "hyperframe")));
                save = new SaveService(storage, def.saveSchemaVersion, def.gameplay.GetSaveMigrations(), bus);
                save.Load();
                Options.PrepareSave?.Invoke(save);
                ServiceLocator.Register<ISaveService>(save);
            });

            boot.Add("Analytics", () =>
            {
                analytics = new AnalyticsDispatcher();
                var memory = new InMemoryAnalyticsBackend();
                analytics.AddBackend(memory);
                if (Debug.isDebugBuild) analytics.AddBackend(new LogAnalyticsBackend());
                ServiceLocator.Register<IAnalyticsService>(analytics);
                ServiceLocator.Register(memory);
                bus.Subscribe<CurrencyChangedEvent>(e => analytics.CurrencyChange(e));
            });

            boot.Add("Player data", () =>
            {
                ServiceLocator.Register<ISettingsService>(new SettingsService(save, Application.version, def.privacyPolicyUrl));
                ServiceLocator.Register<IWallet>(new Wallet(save, bus, new Dictionary<string, long> { [Currencies.Coins] = def.startingCoins }));
                ServiceLocator.Register<IProgressionService>(new ProgressionService(save));
            });

            boot.Add("Monetization (mocks)", async ct =>
            {
                var ads = new MockAdsService(analytics) { NextResult = Options.MockAdResult };
                var iap = new MockIAPService(analytics);
                ads.AdsRemoved = iap.IsOwned(Products.RemoveAds);
                ServiceLocator.Register<IAdsService>(ads);
                ServiceLocator.Register(ads);
                ServiceLocator.Register<IIAPService>(iap);
                ServiceLocator.Register<IConsentService>(new MockConsentService());
                var remote = new LocalRemoteConfig();
                ServiceLocator.Register<IRemoteConfig>(remote);
                await remote.FetchAsync();
            }); // Phase 2: real SDK adapters boot as non-critical steps that fall back to these mocks

            boot.Add("Input", () =>
            {
                var gestureSettings = def.gestures != null ? def.gestures.settings.Clone() : new GestureSettings();
                input = new InputService(gestureSettings, Screen.dpi, () => Time.unscaledTime);
                InputDriver.Create(input, transform);
                ServiceLocator.Register<IInputService>(input);
                ServiceLocator.Register(input);
            });

            boot.Add("Audio", () =>
            {
                IAudioService audio;
                if (Options.MockAudio) audio = new MockAudioService();
                else
                {
                    var table = def.sounds != null ? Instantiate(def.sounds) : ScriptableObject.CreateInstance<SoundTable>();
                    PlaceholderSounds.FillMissing(table);
                    audio = new AudioService(table, tweens, transform);
                }
                ServiceLocator.Register(audio);
                var settings = ServiceLocator.Get<ISettingsService>();
                settings.MusicOn.Bind(on => audio.MusicMuted = !on);
                settings.SfxOn.Bind(on => audio.SfxMuted = !on);
                settings.MusicVolume.Bind(v => audio.MusicVolume = v);
                settings.SfxVolume.Bind(v => audio.SfxVolume = v);
            });

            boot.Add("UI", () =>
            {
                _uiRoot = UIRoot.Create(transform);
                var ui = new UIService(_uiRoot, tweens, input.Lock, bus, new DefaultViewProvider(def.views), def.theme);
                ServiceLocator.Register<IUIService>(ui);
                input.BackPressed += () => ui.HandleBack();
                bus.Subscribe<ButtonClickedEvent>(_ => ServiceLocator.Get<IAudioService>().PlaySfx(SoundIds.UiClick));
                if (_camera != null) _camera.backgroundColor = ui.Theme.background;
                if (Options.InteractiveMockAds && ServiceLocator.TryGet<MockAdsService>(out var mockAds))
                    mockAds.Presenter = (format, placement) => MockAdPopup.Present(ui, format, placement);
            });

            boot.Add("Feedback", () =>
            {
                IHapticsService haptics = Application.isEditor ? (IHapticsService)new MockHapticsService() : new DeviceHapticsService();
                ServiceLocator.Get<ISettingsService>().HapticsOn.Bind(on => haptics.Enabled = on);
                ServiceLocator.Register(haptics);
                ServiceLocator.Register<IFeedbackService>(new FeedbackService(tweens, clock, ServiceLocator.Get<IPoolService>(),
                    ServiceLocator.Get<IUIService>(), haptics, () => _camera));
            });

            boot.Add("Levels + flow", () =>
            {
                var levels = def.gameplay.CreateLevelProvider(def);
                if (levels.Count == 0) throw new InvalidOperationException("The level provider has no levels.");
                ServiceLocator.Register(levels);
                Flow = new GameFlow(def, levels, null);
                ServiceLocator.Register(Flow);
            });

            boot.Add("Game module", () => def.gameplay.OnBoot());
            return boot;
        }

        void EnsureCamera()
        {
            _camera = Camera.main;
            if (_camera != null) return;
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 0f, -10f);
            _camera = go.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 8f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            _lifetime.Cancel();
            if (ServiceLocator.TryGet<ISaveService>(out var save)) save.SaveIfDirty();
            Flow?.Dispose();
            if (ServiceLocator.TryGet<TweenEngine>(out var tweens)) tweens.KillAll();
            ServiceLocator.Reset();
            DebugCommands.Clear();
            Time.timeScale = 1f;
            Instance = null;
        }
    }
}
