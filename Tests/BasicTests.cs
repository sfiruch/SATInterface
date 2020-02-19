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
            Assert.IsTrue(m.IsSatisfiable);
        }

        [TestMethod]
        public void EmptyMaximizeBinary()
        {
            using var m = new Model();
            m.Configuration.OptimizationStrategy = OptimizationStrategy.BinarySearch;
            m.Maximize((LinExpr)0);
            Assert.IsTrue(m.IsSatisfiable);
        }

        [TestMethod]
        public void EmptyMaximizeIncreasing()
        {
            using var m = new Model();
            m.Configuration.OptimizationStrategy = OptimizationStrategy.Increasing;
            m.Maximize((LinExpr)0);
            Assert.IsTrue(m.IsSatisfiable);
        }

        [TestMethod]
        public void EmptyMaximizeDecreasing()
        {
            using var m = new Model();
            m.Configuration.OptimizationStrategy = OptimizationStrategy.Decreasing;
            m.Maximize((LinExpr)0);
            Assert.IsTrue(m.IsSatisfiable);
        }
    }
}
