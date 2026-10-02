using HyperFrame.Services;

namespace HyperFrame.App.Tests
{
    /// <summary>Minimal game used to test the framework flow without any real gameplay.</summary>
    public sealed class FakeGameplayModule : GameplayModule
    {
        public int Created;

        public override IGameplay CreateGameplay()
        {
            Created++;
            return new FakeGameplay();
        }

        public override ILevelProvider CreateLevelProvider(GameDefinition definition) =>
            new FuncLevelProvider(5, i => new FakeLevel { Id = $"fake_{i}" });

        sealed class FakeLevel : ILevelData { public string Id { get; set; } }

        sealed class FakeGameplay : GameplayBase
        {
            protected override void OnBegin() => Context.Hud.SetStatus("fake");
        }
    }
}
