using System;
using System.Threading.Tasks;
using HyperFrame.Audio;
using HyperFrame.Core;
using HyperFrame.Feedback;
using HyperFrame.Input;
using HyperFrame.Services;
using HyperFrame.UI;
using UnityEngine;

namespace HyperFrame.App
{
    /// <summary>The standard flow (AR-4). Games only provide the Gameplay part.</summary>
    public enum GameFlowState { Boot, Consent, Loading, Home, Gameplay, Result, Reward }

    public struct GameFlowChangedEvent { public GameFlowState From; public GameFlowState To; }
    public struct LevelStartedEvent { public int LevelIndex; public string LevelId; }
    public struct LevelFinishedEvent { public int LevelIndex; public LevelOutcome Outcome; }

    /// <summary>
    /// Drives Boot → Consent → Loading → Home → Gameplay → Result(Win/Lose) → Reward → Home on top of
    /// <see cref="StateMachine{TKey}"/>. Registered as a service so views can call it (Play, Pause, Retry).
    /// </summary>
    public sealed class GameFlow : IDisposable
    {
        readonly StateMachine<GameFlowState> _machine = new StateMachine<GameFlowState>("GameFlow");
        readonly GameDefinition _definition;
        readonly Transform _worldParent;
        int _epoch; // increments on every state change; async work checks it before acting

        IGameplay _gameplay;
        GameObject _world;
        InputRouter2D _router;
        Camera _camera;
        CameraSnapshot _cameraBefore;
        float _levelStartTime;
        bool _paused;

        public GameFlowState State => _machine.Current;
        public bool IsPaused => _paused;
        public int CurrentLevelIndex { get; private set; }
        public ILevelData CurrentLevel { get; private set; }
        public IGameplay ActiveGameplay => _gameplay;
        public LevelOutcome LastOutcome { get; private set; }
        public ILevelProvider Levels { get; }
        public event Action<GameFlowState, GameFlowState> Changed;

        static T S<T>() where T : class => ServiceLocator.Get<T>();

        public GameFlow(GameDefinition definition, ILevelProvider levels, Transform worldParent)
        {
            _definition = definition;
            Levels = levels;
            _worldParent = worldParent;

            _machine
                .Add(GameFlowState.Boot, new DelegateState(() => Go(GameFlowState.Consent)))
                .Add(GameFlowState.Consent, new DelegateState(EnterConsent))
                .Add(GameFlowState.Loading, new DelegateState(EnterLoading))
                .Add(GameFlowState.Home, new DelegateState(EnterHome))
                .Add(GameFlowState.Gameplay, new DelegateState(EnterGameplay))
                .Add(GameFlowState.Result, new DelegateState(EnterResult))
                .Add(GameFlowState.Reward, new DelegateState(EnterReward));

            _machine
                .Allow(GameFlowState.Boot, GameFlowState.Consent)
                .Allow(GameFlowState.Consent, GameFlowState.Loading)
                .Allow(GameFlowState.Loading, GameFlowState.Home)
                .Allow(GameFlowState.Home, GameFlowState.Gameplay)
                .Allow(GameFlowState.Gameplay, GameFlowState.Result, GameFlowState.Home, GameFlowState.Gameplay)
                .Allow(GameFlowState.Result, GameFlowState.Reward, GameFlowState.Gameplay, GameFlowState.Home)
                .Allow(GameFlowState.Reward, GameFlowState.Home, GameFlowState.Gameplay);

            _machine.Changed += (from, to) =>
            {
                Changed?.Invoke(from, to);
                if (ServiceLocator.TryGet<IEventBus>(out var bus)) bus.Publish(new GameFlowChangedEvent { From = from, To = to });
            };
        }

        public void Start() => _machine.Start(GameFlowState.Boot);

        void Go(GameFlowState next)
        {
            _epoch++;
            _machine.ChangeState(next);
        }

        /// <summary>Runs async state work; the continuation is ignored if the state changed meanwhile.</summary>
        void RunInState(Func<int, Task> work, string tag)
        {
            int epoch = _epoch;
            work(epoch).Forget("GameFlow." + tag);
        }

        bool Stale(int epoch) => epoch != _epoch;

        // ── States ──────────────────────────────────────────────────────────────────────

        void EnterConsent() => RunInState(async epoch =>
        {
            var status = ServiceLocator.TryGet<IConsentService>(out var consent) ? await consent.RequestAsync() : ConsentStatus.NotRequired;
            HFLog.Info("GameFlow", $"Consent: {status}");
            if (!Stale(epoch)) Go(GameFlowState.Loading);
        }, "Consent");

