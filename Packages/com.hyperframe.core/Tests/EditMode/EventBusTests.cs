using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace HyperFrame.Core.Tests
{
    public class EventBusTests
    {
        struct Ping { public int Value; }
        struct Other { }

        [Test]
        public void Publish_ReachesSubscribers_OfThatTypeOnly()
        {
            var bus = new EventBus();
            int got = 0, other = 0;
            bus.Subscribe<Ping>(e => got += e.Value);
            bus.Subscribe<Other>(_ => other++);
            bus.Publish(new Ping { Value = 3 });
            Assert.AreEqual(3, got);
            Assert.AreEqual(0, other);
        }

        [Test]
        public void DisposingSubscription_Unsubscribes()
        {
            var bus = new EventBus();
            int count = 0;
            var sub = bus.Subscribe<Ping>(_ => count++);
            sub.Dispose();
            sub.Dispose(); // idempotent
            bus.Publish(new Ping());
            Assert.AreEqual(0, count);
            Assert.AreEqual(0, bus.SubscriberCount<Ping>());
        }

        [Test]
        public void UnsubscribeDuringPublish_IsSafe_AndTakesEffectNextPublish()
        {
            var bus = new EventBus();
            var calls = new List<string>();
            IDisposable second = null;
            bus.Subscribe<Ping>(_ => { calls.Add("a"); second.Dispose(); });
            second = bus.Subscribe<Ping>(_ => calls.Add("b"));
            bus.Publish(new Ping());
            bus.Publish(new Ping());
            CollectionAssert.AreEqual(new[] { "a", "a" }, calls);
        }

        [Test]
        public void SubscribeDuringPublish_IsNotCalledUntilNextPublish()
        {
            var bus = new EventBus();
            int late = 0;
            bus.Subscribe<Ping>(_ => bus.Subscribe<Ping>(__ => late++));
            bus.Publish(new Ping());
            Assert.AreEqual(0, late);
        }

        [Test]
        public void HandlerException_DoesNotStopOtherHandlers()
        {
            var sink = new TestLogSink();
            HFLog.Sink = sink;
            try
            {
                var bus = new EventBus();
                int reached = 0;
                bus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
                bus.Subscribe<Ping>(_ => reached++);
                bus.Publish(new Ping());
                Assert.AreEqual(1, reached);
                Assert.AreEqual(1, sink.Exceptions);
            }
            finally { HFLog.Sink = null; }
        }

        [Test]
        public void EventSubscriptions_DisposeAll()
        {
            var bus = new EventBus();
            var group = new EventSubscriptions();
            group.Add(bus.Subscribe<Ping>(_ => { }));
            group.Add(bus.Subscribe<Other>(_ => { }));
            group.Dispose();
            Assert.AreEqual(0, bus.SubscriberCount<Ping>());
            Assert.AreEqual(0, bus.SubscriberCount<Other>());
        }
    }

    sealed class TestLogSink : ILogSink
    {
        public int Exceptions, Warnings, Errors;
        public void Write(LogLevel level, string tag, string message)
        {
            if (level == LogLevel.Warning) Warnings++;
            if (level == LogLevel.Error) Errors++;
        }
        public void WriteException(Exception exception, string tag) => Exceptions++;
    }
}
