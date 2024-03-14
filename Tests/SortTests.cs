using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tests
{
    [TestClass]
    public class SortTests
    {
        private void SortCase(int _n, int _v, Func<Model, BoolExpr[], BoolExpr[]> _sortFunc)
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var trueCount = 0;

            var v = m.AddVars(_n);
            for (var i = 0; i < _n; i++)
                if (((_v >> i) & 1) != 0)
                {
                    m.AddConstr(v[i]);
                    trueCount++;
                }
                else
                    m.AddConstr(!v[i]);

            var sorted = _sortFunc(m, v);

            m.Solve();

            Assert.AreEqual(State.Satisfiable, m.State);
            for (var i = 0; i < v.Length; i++)
                if (i < trueCount)
                    Assert.IsTrue(sorted[i].X, $"n={_n}, v={_v}");
                else
                    Assert.IsFalse(sorted[i].X, $"n={_n}, v={_v}");
        }

        [TestMethod]
        public void TestSortTotalizer()
        {
            for (var i = 0; i < 127; i++)
                SortCase(7, i, (m, v) => m.SortTotalizer(v));
            for (var i = 0; i < 255; i++)
                SortCase(8, i, (m, v) => m.SortTotalizer(v));
        }

        [TestMethod]
        public void TestSortPairwise()
        {
            for (var i = 0; i < 127; i++)
                SortCase(7, i, (m, v) => m.SortPairwise(v));
            for (var i = 0; i < 255; i++)
                SortCase(8, i, (m, v) => m.SortPairwise(v));
        }



        private void ReverseSortCase(int _n, int _v, Func<Model, BoolExpr[], BoolExpr[]> _sortFunc)
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVars(_n);
            var sorted = _sortFunc(m, v);

            m.AddConstr(sorted[_v]);
            if (_v + 1 < _n)
                m.AddConstr(!sorted[_v + 1]);

            m.Solve();

            Assert.AreEqual(State.Satisfiable, m.State);

            Assert.AreEqual(_v,v.Count(i => i.X));

            for (var i = 0; i < v.Length; i++)
                if (i < _n)
                    Assert.IsTrue(sorted[i].X, $"n={_n}, v={_v}");
                else
                    Assert.IsFalse(sorted[i].X, $"n={_n}, v={_v}");
        }

        [TestMethod]
        public void TestReverseSortTotalizer()
        {
            for (var i = 0; i < 7; i++)
                SortCase(7, i, (m, v) => m.SortTotalizer(v));
            for (var i = 0; i < 8; i++)
                SortCase(8, i, (m, v) => m.SortTotalizer(v));
        }

        [TestMethod]
        public void TestReverseSortPairwise()
        {
            for (var i = 0; i < 7; i++)
                SortCase(7, i, (m, v) => m.SortPairwise(v));
            for (var i = 0; i < 8; i++)
                SortCase(8, i, (m, v) => m.SortPairwise(v));
        }

        //[TestMethod]
        //public void TestSortBitonicSimple()
        //{
        //    SortCase(3, 5, (m, v) => m.SortPairwise(v));
        //}
    }
}
