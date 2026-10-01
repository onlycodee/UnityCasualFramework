using System;
using HyperFrame.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HyperFrame.Services.Tests
{
    public class SaveServiceTests
    {
        [Serializable] class Profile { public string name = "anon"; public int level; }

        sealed class QuietSink : ILogSink
        {
            public int Errors;
            public void Write(LogLevel level, string tag, string message) { if (level == LogLevel.Error) Errors++; }
            public void WriteException(Exception exception, string tag) => Errors++;
        }

        QuietSink _sink;
        [SetUp] public void SetUp() { _sink = new QuietSink(); HFLog.Sink = _sink; }
        [TearDown] public void TearDown() => HFLog.Sink = null;

        [Test]
        public void SetSaveLoad_RoundTrips()
        {
            var storage = new InMemorySaveStorage();
            var a = new SaveService(storage);
            a.Set("profile", new Profile { name = "phuong", level = 7 });
            Assert.IsTrue(a.IsDirty);
            a.Save();
            Assert.IsFalse(a.IsDirty);

            var b = new SaveService(storage);
            b.Load();
            var p = b.Get<Profile>("profile");
            Assert.AreEqual("phuong", p.name);
            Assert.AreEqual(7, p.level);
        }

        [Test]
        public void MissingSection_ReturnsDefaults()
        {
            var s = new SaveService(new InMemorySaveStorage());
            s.Load();
            Assert.AreEqual("anon", s.Get<Profile>("profile").name);
        }

        [Test]
        public void OldSave_IsMigrated_InOrder()
        {
            var storage = new InMemorySaveStorage();
            storage.Write("save", "{\"version\":1,\"sections\":{\"profile\":{\"nick\":\"old\",\"lvl\":3}}}");
            var migrations = new ISaveMigration[]
            {
                new SaveMigration(2, root => root["sections"]["profile"]["level"] = (int)root["sections"]["profile"]["level"] + 1),
                new SaveMigration(1, root =>
                {
                    var p = (JObject)root["sections"]["profile"];
                    p["name"] = p["nick"]; p.Remove("nick");
                    p["level"] = p["lvl"]; p.Remove("lvl");
                }),
            };
            var s = new SaveService(storage, schemaVersion: 3, migrations: migrations);
            s.Load();
            var profile = s.Get<Profile>("profile");
            Assert.AreEqual("old", profile.name);
            Assert.AreEqual(4, profile.level);
            Assert.IsTrue(s.IsDirty, "migrated data should be written back");
            s.Save();
            StringAssert.Contains("\"version\":3", storage.Files["save"]);
        }

        [Test]
        public void CorruptFile_IsBackedUp_AndGameStartsFresh()
        {
            var storage = new InMemorySaveStorage();
            storage.Write("save", "{not json");
            var s = new SaveService(storage);
            s.Load();
            Assert.AreEqual("anon", s.Get<Profile>("profile").name);
            Assert.AreEqual("{not json", storage.Files["save.corrupt"]);
            Assert.AreEqual(1, _sink.Errors);
        }

        [Test]
        public void MissingMigration_IsBackedUp_NotCrashing()
        {
            var storage = new InMemorySaveStorage();
            storage.Write("save", "{\"version\":1,\"sections\":{}}");
            var s = new SaveService(storage, schemaVersion: 2);
            s.Load();
            Assert.IsTrue(storage.Files.ContainsKey("save.corrupt"));
        }

        [Test]
        public void NewerSaveThanBuild_IsNotOverwrittenSilently()
        {
            var storage = new InMemorySaveStorage();
            storage.Write("save", "{\"version\":9,\"sections\":{}}");
            var s = new SaveService(storage, schemaVersion: 2);
            s.Load();
            Assert.IsTrue(storage.Files.ContainsKey("save.corrupt"));
        }

        [Test]
        public void Autosaves_OnAppPause_AndQuit_WhenDirty()
        {
            var storage = new InMemorySaveStorage();
            var bus = new EventBus();
            var s = new SaveService(storage, events: bus);
            int saves = 0;
            s.Saved += () => saves++;
            bus.Publish(new AppPauseEvent { Paused = true });
            Assert.AreEqual(0, saves, "nothing changed, nothing to save");
            s.Set("x", new Profile());
            bus.Publish(new AppPauseEvent { Paused = true });
            Assert.AreEqual(1, saves);
            s.Set("x", new Profile { level = 2 });
            bus.Publish(new AppQuitEvent());
            Assert.AreEqual(2, saves);
        }
    }

    public class EconomyAndProgressionTests
    {
        [Test]
        public void Wallet_AddSpend_Persists_AndPublishes()
        {
            var save = new SaveService(new InMemorySaveStorage());
            var bus = new EventBus();
            CurrencyChangedEvent last = default;
            bus.Subscribe<CurrencyChangedEvent>(e => last = e);
            var wallet = new Wallet(save, bus, new System.Collections.Generic.Dictionary<string, long> { [Currencies.Coins] = 100 });

            Assert.AreEqual(100, wallet.Get(Currencies.Coins));
            wallet.Add(Currencies.Coins, 50, "win");
            Assert.AreEqual(150, last.Balance);
            Assert.AreEqual("win", last.Source);
            Assert.IsFalse(wallet.TrySpend(Currencies.Coins, 1000, "shop"));
            Assert.IsTrue(wallet.TrySpend(Currencies.Coins, 30, "shop"));
            Assert.AreEqual(-30, last.Delta);

            var reloaded = new Wallet(save, null, new System.Collections.Generic.Dictionary<string, long> { [Currencies.Coins] = 100 });
            Assert.AreEqual(120, reloaded.Get(Currencies.Coins), "starting balance applies only to a fresh save");
        }

        [Test]
        public void Wallet_RejectsNegativeAdd()
        {
            var wallet = new Wallet(new SaveService(new InMemorySaveStorage()));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add(Currencies.Coins, -5, "x"));
        }

        [Test]
        public void Progression_CompleteUnlocksNext_KeepsBestStars()
        {
            var save = new SaveService(new InMemorySaveStorage());
            var p = new ProgressionService(save);
            Assert.IsTrue(p.IsUnlocked(0));
            Assert.IsFalse(p.IsUnlocked(1));
            p.CompleteLevel(0, 2);
            Assert.AreEqual(1, p.CurrentLevelIndex);
            Assert.IsTrue(p.IsUnlocked(1));
            p.CompleteLevel(0, 1);
            Assert.AreEqual(2, p.GetStars(0));
            p.CompleteLevel(0, 3);
            Assert.AreEqual(3, p.TotalStars);
            Assert.AreEqual(1, new ProgressionService(save).CurrentLevelIndex, "persisted");
        }

        [Test]
        public void Progression_Attempts_ResetOnCompletion()
        {
            var p = new ProgressionService(new SaveService(new InMemorySaveStorage()));
            p.RecordAttempt(0);
            p.RecordAttempt(0);
            Assert.AreEqual(2, p.AttemptsOnCurrent);
            p.CompleteLevel(0, 1);
            Assert.AreEqual(0, p.AttemptsOnCurrent);
        }

        [Test]
        public void Settings_PersistChanges()
        {
            var save = new SaveService(new InMemorySaveStorage());
            var s = new SettingsService(save, "1.2.3");
            s.MusicOn.Value = false;
            s.Language.Value = "vi";
            var again = new SettingsService(save);
            Assert.IsFalse(again.MusicOn.Value);
            Assert.AreEqual("vi", again.Language.Value);
            Assert.IsTrue(again.SfxOn.Value);
        }
    }
}
