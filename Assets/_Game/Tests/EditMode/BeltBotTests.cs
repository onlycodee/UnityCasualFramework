using NUnit.Framework;
using static Game.Tests.PixelLoopTestUtil;

namespace Game.Tests
{
    /// <summary>The helper player shared by the 2D and 3D presentations (debug auto play, test bots).</summary>
    public class BeltBotTests
    {
        static BeltSim TwoColours() => new BeltSim(Level(new[] { "0011", "0011" }, 2, 2, 2, 3, (0, 4), (1, 4)));

        [Test]
        public void Choose_PicksALaunchableShooterThatHits()
        {
            var sim = TwoColours();
            var pick = BeltBot.Choose(sim);
            Assert.IsNotNull(pick);
            Assert.IsTrue(sim.CanLaunch(pick));
        }

        [Test]
        public void Choose_WaitsWhileTheSameColourIsOnTheBelt()
        {
            var sim = new BeltSim(Level(new[] { "00", "00" }, 1, 2, 3, 3, (0, 2), (0, 2)));
            Assert.IsTrue(sim.TryLaunch(BeltBot.Choose(sim)));
            sim.Step(BeltSim.HopSeconds + 0.02f); // hops onto the belt, not yet at the first lane
            Assert.AreEqual(1, sim.Belt.Count);
            Assert.IsNull(BeltBot.Choose(sim), "a second shooter of the same colour should wait");
        }

        [Test]
        public void AutoPlay_ClearsALevel()
        {
            var sim = TwoColours();
            Run(sim, 60f, s =>
            {
                var pick = BeltBot.Choose(s);
                if (pick != null) s.TryLaunch(pick);
            });
            Assert.IsTrue(sim.IsWon, $"{sim.Remaining} pixels left, lost={sim.IsLost}");
        }

        [Test]
        public void NextPlanned_FollowsShooterIds()
        {
            var sim = TwoColours();
            Assert.AreEqual(0, BeltBot.NextPlanned(sim).Id);
            sim.TryLaunch(BeltBot.NextPlanned(sim));
            Assert.AreEqual(1, BeltBot.NextPlanned(sim).Id);
        }
    }
}
