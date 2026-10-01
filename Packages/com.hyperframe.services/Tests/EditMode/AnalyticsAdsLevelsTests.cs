using System;
using System.Collections.Generic;
using HyperFrame.Core;
using NUnit.Framework;

namespace HyperFrame.Services.Tests
{
    public class AnalyticsTests
    {
        [TearDown] public void TearDown() => HFLog.Sink = null;

        [Test]
        public void TypedHelpers_ProduceSchemaValidEvents()
        {
            var a = new AnalyticsDispatcher { StrictSchema = true };
            var mem = new InMemoryAnalyticsBackend();
            a.AddBackend(mem);
            a.SessionStart(1, 0);
            a.LevelStart("l1", 0, 1, "easy");
            a.LevelComplete("l1", 12.345f, 10, 3, 0);
            a.LevelFail("l1", 3f, 5, "out_of_moves");
            a.TutorialStep("tap", 0);
            a.BoosterUse("undo", "l1", "coin");
            a.Ad(AnalyticsSchema.AdImpression, AdFormat.Rewarded, "p", "mock");
            a.IapPurchase("remove_ads", 2.99m, "USD");
            a.CurrencyChange(new CurrencyChangedEvent { Currency = "coins", Delta = 5, Balance = 5, Source = "x" });
            Assert.AreEqual(9, mem.Events.Count);
            Assert.AreEqual(12.35, (double)mem.Last(AnalyticsSchema.LevelComplete).Parameters["duration_s"], 1e-9);
        }

        [Test]
        public void UnknownEvent_IsRejectedInStrictMode()
        {
            var a = new AnalyticsDispatcher { StrictSchema = true };
            Assert.Throws<ArgumentException>(() => a.Log("made_up_event"));
            Assert.Throws<ArgumentException>(() => a.Log(AnalyticsSchema.LevelStart, new Dictionary<string, object> { ["level_id"] = "x" }));
        }

        [Test]
        public void CustomEvents_CanBeRegistered()
        {
            AnalyticsSchema.RegisterCustom("tube_complete", "level_id");
            var a = new AnalyticsDispatcher { StrictSchema = true };
            Assert.DoesNotThrow(() => a.Log("tube_complete", new Dictionary<string, object> { ["level_id"] = "1" }));
        }

        [Test]
        public void FailingBackend_DoesNotBreakOthers()
        {
            HFLog.Sink = new ConsoleLogSink();
            var a = new AnalyticsDispatcher();
            var mem = new InMemoryAnalyticsBackend();
            a.AddBackend(new ThrowingBackend());
            a.AddBackend(mem);
            a.SessionStart(1, 0);
            Assert.AreEqual(1, mem.Events.Count);
        }

        sealed class ThrowingBackend : IAnalyticsBackend
        {
            public string Name => "throws";
            public void Send(AnalyticsEvent evt) => throw new InvalidOperationException();
        }
    }

    public class MockAdsTests
    {
        static AdResult Run(System.Threading.Tasks.Task<AdResult> t) => t.GetAwaiter().GetResult();

        [Test]
        public void Rewarded_Completed_LogsRequestImpressionReward()
        {
            var a = new AnalyticsDispatcher();
            var mem = new InMemoryAnalyticsBackend();
            a.AddBackend(mem);
            var ads = new MockAdsService(a);
            Assert.AreEqual(AdResult.Completed, Run(ads.ShowRewarded("continue")));
            Assert.AreEqual(1, mem.Count(AnalyticsSchema.AdRequest));
            Assert.AreEqual(1, mem.Count(AnalyticsSchema.AdImpression));
            Assert.AreEqual(1, mem.Count(AnalyticsSchema.AdReward));
        }

        [TestCase(AdResult.Skipped)]
        [TestCase(AdResult.Failed)]
        public void Rewarded_NotCompleted_GivesNoReward(AdResult result)
        {
            var a = new AnalyticsDispatcher();
            var mem = new InMemoryAnalyticsBackend();
            a.AddBackend(mem);
            var ads = new MockAdsService(a) { NextResult = result };
            Assert.AreEqual(result, Run(ads.ShowRewarded("x")));
            Assert.AreEqual(0, mem.Count(AnalyticsSchema.AdReward));
        }

