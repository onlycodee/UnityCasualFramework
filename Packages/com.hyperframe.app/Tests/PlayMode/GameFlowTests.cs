using System.Collections;
using System.Collections.Generic;
using HyperFrame.App.Testing;
using HyperFrame.Core;
using HyperFrame.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperFrame.App.Tests
{
    public class GameFlowTests
    {
        GameDefinition _definition;
        FakeGameplayModule _module;
        readonly List<GameFlowState> _visited = new List<GameFlowState>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _module = ScriptableObject.CreateInstance<FakeGameplayModule>();
            _definition = ScriptableObject.CreateInstance<GameDefinition>();
            _definition.gameplay = _module;
            _definition.winRewardCoins = 10;
            _visited.Clear();
            HyperFrameApp.Launch(_definition, AppOptions.ForTests());
            yield return AppDriver.WaitForReady();
            Assert.IsFalse(AppDriver.App.BootFailed, AppDriver.App.BootReport?.ToString());
            AppDriver.Flow.Changed += (_, to) => _visited.Add(to);
            yield return AppDriver.WaitForState(GameFlowState.Home);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (HyperFrameApp.Instance != null) Object.Destroy(HyperFrameApp.Instance.gameObject);
            yield return null;
            Object.Destroy(_definition);
            Object.Destroy(_module);
        }

        [UnityTest]
        public IEnumerator BootReport_ListsEveryStepSucceeded()
        {
            var report = AppDriver.App.BootReport;
            Assert.IsTrue(report.Success);
            foreach (var step in report.Steps) Assert.AreEqual(BootStepStatus.Succeeded, step.Status, step.ToString());
            yield break;
        }

        [UnityTest]
        public IEnumerator Win_FollowsStandardFlow()
        {
            yield return AppDriver.Click<HomeScreen>("btn_Play");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            Assert.AreEqual("fake", AppDriver.UI.GetView<GameplayScreen>().GetText("txt_Status"));
            AppDriver.Flow.ForceFinish(true);
            yield return AppDriver.Click<WinPopup>("btn_Continue");
            yield return AppDriver.WaitForState(GameFlowState.Home, 15f);

            CollectionAssert.AreEqual(
                new[] { GameFlowState.Gameplay, GameFlowState.Result, GameFlowState.Reward, GameFlowState.Home }, _visited);
            Assert.AreEqual(10, ServiceLocator.Get<IWallet>().Get(Currencies.Coins));
            Assert.AreEqual(1, _module.Created);
        }

        [UnityTest]
        public IEnumerator Pause_Home_ReturnsHome_AndUnpauses()
        {
            yield return AppDriver.Click<HomeScreen>("btn_Play");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            yield return AppDriver.Click<GameplayScreen>("btn_Pause");
            yield return AppDriver.Click<PausePopup>("btn_Home");
            yield return AppDriver.WaitForState(GameFlowState.Home);
            Assert.IsFalse(ServiceLocator.Get<IGameClock>().IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator CameraChangedByGameplay_IsRestoredAtHome()
        {
            var cam = Camera.main;
            var before = CameraSnapshot.Capture(cam);
            _module.UsePerspectiveCamera = true;
            yield return AppDriver.Click<HomeScreen>("btn_Play");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            Assert.IsFalse(cam.orthographic, "the fake gameplay switches to perspective");

            yield return AppDriver.Click<GameplayScreen>("btn_Pause");
            yield return AppDriver.Click<PausePopup>("btn_Home");
            yield return AppDriver.WaitForState(GameFlowState.Home);
            Assert.IsTrue(cam.orthographic);
            Assert.AreEqual(before.OrthographicSize, cam.orthographicSize);
            Assert.AreEqual(before.Position, cam.transform.localPosition);
            Assert.AreEqual(before.Rotation, cam.transform.localRotation);
        }

        [UnityTest]
        public IEnumerator Settings_TogglesPersistToSave()
        {
            yield return AppDriver.Click<HomeScreen>("btn_Settings");
            yield return AppDriver.Click<SettingsPopup>("btn_Music");
            Assert.IsFalse(ServiceLocator.Get<ISettingsService>().MusicOn.Value);
            Assert.IsTrue(ServiceLocator.Get<ISaveService>().Get<SettingsData>(SettingsService.Section).musicOn == false);
            Assert.AreEqual("Music: OFF", AppDriver.UI.GetView<SettingsPopup>().GetText("btn_Music"));
            yield return AppDriver.Click<SettingsPopup>("btn_Close");
        }

        [UnityTest]
        public IEnumerator LevelSelect_PlaysUnlockedLevel()
        {
            ServiceLocator.Get<IProgressionService>().CompleteLevel(0, 3);
            ServiceLocator.Get<IProgressionService>().CompleteLevel(1, 3);
            yield return AppDriver.Click<HomeScreen>("btn_Levels");
            yield return AppDriver.WaitForView<LevelSelectScreen>();
            Assert.IsFalse(AppDriver.UI.GetView<LevelSelectScreen>().Click("btn_Level_4"), "level 4 is locked");
            yield return AppDriver.Click<LevelSelectScreen>("btn_Level_2");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            Assert.AreEqual(1, AppDriver.Flow.CurrentLevelIndex);
        }
    }
}
