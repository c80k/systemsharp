using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.DataTypes;

namespace UnitTests.SystemSharp.DataTypes
{
    [TestClass]
    public class UFixTest
    {
        [TestMethod]
        public void TestFromToDouble()
        {
            Assert.AreEqual(0.0, UFix.FromDouble(0.0, 1, 1).DoubleValue, "conversion from/to 0 failed.");
            Assert.AreEqual(1.0, UFix.FromDouble(1.0, 1, 0).DoubleValue, "conversion from/to 1 failed.");
            Assert.AreEqual(1.0, UFix.FromDouble(1.5, 2, 0).DoubleValue, "conversion from/to 1 failed.");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestOverflow1()
        {
            FixedPointSettings.GlobalOverflowMode = EOverflowMode.Fail;
            Assert.AreEqual(EOverflowMode.Fail, FixedPointSettings.GlobalOverflowMode);
            UFix.FromDouble(2.0, 1, 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestOverflow2()
        {
            FixedPointSettings.GlobalOverflowMode = EOverflowMode.Fail;
            Assert.AreEqual(EOverflowMode.Fail, FixedPointSettings.GlobalOverflowMode);
            UFix.FromDouble(-2.5, 1, 0);
        }

        [TestMethod]
        public void TestBasicMath()
        {
            FixedPointSettings.GlobalArithSizingMode = EArithSizingMode.Safe;
            Assert.AreEqual(EArithSizingMode.Safe, FixedPointSettings.GlobalArithSizingMode);

            var v1 = UFix.FromDouble(1.5, 2, 1);
            var v2 = UFix.FromDouble(2.5, 3, 1);
            var v3 = UFix.FromDouble(12.5, 5, 1);

            Assert.AreEqual(4.0, (v1 + v2).DoubleValue);
            Assert.AreEqual(1.0, (v2 - v1).DoubleValue);
            Assert.AreEqual(3.75, (v1 * v2).DoubleValue);
            Assert.AreEqual(0.6, (v1 / v2).DoubleValue, 0.05);
            Assert.AreEqual(1.5, (v1 % v2).DoubleValue);
            Assert.AreEqual(1.0, (v2 % v1).DoubleValue);
            Assert.AreEqual(0.0, (v3 % v2).DoubleValue);
            Assert.AreEqual(2.5, (v2 % v3).DoubleValue);
            Assert.AreEqual(0.5, (v3 % v1).DoubleValue);
            Assert.AreEqual(1.5, (v1 % v3).DoubleValue);
        }
    }
}
