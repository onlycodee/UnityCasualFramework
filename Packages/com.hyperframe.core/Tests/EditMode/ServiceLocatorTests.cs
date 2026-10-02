using System;
using NUnit.Framework;

namespace HyperFrame.Core.Tests
{
    public class ServiceLocatorTests
    {
        interface IFoo { }
        sealed class Foo : IFoo { }
        sealed class DisposableFoo : IFoo, IDisposable { public bool Disposed; public void Dispose() => Disposed = true; }

        [TearDown] public void TearDown() => ServiceLocator.Reset();

        [Test]
        public void Register_ThenGet_ReturnsInstance()
        {
            var foo = new Foo();
            ServiceLocator.Register<IFoo>(foo);
            Assert.AreSame(foo, ServiceLocator.Get<IFoo>());
            Assert.IsTrue(ServiceLocator.Has<IFoo>());
        }

        [Test]
        public void Get_Missing_ThrowsWithServiceName()
        {
            var e = Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<IFoo>());
            StringAssert.Contains("IFoo", e.Message);
        }

        [Test]
        public void Scope_OverridesGlobal_UntilDisposed_AndDisposesItsServices()
        {
            var global = new Foo();
            var scoped = new DisposableFoo();
            ServiceLocator.Register<IFoo>(global);
            var scope = ServiceLocator.CreateScope("level");
            scope.Register<IFoo>(scoped);
            Assert.AreSame(scoped, ServiceLocator.Get<IFoo>());

            scope.Dispose();
            Assert.AreSame(global, ServiceLocator.Get<IFoo>());
            Assert.IsTrue(scoped.Disposed);
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            ServiceLocator.Register<IFoo>(new Foo());
            ServiceLocator.CreateScope("s").Register<IFoo>(new Foo());
            ServiceLocator.Reset();
            Assert.IsFalse(ServiceLocator.Has<IFoo>());
        }
    }
}
