using System;
using System.Collections.Generic;
using HyperFrame.Services;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Level data for the Phase 0 dummy game "Tap Targets": tap every target before running out of taps.
    /// Generated from a seed so levels are deterministic and testable.
    /// </summary>
    [Serializable]
    public sealed class TapTargetsLevel : ILevelData
    {
        public string id;
        public int targetCount;
        /// <summary>Taps allowed (hits + misses).</summary>
        public int tapLimit;
        public List<Vector2> positions = new List<Vector2>();

        public string Id => id;

        /// <summary>Play area in world units (camera ortho size 8, portrait), clear of the HUD.</summary>
        public static readonly Rect PlayArea = new Rect(-3.2f, -5.5f, 6.4f, 9.5f);
        public const float TargetRadius = 0.6f;

        public static TapTargetsLevel Generate(int index)
        {
            var rng = new System.Random(1000 + index);
            int count = 3 + Math.Min(index, 12) / 2 + rng.Next(0, 2);
            var level = new TapTargetsLevel
            {
                id = $"tap_{index + 1:000}",
                targetCount = count,
                tapLimit = count + Math.Max(1, 4 - index / 5) // fewer spare taps as levels go on
            };

            int guard = 0;
            while (level.positions.Count < count && guard++ < 1000)
            {
                var p = new Vector2(
                    PlayArea.xMin + (float)rng.NextDouble() * PlayArea.width,
                    PlayArea.yMin + (float)rng.NextDouble() * PlayArea.height);
                bool overlaps = level.positions.Exists(q => Vector2.Distance(p, q) < TargetRadius * 2.4f);
                if (!overlaps) level.positions.Add(p);
            }
            level.targetCount = level.positions.Count;
            return level;
        }
    }
}