        void EnterLoading() => RunInState(async epoch =>
        {
            var ui = S<IUIService>();
            ui.SetLoading(true);
            await Task.Yield(); // let the loading overlay render; heavy preloads go here (CORE-08 later)
            if (Stale(epoch)) return;
            ui.SetLoading(false);
            Go(GameFlowState.Home);
        }, "Loading");

        void EnterHome()
        {
            EndSession();
            SetPaused(false);
            S<IUIService>().CloseAllPopups();
            S<IInputService>().Lock.ReleaseAll(); // logs anything that leaked a lock (soft-lock guard)
            S<IUIService>().ShowScreen<HomeScreen>();
            S<IAudioService>().PlayMusic(SoundIds.MusicHome);
        }

        void EnterGameplay()
        {
            EndSession();
            SetPaused(false);
            var progression = S<IProgressionService>();
            CurrentLevel = Levels.Get(CurrentLevelIndex);
            progression.RecordAttempt(CurrentLevelIndex);

            var ui = S<IUIService>();
            ui.CloseAllPopups();
            var hud = ui.ShowScreen<GameplayScreen>(CurrentLevelIndex);
            S<IAudioService>().PlayMusic(SoundIds.MusicGameplay);

            _world = new GameObject($"Level_{CurrentLevelIndex + 1}");
            if (_worldParent != null) _world.transform.SetParent(_worldParent, false);
            var input = S<IInputService>();
            _router = new InputRouter2D(input);
            _camera = Camera.main;
            _cameraBefore = CameraSnapshot.Capture(_camera);

            var context = new GameplayContext
            {
                LevelIndex = CurrentLevelIndex,
                Level = CurrentLevel,
                WorldRoot = _world.transform,
                Camera = _camera,
                Input = input,
                Router = _router,
                Feedback = S<IFeedbackService>(),
                Audio = S<IAudioService>(),
                Tweens = S<TweenEngine>(),
                Clock = S<IGameClock>(),
                Hud = hud
            };

            _gameplay = _definition.gameplay.CreateGameplay();
            var thisGameplay = _gameplay;
            _gameplay.Finished += outcome => OnGameplayFinished(thisGameplay, outcome);
            _levelStartTime = S<IGameClock>().GameTime;

            S<IAnalyticsService>().LevelStart(CurrentLevel.Id, CurrentLevelIndex, progression.AttemptsOnCurrent,
                _definition.gameplay.DescribeDifficulty(CurrentLevelIndex, CurrentLevel));
            S<IEventBus>().Publish(new LevelStartedEvent { LevelIndex = CurrentLevelIndex, LevelId = CurrentLevel.Id });

            try { _gameplay.Begin(context); }
            catch (Exception e)
            {
                HFLog.Exception(e, "Gameplay.Begin");
                ui.Toast("Level failed to load");
                Go(GameFlowState.Home);
            }
        }

        void OnGameplayFinished(IGameplay source, LevelOutcome outcome)
        {
            if (source != _gameplay || State != GameFlowState.Gameplay) return;
            outcome.DurationSeconds = S<IGameClock>().GameTime - _levelStartTime;
            LastOutcome = outcome;
            S<IEventBus>().Publish(new LevelFinishedEvent { LevelIndex = CurrentLevelIndex, Outcome = outcome });
            Go(GameFlowState.Result);
        }

        void EnterResult() => RunInState(async epoch =>
        {
            var outcome = LastOutcome;
            var analytics = S<IAnalyticsService>();
            var ui = S<IUIService>();
            var audio = S<IAudioService>();
            if (outcome.Won)
            {
                S<IProgressionService>().CompleteLevel(CurrentLevelIndex, outcome.Stars);
                analytics.LevelComplete(CurrentLevel.Id, outcome.DurationSeconds, outcome.Moves, outcome.Stars, outcome.BoostersUsed);
                audio.PlaySfx(SoundIds.Win);
                S<IFeedbackService>().Confetti();
                S<IFeedbackService>().Haptic(HapticType.Success);

                int reward = _definition.gameplay.CalculateReward(outcome, _definition);
                var result = await ui.ShowPopup<WinPopup>(new WinPopup.Args
                {
                    LevelIndex = CurrentLevelIndex,
                    Stars = outcome.Stars,
                    Reward = reward,
                    Multiplier = _definition.rewardedMultiplier
                });
                if (Stale(epoch)) return;
                _pendingReward = reward;
                _pendingRewardWithAd = result.Is(WinPopup.ActionMultiply);
                Go(GameFlowState.Reward);
            }
            else
            {
                analytics.LevelFail(CurrentLevel.Id, outcome.DurationSeconds, outcome.Moves, outcome.Reason ?? "unknown");
                audio.PlaySfx(SoundIds.Lose);
                S<IFeedbackService>().Haptic(HapticType.Failure);
                var result = await ui.ShowPopup<LosePopup>(outcome);
                if (Stale(epoch)) return;
                Go(result.Is(LosePopup.ActionRetry) ? GameFlowState.Gameplay : GameFlowState.Home);
            }
        }, "Result");

