namespace Game
{
    /// <summary>
    /// Choices a helper player makes, shared by the 2D and 3D presentations: the debug-console auto player
    /// and the planned order used by tests and bots.
    /// </summary>
    public static class BeltBot
    {
        /// <summary>
        /// The shooter that would clear the most pixels in one loop right now, or null to wait (belt full,
        /// nothing useful, or a shooter of that colour is already working on the belt).
        /// </summary>
        public static Shooter Choose(BeltSim sim)
        {
            if (sim.IsOver || sim.BeltFull) return null;
            var cells = sim.SnapshotCells();
            Shooter best = null;
            int bestHits = 0;
            void Consider(Shooter s)
            {
                if (s == null || !sim.CanLaunch(s)) return;
                foreach (var b in sim.Belt) if (b.Color == s.Color) return; // let the one on the belt work first
                int hits = BeltSim.LoopHits(cells, sim.Geometry, s.Color, s.Ammo, apply: false);
                if (hits > bestHits) { best = s; bestHits = hits; }
            }
            foreach (var s in sim.Tray) Consider(s);
            for (int c = 0; c < sim.Columns.Count; c++) Consider(sim.Front(c));
            return best;
        }

        /// <summary>Next shooter of the planned order still waiting to be sent (queue or tray).</summary>
        public static Shooter NextPlanned(BeltSim sim)
        {
            Shooter best = null;
            foreach (var s in sim.Shooters)
                if ((s.State == ShooterState.Queued || s.State == ShooterState.Tray) && (best == null || s.Id < best.Id))
                    best = s;
            return best;
        }
    }
}
