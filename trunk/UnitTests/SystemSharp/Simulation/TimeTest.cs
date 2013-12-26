using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.Components;

namespace UnitTests.SystemSharp.Simulation
{
    [TestClass]
    public class TimeTest
    {
        class TestComponent : Component
        {
            public bool _ps1called;
            public bool _ps2called;

            private async void Process1()
            {
                await Time.Create(1.0, ETimeUnit.ns);
                Assert.AreEqual(Time.Create(1.0, ETimeUnit.ns), DesignContext.Instance.CurTime);
                await Time.Create(1.5, ETimeUnit.ns);
                Assert.AreEqual(Time.Create(2.5, ETimeUnit.ns), DesignContext.Instance.CurTime);
                _ps1called = true;
                await Time.Infinite;
            }

            private async void Process2()
            {
                await Time.Create(0.8, ETimeUnit.ns);
                Assert.AreEqual(Time.Create(0.8, ETimeUnit.ns), DesignContext.Instance.CurTime);
                await Time.Create(1.0, ETimeUnit.ns);
                Assert.AreEqual(Time.Create(1.8, ETimeUnit.ns), DesignContext.Instance.CurTime);
                _ps2called = true;
                await Time.Infinite;
            }

            protected override void Initialize()
            {
                AddThread(Process1);
                AddThread(Process2);
            }
        }

        [TestMethod]
        public void TestTime2()
        {
            DesignContext.Reset();
            DesignContext.Instance.Resolution = Time.Create(1.0, ETimeUnit.ps);
            var c = new TestComponent();
            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(Time.Create(3.0, ETimeUnit.ns));
            Assert.IsTrue(c._ps1called, "Process1 was not executed.");
            Assert.IsTrue(c._ps2called, "Process2 was not executed.");
        }
    }
}
