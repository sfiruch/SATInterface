using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Tests
{
    [TestClass]
    public class AbortTests
    {
        [TestMethod]
        public void AbortLazyOptimization()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var vars = m.AddVars(100);
            var obj = m.Sum(vars);

            var abortCalled = false;
            m.Maximize(obj, () =>
            {
                if (abortCalled)
                    Assert.Fail("Optimization continued after calling Model.Abort()");

                Assert.AreEqual(obj.X, vars.Count(v => v.X));

                m.AddConstr(m.And(vars.Select(v => !v)));
                m.Abort();
                abortCalled = true;
            });

            Assert.AreEqual(State.Undecided, m.State);
            Assert.IsTrue(abortCalled);
        }

        [TestMethod]
        public void AbortOptimization()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var vars = m.AddVars(100);
            var obj = m.Sum(vars);

            var abortCalled = false;
            var objVal = BigInteger.MinusOne;
            m.Maximize(obj, () =>
            {
                if (abortCalled)
                    Assert.Fail("Optimization continued after calling Model.Abort()");

                Assert.AreEqual(obj.X, vars.Count(v => v.X));

                objVal = obj.X;
                
                m.Abort();
                abortCalled = true;
            });

            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsTrue(abortCalled);
            Assert.AreEqual(obj.X, objVal);
            Assert.AreEqual(obj.X, vars.Count(v => v.X));
        }

        [TestMethod]
        public void AbortOptimizationLazy()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var vars = m.AddVars(100);
            var obj = m.Sum(vars);

            var abortCalled = false;
            var objVal = BigInteger.Zero;
            m.Maximize(obj, () =>
            {
                if (abortCalled)
                    Assert.Fail("Optimization continued after calling Model.Abort()");

                Assert.AreEqual(obj.X, vars.Count(v => v.X));

                if (obj.X > 90)
                    m.AddConstr(!vars.First(v => v.X));
                else
                    objVal = BigInteger.Max(objVal, obj.X);

                if (obj.X == 90)
                {
                    m.Abort();
                    abortCalled = true;
                }
            });

            Assert.AreEqual(State.Satisfiable,m.State);
            Assert.IsTrue(abortCalled);
            Assert.AreEqual(90, obj.X);
            Assert.AreEqual(90, objVal);
            Assert.AreEqual(90, vars.Count(v => v.X));
        }


        [TestMethod]
        public void AbortOutsideCallbackException()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                m.Abort();
            });
        }

        [TestMethod]
        public void AbortAfterSolveException()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            m.Solve();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                m.Abort();
            });
        }

        [TestMethod]
        public void AbortEnumerationImmediately()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var vars = m.AddVars(4);

            m.EnumerateSolutions(vars, () =>
            {
                m.AddConstr(vars[0]);
                m.Abort();
            });

            Assert.AreEqual(State.Undecided, m.State);
        }

        [TestMethod]
        public void AbortEnumeration()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var vars = m.AddVars(4);

            var cnt = 0;
            m.EnumerateSolutions(vars, () =>
            {
                cnt++;

                if(cnt==4)
                    m.Abort();
                if (cnt > 4)
                    Assert.Fail("Enumeration continued after calling Model.Abort()");
            });

            Assert.AreEqual(4, cnt);
            Assert.AreEqual(State.Satisfiable, m.State);
        }
    }
}
