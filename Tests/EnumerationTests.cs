using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests
{
    [TestClass]
    public class EnumerationTests
    {
        [TestMethod]
        public void BinaryAll()
        {
            using var m = new Model();
            var v = m.AddVars(3);

            var cnt = 0;
            var found = new bool[8];
            m.EnumerateSolutions(v, () =>
            {
                cnt++;
                found[(v[0].X ? 1 : 0) + (v[1].X ? 2 : 0) + (v[2].X ? 4 : 0)] = true;
            });

            Assert.AreEqual(2 * 2 * 2, cnt);
            Assert.IsTrue(found.All(v => v));
        }

        [TestMethod]
        public void BinaryUnsat()
        {
            using var m = new Model();
            var v = m.AddVars(5);

            m.AddConstr(m.Sum(v) <= 2);
            m.AddConstr(m.Sum(v) >= 3);

            var cnt = 0;
            m.EnumerateSolutions(v, () =>
            {
                cnt++;
            });

            Assert.AreEqual(0, cnt);
        }

        [TestMethod]
        public void BinarySubset()
        {
            using var m = new Model();
            var v = m.AddVars(10);

            var cnt = 0;
            var found = new bool[4];
            m.EnumerateSolutions(new[] { v[2], v[4] }, () =>
             {
                 cnt++;
                 found[(v[2].X ? 1 : 0) + (v[4].X ? 2 : 0)] = true;
             });

            Assert.AreEqual(2 * 2, cnt);
            Assert.IsTrue(found.All(v => v));
        }

        [TestMethod]
        public void BinarySubsetLazy()
        {
            using var m = new Model();
            var v = m.AddVars(8);

            var cnt = 0;
            m.EnumerateSolutions(v, () =>
            {
                for (var i = 0; i < 4; i++)
                    if (v[i].X)
                    {
                        m.AddConstr(!v[i].X);
                        return;
                    }

                cnt++;
            });

            Assert.AreEqual(16, cnt);
        }
    }
}
