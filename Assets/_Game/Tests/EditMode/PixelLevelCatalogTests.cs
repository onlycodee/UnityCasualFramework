using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using static Game.Tests.PixelLoopTestUtil;

namespace Game.Tests
{
    /// <summary>Every shipped level parses, uses its whole palette and can be cleared with the planned queue.</summary>
    public class PixelLevelCatalogTests
    {
        static IEnumerable<string> Files() => LevelFileNames();

        static PixelLevel Load(string file) => PixelLevel.FromJson(File.ReadAllText(Path.Combine(LevelsDirectory, file)));

        [Test]
        public void Catalog_HasLevels()
        {
            Assert.That(LevelFiles().Count(), Is.GreaterThanOrEqualTo(20));
        }

        [TestCaseSource(nameof(Files))]
        public void Level_IsWellFormed(string file)
        {
            var level = Load(file);
            Assert.IsNotEmpty(level.id);
            Assert.IsNotEmpty(level.name);
            Assert.That(level.Width, Is.InRange(4, 20));
            Assert.That(level.Height, Is.InRange(4, 20));
            foreach (var row in level.rows) Assert.AreEqual(level.Width, row.Length, $"{file}: ragged row '{row}'");
            for (int c = 0; c < level.colors.Length; c++)
                Assert.Greater(level.CountColor(c), 0, $"{file}: colour {c} ({level.colors[c]}) is never used");
        }

        [TestCaseSource(nameof(Files))]
        public void PlannedQueue_HasExactlyTheAmmoNeeded(string file)
        {
            var level = Load(file);
            for (int c = 0; c < level.colors.Length; c++)
            {
                int ammo = level.shooters.Where(s => s.color == c).Sum(s => s.ammo);
                Assert.AreEqual(level.CountColor(c), ammo, $"{file}: colour {c}");
            }
            Assert.IsTrue(level.shooters.All(s => s.ammo > 0 && s.ammo <= level.maxAmmo));
        }

        [TestCaseSource(nameof(Files))]
        public void PlannedQueue_ClearsThePicture_OneShooterAtATime(string file)
        {
            var sim = new BeltSim(Load(file));
            PlaySequential(sim);
            Assert.IsTrue(sim.IsWon, $"{file}: {sim.Remaining} pixels left, lost={sim.IsLost} ({sim.LoseReason})");
            Assert.AreEqual(0, sim.Docks, "a planned shooter came back with ammo");
            Assert.AreEqual(3, sim.Stars);
        }

        [TestCaseSource(nameof(Files))]
        public void Plan_IsDeterministic(string file)
        {
            var a = Load(file).shooters;
            var b = Load(file).shooters;
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Ids_AreUnique()
        {
            var ids = LevelFiles().Select(f => PixelLevel.FromJson(File.ReadAllText(f)).id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
        }

#if UNITY_EDITOR
        [Test]
        public void Module_ListsEveryLevelFile_InOrder()
        {
            var module = UnityEngine.Resources.Load<PixelLoopModule>("PixelLoopModule");
            Assert.IsNotNull(module, "Assets/_Game/Resources/PixelLoopModule.asset is missing");
            var listed = module.levels.Select(t => t != null ? t.name + ".json" : "<missing>").ToList();
            CollectionAssert.AreEqual(LevelFileNames().ToList(), listed);
        }
#endif
    }
}
