using HyperFrame.App;
using HyperFrame.Services;
using UnityEngine;

namespace Game
{
    /// <summary>The game's plug-in (referenced from GameDefinition.gameplay). 50 generated levels, then endless.</summary>
    [CreateAssetMenu(menuName = "Game/Tap Targets Module", fileName = "TapTargetsModule")]
    public sealed class TapTargetsModule : GameplayModule
    {
        public int levelCount = 50;
        public Color[] palette =
        {
            new Color(0.95f, 0.36f, 0.36f), new Color(0.36f, 0.75f, 0.95f), new Color(0.98f, 0.80f, 0.25f),
            new Color(0.45f, 0.85f, 0.45f), new Color(0.75f, 0.50f, 0.95f)
        };

        public override IGameplay CreateGameplay() => new TapTargetsGameplay(palette);

        public override ILevelProvider CreateLevelProvider(GameDefinition definition) =>
            new FuncLevelProvider(levelCount, index => TapTargetsLevel.Generate(index));

        public override string DescribeDifficulty(int levelIndex, ILevelData level) =>
            level is TapTargetsLevel l ? $"targets_{l.targetCount}_spare_{l.tapLimit - l.targetCount}" : "";
    }
}
