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
            using var m = new Model();
            m.Solve();
            Assert.AreEqual(State.Satisfiable, m.State);
        }

        [TestMethod]
        public void EmptyMaximizeBalanced()
        {
            using var m = new Model();
            m.Configuration.OptimizationFocus = OptimizationFocus.Balanced;
            m.Maximize((LinExpr)0);
            Assert.AreEqual(State.Satisfiable, m.State);
        }

        [TestMethod]
        public void EmptyMaximizeIncumbent()
        {
            using var m = new Model();
            m.Configuration.OptimizationFocus = OptimizationFocus.Incumbent;
            m.Maximize((LinExpr)0);
            Assert.AreEqual(State.Satisfiable, m.State);
        }

        [TestMethod]
        public void EmptyMaximizeBound()
        {
            using var m = new Model();
            m.Configuration.OptimizationFocus = OptimizationFocus.Bound;
            m.Maximize((LinExpr)0);
            Assert.AreEqual(State.Satisfiable, m.State);
        }
    }
}