        int _pendingReward;
        bool _pendingRewardWithAd;

        void EnterReward() => RunInState(async epoch =>
        {
            int amount = _pendingReward;
            if (_pendingRewardWithAd)
            {
                var adResult = await S<IAdsService>().ShowRewarded("win_multiplier");
                if (Stale(epoch)) return;
                if (adResult == AdResult.Completed) amount *= Math.Max(1, _definition.rewardedMultiplier);
            }

            var wallet = S<IWallet>();
            var ui = S<IUIService>();
            var hud = ui.CurrentScreen as GameplayScreen;
            if (amount > 0 && hud != null && hud.IsVisible)
            {
                var done = new TaskCompletionSource<bool>();
                var from = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                S<IFeedbackService>().CoinFly(from, hud.CoinTarget, Mathf.Clamp(amount / 5, 3, 12),
                    () => S<IAudioService>().PlaySfx(SoundIds.Coin),
                    () => done.TrySetResult(true));
                await done.Task;
                if (Stale(epoch)) return;
            }
            if (amount > 0) wallet.Add(Currencies.Coins, amount, "level_win");

            if (_definition.continueToNextLevel)
            {
                CurrentLevelIndex = S<IProgressionService>().CurrentLevelIndex;
                Go(GameFlowState.Gameplay);
            }
            else
            {
                Go(GameFlowState.Home);
            }
        }, "Reward");

        // ── Commands used by views, debug console and tests ─────────────────────────────

        /// <summary>Starts a level (Home → Gameplay). Defaults to the progression's current level.</summary>
        public void Play(int? levelIndex = null)
        {
            CurrentLevelIndex = levelIndex ?? S<IProgressionService>().CurrentLevelIndex;
            if (levelIndex.HasValue) S<IProgressionService>().SetCurrentLevel(levelIndex.Value);
            Go(GameFlowState.Gameplay);
        }

        public void Restart()
        {
            if (State == GameFlowState.Gameplay || State == GameFlowState.Result) Go(GameFlowState.Gameplay);
        }

        public void GoHome()
        {
            if (State != GameFlowState.Home) Go(GameFlowState.Home);
        }

        /// <summary>Opens the pause popup and pauses the game clock until it closes.</summary>
        public void Pause() => PauseAsync().Forget("GameFlow.Pause");

        async Task PauseAsync()
        {
            if (State != GameFlowState.Gameplay || _paused) return;
            int epoch = _epoch;
            SetPaused(true);
            var result = await S<IUIService>().ShowPopup<PausePopup>();
            if (Stale(epoch)) return;
            SetPaused(false);
            if (result.Is(PausePopup.ActionHome)) GoHome();
            else if (result.Is(PausePopup.ActionRestart)) Restart();
        }

        void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            var clock = S<IGameClock>();
            if (paused) clock.Pause("pause_popup");
            else clock.Resume("pause_popup");
        }

        /// <summary>Ends the current level as a win or loss (debug console, tests).</summary>
        public void ForceFinish(bool win)
        {
            if (State == GameFlowState.Gameplay) _gameplay?.ForceFinish(win);
        }

        void EndSession()
        {
            _router?.Dispose();
            _router = null;
            if (_gameplay != null)
            {
                try { _gameplay.Dispose(); }
                catch (Exception e) { HFLog.Exception(e, "Gameplay.Dispose"); }
                _gameplay = null;
            }
            if (_world != null)
            {
                S<TweenEngine>().KillTarget(_world);
                UnityEngine.Object.Destroy(_world);
                _world = null;
            }
            // A gameplay may have turned the camera into a 3D rig (or shaken it); put it back.
            if (_camera != null)
            {
                S<TweenEngine>().KillTarget(_camera.transform);
                _cameraBefore.Restore(_camera);
            }
            _camera = null;
            _cameraBefore = default;
        }

        public void Dispose()
        {
            _epoch++;
            if (ServiceLocator.Has<IUIService>()) EndSession();
        }
    }
}
