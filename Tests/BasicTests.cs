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
            var m = new Model();
            m.Solve();
            Assert.IsTrue(m.IsSatisfiable);
        }

        [TestMethod]
        public void EmptyMaximizeBinary()
        {
            var m = new Model();
            m.Maximize(UIntVar.Const(m, 0), _strategy: Model.OptimizationStrategy.BinarySearch);
            Assert.IsTrue(m.IsSatisfiable);
        }

        [TestMethod]
        public void EmptyMaximizeIncreasing()
        {
            var m = new Model();
            m.Maximize(UIntVar.Const(m, 0), _strategy: Model.OptimizationStrategy.Increasing);
            Assert.IsTrue(m.IsSatisfiable);
        }

        [TestMethod]
        public void EmptyMaximizeDecreasing()
        {
            var m = new Model();
            m.Maximize(UIntVar.Const(m, 0), _strategy: Model.OptimizationStrategy.Decreasing);
            Assert.IsTrue(m.IsSatisfiable);
        }
    }
}
