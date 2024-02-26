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
        public void AndShortCircuit1()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVar();
            var r = v & true;
            Assert.AreEqual(v, r);
        }

        [TestMethod]
        public void OrShortCircuit1()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVar();
            var r = v | false;
            Assert.AreEqual(v, r);
        }

        [TestMethod]
        public void AndShortCircuit3()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVar();
            var r = v & false;
            Assert.AreEqual(Model.False, r);
        }

        [TestMethod]
        public void OrShortCircuit3()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVar();
            var r = v | true;
            Assert.AreEqual(Model.True, r);
        }


        [TestMethod]
        public void AndShortCircuit2()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVar();
            var r = v & !v;
            Assert.AreEqual(Model.False, r);
        }

        [TestMethod]
        public void AndHierarchyShortCircuit()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVars(2);
            var r1 = v[0] & v[1];
            var r2 = r1 & !v[0];
            Assert.AreEqual(Model.False, r2);
        }

        [TestMethod]
        public void OrShortCircuit2()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVar();
            var r = v | !v;
            Assert.AreEqual(Model.True, r);
        }

        [TestMethod]
        public void OrHierarchyShortCircuit()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVars(2);
            var r1 = v[0] | v[1];
            var r2 = r1 | !v[0];
            Assert.AreEqual(Model.True, r2);
        }

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

        [DataRow(OptimizationFocus.Bisection)]
        [DataRow(OptimizationFocus.Binary)]
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
            m.Maximize(new LinExpr());
            Assert.AreEqual(State.Satisfiable, m.State);
        }
    }
}
