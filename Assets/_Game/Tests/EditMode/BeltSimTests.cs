using NUnit.Framework;
using static Game.Tests.PixelLoopTestUtil;

namespace Game.Tests
{
    /// <summary>The Pixel Loop rules (BeltSim) without any engine objects.</summary>
    public class BeltSimTests
    {
        static readonly string[] Ring =
        {
            "000",
            "010",
            "000",
        };

        [Test]
        public void Rows_AreTopFirst()
        {
            var level = Level(new[] { "0.", ".1" }, 2, 1, 1, 1, (0, 1));
            Assert.AreEqual(0, level.Cell(0, 1), "top-left");
            Assert.AreEqual(1, level.Cell(1, 0), "bottom-right");
            Assert.AreEqual(-1, level.Cell(1, 1));
        }

        [Test]
        public void Shooter_ClearsMatchingOuterPixels_AndIsSpent()
        {
            var sim = new BeltSim(Level(new[] { "00" }, 1, 1, 1, 1, (0, 2)));
            Shooter spent = null;
            sim.Spent += s => spent = s;
            Assert.IsTrue(sim.TryLaunchColumn(0));
            Run(sim, 5f);
            Assert.IsTrue(sim.IsWon);
            Assert.AreEqual(0, sim.Remaining);
            Assert.IsNotNull(spent);
            Assert.AreEqual(ShooterState.Spent, spent.State);
        }

        [Test]
        public void Shooter_CannotHitAColourBehindAnother()
        {
            var sim = new BeltSim(Level(Ring, 2, 1, 1, 2, (1, 1)));
            Assert.IsTrue(sim.TryLaunchColumn(0));
            Run(sim, 3f);
            Assert.IsFalse(sim.IsOver);
            Assert.AreEqual(9, sim.Remaining, "nothing was hit");
            Assert.AreEqual(ShooterState.Tray, sim.Shooters[0].State, "a shooter that finishes the loop with ammo parks in the tray");
            Assert.AreEqual(1, sim.Shooters[0].Ammo);
            Assert.AreEqual(1, sim.Docks);
        }

        [Test]
        public void Peeling_TheOuterLayer_ExposesTheInside()
        {
            var sim = new BeltSim(Level(Ring, 2, 1, 1, 2, (0, 8), (1, 1)));
            Assert.IsTrue(sim.TryLaunchColumn(0));
            Run(sim, 3f);
            Assert.AreEqual(1, sim.Remaining);
            Assert.IsTrue(sim.TryLaunchColumn(0));
            Run(sim, 3f);
            Assert.IsTrue(sim.IsWon);
            Assert.AreEqual(0, sim.Docks);
        }

        [Test]
        public void FullTray_LosesTheLevel()
        {
            var sim = new BeltSim(Level(Ring, 2, 2, 2, 1, (1, 1), (1, 1)));
            string reason = null;
            sim.Lost += r => reason = r;
            Assert.IsTrue(sim.TryLaunchColumn(0));
            Assert.IsTrue(sim.TryLaunchColumn(1));
            Run(sim, 5f);
            Assert.IsTrue(sim.IsLost);
            Assert.AreEqual("tray_full", reason);
        }

        [Test]
        public void TrayShooter_CanBeSentAgain()
        {
            var sim = new BeltSim(Level(Ring, 2, 1, 1, 2, (1, 1), (0, 8)));
            sim.TryLaunchColumn(0);
            Run(sim, 3f); // blocked: parks in the tray
            var parked = sim.Tray[0];
            Assert.IsNotNull(parked);
            sim.TryLaunchColumn(0);
            Run(sim, 3f); // outer ring cleared
            Assert.IsTrue(sim.TryLaunch(parked));
            Assert.IsNull(sim.Tray[0]);
            Run(sim, 3f);
            Assert.IsTrue(sim.IsWon);
        }

        [Test]
        public void BeltCapacity_RefusesExtraShooters()
        {
            var sim = new BeltSim(Level(Ring, 2, 2, 1, 2, (0, 4), (0, 4)));
            int denied = 0;
            sim.Denied += _ => denied++;
            Assert.IsTrue(sim.TryLaunchColumn(0));
            Assert.IsFalse(sim.TryLaunchColumn(1));
            Assert.AreEqual(1, denied);
        }

        [Test]
        public void OnlyTheFrontOfAColumn_CanBeSent()
        {
            var sim = new BeltSim(Level(Ring, 2, 1, 3, 2, (0, 4), (0, 4)));
            var second = sim.Columns[0][1];
            Assert.IsFalse(sim.TryLaunch(second));
            Assert.IsTrue(sim.TryLaunch(sim.Front(0)));
            Assert.AreSame(second, sim.Front(0));
        }

        [Test]
        public void ShootersEnteringTogether_KeepTheirSpacing()
        {
            var sim = new BeltSim(Level(Ring, 2, 2, 2, 2, (1, 1), (1, 1))); // blocked colour: they never fire
            sim.TryLaunchColumn(0);
            sim.TryLaunchColumn(1);
            Assert.AreEqual(2, sim.Waiting.Count, "both hop towards the entry first");
            Run(sim, BeltSim.HopSeconds + 0.02f);
            Assert.AreEqual(1, sim.Belt.Count, "second waits for room at the entry");
            Assert.AreEqual(1, sim.Waiting.Count);
            Run(sim, 0.5f);
            Assert.AreEqual(2, sim.Belt.Count);
            Assert.That(sim.Belt[0].Distance - sim.Belt[1].Distance, Is.GreaterThanOrEqualTo(BeltGeometry.Spacing - 0.01f));
        }

        [Test]
        public void LongFrames_DoNotSkipLanes()
        {
            var slow = new BeltSim(Level(new[] { "0000", "0000" }, 1, 1, 1, 1, (0, 8)));
            slow.TryLaunchColumn(0);
            for (int i = 0; i < 4 && !slow.IsOver; i++) slow.Step(1f); // 2 s loop in 1 s frames
            Assert.IsTrue(slow.IsWon);
        }

        [Test]
        public void Geometry_LanesLineUpWithPixels()
        {
            var g = new BeltGeometry(4, 3);
            Assert.AreEqual(2 * 4 + 2 * 3, g.Stops.Length);
            for (int i = 1; i < g.Stops.Length; i++) Assert.Greater(g.Stops[i].Distance, g.Stops[i - 1].Distance);
            foreach (var stop in g.Stops)
            {
                var p = g.At(stop.Distance);
                if (stop.Side == BeltSide.Bottom || stop.Side == BeltSide.Top)
                {
                    g.CellCenter(stop.Lane, 0, out float cx, out _);
                    Assert.AreEqual(cx, p.X, 1e-4f, $"{stop.Side} lane {stop.Lane}");
                }
                else
                {
                    g.CellCenter(0, stop.Lane, out _, out float cy);
                    Assert.AreEqual(cy, p.Y, 1e-4f, $"{stop.Side} lane {stop.Lane}");
                }
            }
            var start = g.At(0f);
            var end = g.At(g.Length - 1e-4f);
            Assert.AreEqual(start.X, end.X, 1e-3f);
            Assert.AreEqual(start.Y, end.Y, 1e-3f);
        }
    }
}
