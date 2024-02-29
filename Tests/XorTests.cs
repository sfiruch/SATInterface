using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tests
{
    [TestClass]
    public class XorTests
    {
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(6)]
        [DataRow(7)]
        [DataRow(8)]
        [DataRow(9)]
        [DataRow(10)]
        [DataRow(11)]
        [DataRow(12)]
        [DataRow(13)]
        [TestMethod]
        public void XorPositive(int _n)
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVars(_n);
            m.AddConstr(m.Xor(v));

            var count = 0;
            m.EnumerateSolutions(v, () =>
            {
                count++;

                var r = false;
                foreach (var vi in v)
                    r ^= vi.X;
                Assert.IsTrue(r);
            });
            Assert.AreEqual((1 << _n) / 2, count);
        }

        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(6)]
        [DataRow(7)]
        [DataRow(8)]
        [DataRow(9)]
        [DataRow(10)]
        [DataRow(11)]
        [DataRow(12)]
        [DataRow(13)]
        [TestMethod]
        public void XorNegative(int _n)
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVars(_n);
            m.AddConstr(!m.Xor(v));

            var count = 0;
            m.EnumerateSolutions(v, () =>
            {
                count++;

                var r = false;
                foreach (var vi in v)
                    r ^= vi.X;
                Assert.IsFalse(r);
            });
            Assert.AreEqual(((1 << _n)+1) / 2, count);
        }
    }
}
