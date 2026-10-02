using HyperFrame.Services;
using UnityEngine;

namespace HyperFrame.App.Tests
{
    /// <summary>Minimal game used to test the framework flow without any real gameplay.</summary>
    public sealed class FakeGameplayModule : GameplayModule
    {
        public int Created;
        /// <summary>When set, each level turns the shared camera into a perspective rig (like a 3D game).</summary>
        public bool UsePerspectiveCamera;

        public override IGameplay CreateGameplay()
        {
            Created++;
            return new FakeGameplay(UsePerspectiveCamera);
        }

        public override ILevelProvider CreateLevelProvider(GameDefinition definition) =>
            new FuncLevelProvider(5, i => new FakeLevel { Id = $"fake_{i}" });

        sealed class FakeLevel : ILevelData { public string Id { get; set; } }

        sealed class FakeGameplay : GameplayBase
        {
            readonly bool _perspective;
            public FakeGameplay(bool perspective) => _perspective = perspective;

            protected override void OnBegin()
            {
                Context.Hud.SetStatus("fake");
                if (!_perspective || Context.Camera == null) return;
                Context.Camera.orthographic = false;
                Context.Camera.fieldOfView = 35f;
                Context.Camera.transform.SetPositionAndRotation(new Vector3(0f, 12f, -9f), Quaternion.Euler(55f, 0f, 0f));
            }
        }
    }
}
