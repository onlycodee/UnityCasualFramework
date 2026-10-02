using System.Collections;
using HyperFrame.App;
using HyperFrame.App.Testing;
using HyperFrame.Core;
using HyperFrame.Services;
using HyperFrame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Both presentations (2D sprites and 3D voxels) play level 1 to a win through real taps, whichever one
    /// GameDefinition currently selects. The 3D one must hand the camera and lighting back at Home.
    /// </summary>
    public class PresentationTests
    {
        GameDefinition _definition;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (HyperFrameApp.Instance != null) Object.Destroy(HyperFrameApp.Instance.gameObject);
            yield return null;
            if (_definition != null) Object.Destroy(_definition);
        }

        GameDefinition DefinitionWith(string module)
        {
            var source = Resources.Load<GameDefinition>(HyperFrameApp.DefinitionResourcePath);
            _definition = Object.Instantiate(source);
            _definition.gameplay = Resources.Load<GameplayModule>(module);
            Assert.IsNotNull(_definition.gameplay, $"Resources/{module}.asset is missing");
            return _definition;
        }

        [UnityTest]
        public IEnumerator Flat2D_PlaysLevel1ToAWin() => PlayAndWin("PixelLoopModule", typeof(PixelLoopGameplay));

        [UnityTest]
        public IEnumerator Voxel3D_PlaysLevel1ToAWin() => PlayAndWin("PixelLoop3DModule", typeof(PixelLoop3DGameplay));

        IEnumerator PlayAndWin(string module, System.Type gameplayType)
        {
            yield return FullFlowTests.Boot(DefinitionWith(module));
            var camera = Camera.main;
            bool orthographicAtHome = camera.orthographic;
            bool fogAtHome = RenderSettings.fog;

            yield return AppDriver.Click<HomeScreen>("btn_Play");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            Assert.IsInstanceOf(gameplayType, AppDriver.Flow.ActiveGameplay);
            yield return FullFlowTests.PlayLevel();

            yield return AppDriver.WaitForState(GameFlowState.Result);
            yield return AppDriver.Click<WinPopup>("btn_Continue");
            yield return AppDriver.WaitForState(GameFlowState.Home, 15f);

            Assert.AreEqual(3, ServiceLocator.Get<IProgressionService>().GetStars(0));
            Assert.AreEqual(orthographicAtHome, camera.orthographic, "camera projection was not restored");
            Assert.AreEqual(fogAtHome, RenderSettings.fog, "fog was not restored");
            Assert.IsFalse(ServiceLocator.Get<HyperFrame.Input.IInputService>().Lock.IsLocked, AppDriver.Describe());
        }

        [UnityTest]
        public IEnumerator Voxel3D_TapOnAColumn_SendsItsFrontShooter()
        {
            yield return FullFlowTests.Boot(DefinitionWith("PixelLoop3DModule"));
            yield return AppDriver.Click<HomeScreen>("btn_Play");
            yield return AppDriver.WaitForState(GameFlowState.Gameplay);
            yield return new WaitForSeconds(1.3f); // camera fly-in
            var gameplay = (PixelLoop3DGameplay)AppDriver.Flow.ActiveGameplay;
            var columns = gameplay.Sim.Columns.Count;
            var front = gameplay.Sim.Front(columns - 1);
            AppDriver.TapWorld(gameplay.TapPointFor(front));
            yield return null;
            Assert.AreEqual(1, gameplay.Sim.Launches, "a tap on the last column sends its front shooter");
            Assert.AreNotEqual(ShooterState.Queued, front.State);
        }
    }
}
