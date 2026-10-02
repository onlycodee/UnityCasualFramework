using System.Collections.Generic;
using HyperFrame.App;
using HyperFrame.Audio;
using HyperFrame.Core;
using HyperFrame.Services;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The game's plug-in (referenced from GameDefinition.gameplay). Levels are the JSON pictures in
    /// Assets/_Game/Levels, listed here in play order; after the last one play loops from loopFromIndex.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Pixel Loop Module", fileName = "PixelLoopModule")]
    public sealed class PixelLoopModule : GameplayModule
    {
        [Tooltip("Level JSON files in play order.")]
        public List<TextAsset> levels = new List<TextAsset>();
        [Tooltip("After the last level, continue from this index.")]
        public int loopFromIndex = 5;
        public PixelLoopStyle style = new PixelLoopStyle();

        public override IGameplay CreateGameplay() => new PixelLoopGameplay(style);

        public override ILevelProvider CreateLevelProvider(GameDefinition definition)
        {
            var cache = new Dictionary<int, ILevelData>();
            return new FuncLevelProvider(levels.Count, index =>
            {
                int i = LevelIndexing.Wrap(index, levels.Count, loopFromIndex);
                if (!cache.TryGetValue(i, out var level)) cache[i] = level = ParseLevel(levels[i]);
                return level;
            });
        }

        public override ILevelData ParseLevel(TextAsset json) => PixelLevel.FromJson(json.text);

        public override void ConfigureSounds(SoundTable table) => PixelLoopSounds.Configure(table);

        public override void OnBoot()
        {
            DebugCommands.Register("Game", "Auto play on/off", () => PixelLoopGameplay.AutoPlay = !PixelLoopGameplay.AutoPlay,
                () => $"Auto play: {(PixelLoopGameplay.AutoPlay ? "on" : "off")}");
            DebugCommands.Register("Game", "Belt speed x1 / x3", () => PixelLoopGameplay.SpeedBoost = PixelLoopGameplay.SpeedBoost > 1f ? 1f : 3f,
                () => $"Belt speed x{PixelLoopGameplay.SpeedBoost:0}");
        }

        public override string DescribeDifficulty(int levelIndex, ILevelData level) =>
            level is PixelLevel l ? $"px_{l.PixelCount}_col_{l.colors.Length}_sh_{l.shooters.Count}_belt_{l.beltCapacity}" : "";
    }

    /// <summary>Colours of the stage (AR-3: look-and-feel is data). Pixel colours come from each level.</summary>
    [System.Serializable]
    public sealed class PixelLoopStyle
    {
        public Color backgroundTop = new Color(0.22f, 0.19f, 0.42f);
        public Color backgroundBottom = new Color(0.09f, 0.08f, 0.20f);
        public Color board = new Color(0.96f, 0.95f, 0.99f);
        public Color boardShadow = new Color(0f, 0f, 0f, 0.35f);
        public Color track = new Color(0.17f, 0.15f, 0.29f);
        public Color tread = new Color(0.33f, 0.30f, 0.52f);
        public Color slot = new Color(1f, 1f, 1f, 0.10f);
        public Color warning = new Color(1f, 0.32f, 0.36f);
        public Color progress = new Color(0.45f, 0.92f, 0.62f);
        public Color bokeh = new Color(0.65f, 0.55f, 1f, 0.10f);
    }
}
