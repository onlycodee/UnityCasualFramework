using NUnit.Framework;

namespace HyperFrame.Core.Tests
{
    public class ObservableTests
    {
        [Test]
        public void Changed_FiresOnlyOnRealChange()
        {
            var o = new Observable<int>(1);
            int calls = 0;
            o.Changed += _ => calls++;
            o.Value = 1;
            o.Value = 2;
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Bind_CallsImmediately_AndStopsAfterDispose()
        {
            var o = new Observable<string>("a");
            string seen = null;
            var binding = o.Bind(v => seen = v);
            Assert.AreEqual("a", seen);
            binding.Dispose();
            o.Value = "b";
            Assert.AreEqual("a", seen);
        }
    }
}
