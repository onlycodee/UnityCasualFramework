using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class TapTargetsLevelTests
    {
        [Test]
        public void Generate_IsDeterministic()
        {
            var a = TapTargetsLevel.Generate(7);
            var b = TapTargetsLevel.Generate(7);
            Assert.AreEqual(a.id, b.id);
            CollectionAssert.AreEqual(a.positions, b.positions);
        }

        [Test]
        public void Generate_First50Levels_AreWinnable([NUnit.Framework.Range(0, 49)] int index)
        {
            var level = TapTargetsLevel.Generate(index);
            Assert.That(level.targetCount, Is.GreaterThanOrEqualTo(3));
            Assert.AreEqual(level.targetCount, level.positions.Count);
            Assert.That(level.tapLimit, Is.GreaterThanOrEqualTo(level.targetCount), "a level needs at least one tap per target");

            for (int i = 0; i < level.positions.Count; i++)
            {
                Assert.IsTrue(TapTargetsLevel.PlayArea.Contains(level.positions[i]), $"target {i} outside play area");
                for (int j = i + 1; j < level.positions.Count; j++)
                    Assert.That(Vector2.Distance(level.positions[i], level.positions[j]),
                        Is.GreaterThan(TapTargetsLevel.TargetRadius * 2f), $"targets {i} and {j} overlap");
            }
        }
    }
}
