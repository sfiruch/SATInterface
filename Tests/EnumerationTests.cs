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
            const int N = 12;

            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(N);

            var cnt = 0;
            var found = new bool[1 << N];
            m.EnumerateSolutions(v, () =>
            {
                cnt++;

                var x = 0;
                for (var i = 0; i < N; i++)
                    if (v[i].X)
                        x |= (1 << i);

                Assert.IsFalse(found[x]);
                found[x] = true;
            });

            Assert.AreEqual(1 << N, cnt);
            Assert.IsTrue(found.All(v => v));
            Assert.AreEqual(State.Satisfiable, m.State);
        }

        [TestMethod]
        public void RepeatedEnumeration()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(3);

            var cnt = 0;
            for (var i = 0; i < 3; i++)
                m.EnumerateSolutions(v, () =>
                {
                    cnt++;
                });

            Assert.AreEqual(3 * 2 * 2 * 2, cnt);
            Assert.AreEqual(State.Satisfiable, m.State);
        }

        [TestMethod]
        public void BinaryUnsat()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(5);

            m.AddConstr(m.Sum(v) <= 2);
            m.AddConstr(m.Sum(v) >= 3);

            var cnt = 0;
            m.EnumerateSolutions(v, () =>
            {
                cnt++;
            });

            Assert.AreEqual(0, cnt);
            Assert.AreEqual(State.Unsatisfiable, m.State);
        }

        [TestMethod]
        public void BinarySubset()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
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
            Assert.AreEqual(State.Satisfiable, m.State);
        }

        [TestMethod]
        public void BinarySubsetLazyCondition()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(8);

            var cnt = 0;
            m.EnumerateSolutions(v, () =>
            {
                for (var i = 0; i < 4; i++)
                    if (v[i].X)
                    {
                        m.AddConstr(!v[i]);
                        return;
                    }

                cnt++;
            });

            Assert.AreEqual(16, cnt);
            Assert.AreEqual(State.Satisfiable, m.State);
        }

        [TestMethod]
        public void BinarySubsetLazyVars()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(4);

            var cnt = 0;
            m.EnumerateSolutions(v, () =>
            {
                m.AddVars(2);
                cnt++;
            });

            Assert.AreEqual(16, cnt);
            Assert.AreEqual(State.Satisfiable, m.State);
        }
    }
}
