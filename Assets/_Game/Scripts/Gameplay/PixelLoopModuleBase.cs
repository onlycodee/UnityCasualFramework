using System.Collections.Generic;
using HyperFrame.App;
using HyperFrame.Audio;
using HyperFrame.Core;
using HyperFrame.Services;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// What the 2D and 3D Pixel Loop modules share: the level list, sounds and debug commands. Each subclass
    /// only adds its presentation. Pick one in GameDefinition.gameplay.
    /// </summary>
    public abstract class PixelLoopModuleBase : GameplayModule
    {
        [Tooltip("Level JSON files in play order.")]
        public List<TextAsset> levels = new List<TextAsset>();
        [Tooltip("After the last level, continue from this index.")]
        public int loopFromIndex = 5;

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
            DebugCommands.Register("Game", "Auto play on/off", () => PixelLoopDebug.AutoPlay = !PixelLoopDebug.AutoPlay,
                () => $"Auto play: {(PixelLoopDebug.AutoPlay ? "on" : "off")}");
            DebugCommands.Register("Game", "Belt speed x1 / x3", () => PixelLoopDebug.SpeedBoost = PixelLoopDebug.SpeedBoost > 1f ? 1f : 3f,
                () => $"Belt speed x{PixelLoopDebug.SpeedBoost:0}");
        }

        public override string DescribeDifficulty(int levelIndex, ILevelData level) =>
            level is PixelLevel l ? $"px_{l.PixelCount}_col_{l.colors.Length}_sh_{l.shooters.Count}_belt_{l.beltCapacity}" : "";
    }

    /// <summary>Debug console toggles (Game/…), read by both presentations.</summary>
    public static class PixelLoopDebug
    {
        /// <summary>A bot plays the best available shooter (<see cref="BeltBot.Choose"/>).</summary>
        public static bool AutoPlay;
        public static float SpeedBoost = 1f;
    }

    /// <summary>What tests and bots need from either presentation.</summary>
    public interface IPixelLoopGameplay : IGameplay
    {
        BeltSim Sim { get; }
        /// <summary>World point a player would tap to send this shooter.</summary>
        Vector3 TapPointFor(Shooter s);
    }
}
