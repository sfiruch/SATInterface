using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    [TestClass]
    public class BasicTests
    {
        [TestMethod]
        public void EmptySolve()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            m.Solve();
            Assert.AreEqual(State.Satisfiable, m.State);
        }

        [TestMethod]
        public void Assumptions()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVar();

            m.Solve(new[] { v });
            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsTrue(v.X);

            m.Solve(new[] { !v });
            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsFalse(v.X);
        }


        [TestMethod]
        public void RepeatedSolves()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            m.Solve();
            m.AddVar();
            m.Solve();
            var v2 = m.AddVar();
            m.Solve();
            m.AddConstr(v2);
            m.Solve();
            m.Solve();
        }

        [DataRow(OptimizationFocus.Balanced)]
        [DataRow(OptimizationFocus.Incumbent)]
        [DataRow(OptimizationFocus.Bound)]
        [DataTestMethod]
        public void EmptyMaximizeBalanced(OptimizationFocus _focus)
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0,
                OptimizationFocus = _focus
            });
            m.Maximize((LinExpr)0);
            Assert.AreEqual(State.Satisfiable, m.State);
        }
    }
}