        [Test]
        public void NoAds_DisablesInterstitial_ButNotRewarded()
        {
            var ads = new MockAdsService { AdsRemoved = true };
            Assert.AreEqual(AdResult.Disabled, Run(ads.ShowInterstitial("level_end")));
            Assert.AreEqual(AdResult.Completed, Run(ads.ShowRewarded("x")));
            Assert.IsFalse(ads.IsReady(AdFormat.Interstitial));
        }

        [Test]
        public void MockIap_PurchaseOwns_AndRepeatIsAlreadyOwned()
        {
            var iap = new MockIAPService();
            Assert.AreEqual(PurchaseResult.Success, iap.Purchase(Products.RemoveAds).Result);
            Assert.IsTrue(iap.IsOwned(Products.RemoveAds));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, iap.Purchase(Products.RemoveAds).Result);
        }
    }

    public class LevelTests
    {
        [TestCase(0, 10, 0, 0)]
        [TestCase(9, 10, 0, 9)]
        [TestCase(10, 10, 0, 0)]
        [TestCase(10, 10, 5, 5)]
        [TestCase(14, 10, 5, 9)]
        [TestCase(15, 10, 5, 5)]
        public void Wrap_LoopsFromIndex(int index, int count, int loopFrom, int expected) =>
            Assert.AreEqual(expected, LevelIndexing.Wrap(index, count, loopFrom));

        // A toy puzzle: reach a target number from 0 with +1 / +3 moves. Exercises the batch validator.
        sealed class Toy { public int Target; }
        sealed class ToyGen : ILevelGenerator<Toy>
        {
            public Toy Generate(int seed, DifficultyParams p) => new Toy { Target = 2 + (int)(p.difficulty * 20) + seed % 3 };
        }
        sealed class ToySolver : ILevelSolver<Toy, int>
        {
            public SolveResult<int> Solve(Toy level, int maxNodes)
            {
                var r = new SolveResult<int> { Solvable = true };
                int v = 0;
                while (v + 3 <= level.Target) { r.Moves.Add(3); v += 3; }
                while (v < level.Target) { r.Moves.Add(1); v += 1; }
                r.NodesExplored = r.Moves.Count;
                return r;
            }
        }

        [Test]
        public void BatchValidator_ReportsSolvability_AndDifficultyCurve()
        {
            var report = LevelBatchValidator.Run(new ToyGen(), new ToySolver(), 30,
                i => new DifficultyParams { difficulty = i / 30f });
            Assert.AreEqual(30, report.Count);
            Assert.IsTrue(report.AllSolvable);
            var blocks = report.AverageMovesPerBlock(10);
            Assert.AreEqual(3, blocks.Count);
            Assert.Less(blocks[0], blocks[2], "difficulty should rise");
            StringAssert.StartsWith("index,seed", report.ToCsv());
            StringAssert.Contains("30/30", report.ToMarkdownSummary());
        }

        [Test]
        public void BatchValidator_CapturesGeneratorErrors()
        {
            var report = LevelBatchValidator.Run(new ThrowingGen(), new ToySolver(), 2, _ => new DifficultyParams());
            Assert.AreEqual(0, report.SolvableCount);
            Assert.AreEqual("nope", report.Entries[0].Error);
        }

        sealed class ThrowingGen : ILevelGenerator<Toy>
        {
            public Toy Generate(int seed, DifficultyParams p) => throw new InvalidOperationException("nope");
        }

        [Test]
        public void LocalRemoteConfig_ReturnsOverridesOrFallback()
        {
            var rc = new LocalRemoteConfig().Set("moves", 30).Set("ratio", 0.5f).Set("on", true);
            Assert.AreEqual(30, rc.GetInt("moves", 1));
            Assert.AreEqual(0.5f, rc.GetFloat("ratio", 0f));
            Assert.IsTrue(rc.GetBool("on", false));
            Assert.AreEqual(7, rc.GetInt("missing", 7));
        }
    }
}
