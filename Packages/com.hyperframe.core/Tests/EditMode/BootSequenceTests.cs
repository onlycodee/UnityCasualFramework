using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace HyperFrame.Core.Tests
{
    public class BootSequenceTests
    {
        [SetUp] public void SetUp() => HFLog.Sink = new TestLogSink();
        [TearDown] public void TearDown() => HFLog.Sink = null;

        // Run off the main thread: blocking on a task that awaits Unity's SynchronizationContext would deadlock.
        static BootReport Run(BootSequence boot) => Task.Run(() => boot.RunAsync()).GetAwaiter().GetResult();

        [Test]
        public void RunsStepsInDeclaredOrder()
        {
            var order = new List<string>();
            var boot = new BootSequence()
                .Add("a", () => order.Add("a"))
                .Add("b", async ct => { await Task.Yield(); order.Add("b"); })
                .Add("c", () => order.Add("c"));
            var report = Run(boot);
            Assert.IsTrue(report.Success);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, order);
            Assert.AreEqual(3, report.Steps.Count);
        }

        [Test]
        public void OptionalFailure_IsReported_AndBootContinues()
        {
            bool ranAfter = false;
            var boot = new BootSequence()
                .Add("analytics", () => throw new Exception("offline"), critical: false)
                .Add("after", () => ranAfter = true);
            var report = Run(boot);
            Assert.IsTrue(report.Success);
            Assert.IsTrue(ranAfter);
            Assert.AreEqual(BootStepStatus.Failed, report.Steps[0].Status);
        }

        [Test]
        public void CriticalFailure_StopsBoot_AndSkipsTheRest()
        {
            var boot = new BootSequence()
                .Add("save", () => throw new Exception("disk"))
                .Add("ui", () => { });
            var report = Run(boot);
            Assert.IsFalse(report.Success);
            Assert.AreEqual(BootStepStatus.Failed, report.Steps[0].Status);
            Assert.AreEqual(BootStepStatus.Skipped, report.Steps[1].Status);
        }

        [Test]
        public void SlowStep_TimesOut()
        {
            var boot = new BootSequence()
                .Add("slow", ct => Task.Delay(5000, ct), critical: false, timeout: TimeSpan.FromMilliseconds(50))
                .Add("next", () => { });
            var report = Run(boot);
            Assert.AreEqual(BootStepStatus.TimedOut, report.Steps[0].Status);
            Assert.AreEqual(BootStepStatus.Succeeded, report.Steps[1].Status);
            Assert.Less(report.TotalMs, 4000);
        }
    }
}
