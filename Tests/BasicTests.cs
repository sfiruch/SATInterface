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
            using var m = new Model(); m.Configuration.Verbosity = 0;
            m.Solve();
            Assert.AreEqual(State.Satisfiable, m.State);
        }

        [DataRow(OptimizationFocus.Balanced)]
        [DataRow(OptimizationFocus.Incumbent)]
        [DataRow(OptimizationFocus.Bound)]
        [DataTestMethod]
        public void EmptyMaximizeBalanced(OptimizationFocus _focus)
        {
            using var m = new Model(); m.Configuration.Verbosity = 0;
            m.Configuration.OptimizationFocus = _focus;
            m.Maximize((LinExpr)0);
            Assert.AreEqual(State.Satisfiable, m.State);
        }
    }
}
