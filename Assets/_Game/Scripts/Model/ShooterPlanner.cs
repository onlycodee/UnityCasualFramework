using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// Builds a shooter queue for a picture that is always clearable: it peels the picture the way a
    /// lone shooter would, choosing each time a colour that can land many shots in one loop, and gives
    /// that shooter exactly the shots it will land (capped by maxAmmo). Sending the queue one shooter at a
    /// time, in order, therefore clears the picture with no shooter ever reaching the tray. Players who
    /// send several at once, or out of order, can still get shooters back with ammo left — that is the game.
    /// </summary>
    public static class ShooterPlanner
    {
        /// <summary>Colours whose loop yield is at least this share of the best one are all fair picks.</summary>
        const float PickWindow = 0.6f;

        public static List<ShooterSpec> Plan(PixelLevel level)
        {
            var geometry = new BeltGeometry(level.Width, level.Height);
            var cells = level.CopyCells();
            var rng = new Random(level.seed);
            var plan = new List<ShooterSpec>();
            int colors = level.colors.Length;
            var yield = new int[colors];
            var candidates = new List<int>();
            int remaining = level.PixelCount;

            while (remaining > 0)
            {
                int best = 0;
                for (int c = 0; c < colors; c++)
                {
                    yield[c] = BeltSim.LoopHits(cells, geometry, c, int.MaxValue, apply: false);
                    best = Math.Max(best, yield[c]);
                }
                // Some pixel always touches the outside of the remaining picture, so best > 0.
                if (best == 0) throw new InvalidOperationException($"Level '{level.id}': no colour can be hit.");

                candidates.Clear();
                for (int c = 0; c < colors; c++)
                    if (yield[c] > 0 && yield[c] >= best * PickWindow) candidates.Add(c);
                int color = candidates[rng.Next(candidates.Count)];
                int ammo = Math.Min(yield[color], level.maxAmmo);
                BeltSim.LoopHits(cells, geometry, color, ammo, apply: true);
                remaining -= ammo;
                plan.Add(new ShooterSpec(color, ammo));
            }
            return plan;
        }
    }
}
