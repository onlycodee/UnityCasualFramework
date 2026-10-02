using System.Collections;
using HyperFrame.App;
using HyperFrame.App.Testing;
using HyperFrame.Core;
using HyperFrame.Input;
using HyperFrame.Services;
using HyperFrame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// The game runs Boot → Home → Play → Win → Home, driven like a player (UI buttons pressed by name,
    /// world taps injected through the input service).
    /// </summary>
    public class FullFlowTests
    {
        GameDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = Resources.Load<GameDefinition>(HyperFrameApp.DefinitionResourcePath);
            Assert.IsNotNull(_definition, "Assets/_Game/Resources/GameDefinition.asset is missing");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (HyperFrameApp.Instance != null) Object.Destroy(HyperFrameApp.Instance.gameObject);
            yield return null;
        }

        IEnumerator Boot(AppOptions options = null) => Boot(_definition, options);

        internal static IEnumerator Boot(GameDefinition definition, AppOptions options = null)
        {
            HyperFrameApp.Launch(definition, options ?? AppOptions.ForTests());
            yield return AppDriver.WaitForReady();
            Assert.IsFalse(AppDriver.App.BootFailed, AppDriver.App.BootReport?.ToString());
            yield return AppDriver.WaitForState(GameFlowState.Home);
            yield return AppDriver.WaitForView<HomeScreen>();
        }

        /// <summary>
        /// Plays the level like a careful player: taps the queue column (or tray slot) of the next planned
        /// shooter whenever the belt is empty, through real input injection.
        /// </summary>
        internal static IEnumerator PlayLevel()
        {
            var gameplay = (IPixelLoopGameplay)AppDriver.Flow.ActiveGameplay;
            yield return new WaitForSeconds(0.8f); // pixels and shooters fly in
            ServiceLocator.Get<IGameClock>().TimeScale = 3f; // keep the test short
            var sim = gameplay.Sim;
            float end = Time.realtimeSinceStartup + 120f;
            while (!sim.IsOver && Time.realtimeSinceStartup < end)
            {
                if (sim.BeltLoad == 0)
                {
                    var next = BeltBot.NextPlanned(sim);
                    if (next != null)
                    {
                        int launches = sim.Launches;
                        AppDriver.TapWorld(gameplay.TapPointFor(next));
                        yield return AppDriver.WaitUntil(() => sim.Launches > launches, 5f, $"shooter {next.Id} to launch");
                    }
                }
                yield return null;
            }
            Assert.IsTrue(sim.IsWon, $"level not cleared: {sim.Remaining} pixels left, lost={sim.IsLost}. {AppDriver.Describe()}");
        }

        [UnityTest]
        public IEnumerator Boot_Home_Play_Win_Home()
        {
            yield return Boot();
            var progression = ServiceLocator.Get<IProgressionService>();
            var wallet = ServiceLocator.Get<IWallet>();
            var analytics = ServiceLocator.Get<InMemoryAnalyticsBackend>();
            Assert.AreEqual(0, progression.CurrentLevelIndex);
            long coinsBefore = wallet.Get(Currencies.Coins);

            yield return AppDriver.Click<HomeScreen>("btn_Play");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            yield return PlayLevel();

            yield return AppDriver.WaitForState(GameFlowState.Result);
            yield return AppDriver.Click<WinPopup>("btn_Continue");
            yield return AppDriver.WaitForState(GameFlowState.Home, 15f);

            Assert.AreEqual(1, progression.CurrentLevelIndex, "level 1 should be completed");
            Assert.AreEqual(3, progression.GetStars(0), "no shooter came back = 3 stars");
            Assert.AreEqual(coinsBefore + _definition.winRewardCoins, wallet.Get(Currencies.Coins));
            Assert.AreEqual(1, analytics.Count(AnalyticsSchema.LevelStart));
            Assert.AreEqual(1, analytics.Count(AnalyticsSchema.LevelComplete));
            Assert.IsFalse(ServiceLocator.Get<IInputService>().Lock.IsLocked, "input lock leaked: " + AppDriver.Describe());
            Assert.IsFalse(ServiceLocator.Get<IGameClock>().IsPaused);
        }

        [UnityTest]
        public IEnumerator Lose_Then_Retry_Then_Home()
        {
            yield return Boot();
            yield return AppDriver.Click<HomeScreen>("btn_Play");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            yield return new WaitForSeconds(0.8f);
            var gameplay = (IPixelLoopGameplay)AppDriver.Flow.ActiveGameplay;
            AppDriver.TapWorld(gameplay.TapPointFor(gameplay.Sim.Front(0)));
            yield return null;
            Assert.AreEqual(1, gameplay.Moves, "a tap on a queue column sends its front shooter");
            AppDriver.Flow.ForceFinish(false);

            yield return AppDriver.WaitForState(GameFlowState.Result);
            yield return AppDriver.Click<LosePopup>("btn_Retry");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            Assert.AreEqual(0, AppDriver.Flow.ActiveGameplay.Moves, "retry starts a fresh attempt");

            AppDriver.Flow.ForceFinish(false);
            yield return AppDriver.Click<LosePopup>("btn_Home");
            yield return AppDriver.WaitForState(GameFlowState.Home);
            Assert.AreEqual(0, ServiceLocator.Get<IProgressionService>().CurrentLevelIndex);
            Assert.AreEqual(2, ServiceLocator.Get<InMemoryAnalyticsBackend>().Count(AnalyticsSchema.LevelFail));
        }

        [UnityTest]
        public IEnumerator Win_WithRewardedMultiplier_GrantsDoubleCoins()
        {
            yield return Boot();
            var wallet = ServiceLocator.Get<IWallet>();
            long before = wallet.Get(Currencies.Coins);

            yield return AppDriver.Click<HomeScreen>("btn_Play");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            AppDriver.Flow.ForceFinish(true);
            yield return AppDriver.Click<WinPopup>("btn_Multiply");
            yield return AppDriver.WaitForState(GameFlowState.Home, 15f);

            Assert.AreEqual(before + _definition.winRewardCoins * _definition.rewardedMultiplier, wallet.Get(Currencies.Coins));
            Assert.AreEqual(1, ServiceLocator.Get<InMemoryAnalyticsBackend>().Count(AnalyticsSchema.AdReward));
        }

        [UnityTest]
        public IEnumerator Win_WithSkippedAd_GrantsBaseCoins()
        {
            var options = AppOptions.ForTests();
            options.MockAdResult = AdResult.Skipped;
            yield return Boot(options);
            var wallet = ServiceLocator.Get<IWallet>();
            long before = wallet.Get(Currencies.Coins);

            yield return AppDriver.Click<HomeScreen>("btn_Play");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            AppDriver.Flow.ForceFinish(true);
            yield return AppDriver.Click<WinPopup>("btn_Multiply");
            yield return AppDriver.WaitForState(GameFlowState.Home, 15f);

            Assert.AreEqual(before + _definition.winRewardCoins, wallet.Get(Currencies.Coins));
        }

        [UnityTest]
        public IEnumerator Pause_LocksInput_AndResumeReleasesIt()
        {
            yield return Boot();
            yield return AppDriver.Click<HomeScreen>("btn_Play");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            yield return AppDriver.Click<GameplayScreen>("btn_Pause");
            yield return AppDriver.WaitForView<PausePopup>();

            Assert.IsTrue(ServiceLocator.Get<IGameClock>().IsPaused);
            Assert.IsTrue(ServiceLocator.Get<IInputService>().Lock.IsLocked);

            // A tap while paused must not send a shooter.
            var gameplay = (IPixelLoopGameplay)AppDriver.Flow.ActiveGameplay;
            AppDriver.TapWorld(gameplay.TapPointFor(gameplay.Sim.Front(0)));
            Assert.AreEqual(0, gameplay.Moves);
            Assert.AreEqual(0, gameplay.Sim.Launches);

            yield return AppDriver.Click<PausePopup>("btn_Resume");
            yield return AppDriver.WaitUntil(() => !ServiceLocator.Get<IInputService>().Lock.IsLocked, 5f, "input unlock");
            Assert.IsFalse(ServiceLocator.Get<IGameClock>().IsPaused);
        }

        [UnityTest]
        public IEnumerator BackButton_InGameplay_OpensPause_ThenResumes()
        {
            yield return Boot();
            yield return AppDriver.Click<HomeScreen>("btn_Play");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            yield return AppDriver.WaitForView<GameplayScreen>();

            ServiceLocator.Get<IInputService>().InjectBack();
            yield return AppDriver.WaitForView<PausePopup>();
            ServiceLocator.Get<IInputService>().InjectBack();
            yield return AppDriver.WaitUntil(() => !ServiceLocator.Get<IGameClock>().IsPaused, 5f, "resume after back");
            Assert.AreEqual(GameFlowState.Gameplay, AppDriver.Flow.State);
        }

        [UnityTest]
        public IEnumerator Progress_SurvivesReboot()
        {
            var storage = new InMemorySaveStorage(); // shared by both boots, like the device disk
            var options = AppOptions.ForTests();
            options.SaveStorage = storage;

            yield return Boot(options);
            yield return AppDriver.Click<HomeScreen>("btn_Play");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            AppDriver.Flow.ForceFinish(true);
            yield return AppDriver.Click<WinPopup>("btn_Continue");
            yield return AppDriver.WaitForState(GameFlowState.Home, 15f);
            long coins = ServiceLocator.Get<IWallet>().Get(Currencies.Coins);

            Object.Destroy(HyperFrameApp.Instance.gameObject); // OnDestroy saves
            yield return null;
            Assert.IsTrue(storage.Files.ContainsKey("save"), "app did not save on shutdown");

            yield return Boot(options);
            Assert.AreEqual(1, ServiceLocator.Get<IProgressionService>().CurrentLevelIndex);
            Assert.AreEqual(coins, ServiceLocator.Get<IWallet>().Get(Currencies.Coins));
            Assert.AreEqual("Level 2", ServiceLocator.Get<IUIService>().GetView<HomeScreen>().GetText("txt_Level"));
        }
    }
}
