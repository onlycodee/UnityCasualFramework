using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Tests
{
    static class PixelLoopTestUtil
    {
        /// <summary>A level from rows (top first) with an explicit queue.</summary>
        public static PixelLevel Level(string[] rows, int colors, int columns, int capacity, int tray, params (int color, int ammo)[] shooters)
        {
            var level = new PixelLevel
            {
                id = "test",
                colors = Enumerable.Range(0, colors).Select(i => "#FFFFFF").ToArray(),
                rows = rows,
                queueColumns = columns,
                beltCapacity = capacity,
                traySlots = tray,
                loopSeconds = 2f,
                shooters = shooters.Select(s => new ShooterSpec(s.color, s.ammo)).ToList()
            };
            return level.Build();
        }

        /// <summary>Runs the sim at 60 fps until it ends or the time runs out.</summary>
        public static void Run(BeltSim sim, float seconds, Action<BeltSim> eachFrame = null)
        {
            for (float t = 0f; t < seconds && !sim.IsOver; t += 1f / 60f)
            {
                eachFrame?.Invoke(sim);
                sim.Step(1f / 60f);
            }
        }

        /// <summary>Plays the planned order, one shooter on the belt at a time.</summary>
        public static void PlaySequential(BeltSim sim, float maxSeconds = 600f)
        {
            Run(sim, maxSeconds, s =>
            {
                if (s.BeltLoad > 0) return;
                Shooter next = null;
                foreach (var sh in s.Shooters)
                    if ((sh.State == ShooterState.Queued || sh.State == ShooterState.Tray) && (next == null || sh.Id < next.Id)) next = sh;
                if (next != null) s.TryLaunch(next);
            });
        }

        public static string LevelsDirectory
        {
            get
            {
#if UNITY_5_3_OR_NEWER
                return Path.Combine(UnityEngine.Application.dataPath, "_Game", "Levels");
#else
                return Environment.GetEnvironmentVariable("PIXEL_LOOP_LEVELS") ?? "Assets/_Game/Levels";
#endif
            }
        }

        public static IEnumerable<string> LevelFiles() =>
            Directory.GetFiles(LevelsDirectory, "*.json").OrderBy(p => p, StringComparer.Ordinal);

        public static IEnumerable<string> LevelFileNames() => LevelFiles().Select(Path.GetFileName);
    }
}
