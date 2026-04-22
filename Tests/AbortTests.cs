using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using SATInterface.Solver;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class AbortTests
    {
        // Time budget for a thread-based abort to actually stop the solver. Has to be
        // generous so we don't flake under heavy parallel load; a regression would hang
        // the solver indefinitely, so any finite bound catches it.
        private static readonly TimeSpan AbortDeadline = TimeSpan.FromSeconds(30);

        // Builds an UNSAT pigeonhole instance: (holes+1) pigeons into `holes` holes.
        // Exponentially hard for CDCL-style solvers, so a well-chosen `holes` is guaranteed
        // not to finish within AbortDeadline on any machine.
        private static void AddPigeonhole(Model m, int holes)
        {
            var p = new BoolExpr[holes + 1, holes];
            for (var i = 0; i <= holes; i++)
                for (var j = 0; j < holes; j++)
                    p[i, j] = m.AddVar();

            for (var i = 0; i <= holes; i++)
                m.AddConstr(m.Or(Enumerable.Range(0, holes).Select(j => p[i, j])));

            for (var j = 0; j < holes; j++)
                for (var i1 = 0; i1 <= holes; i1++)
                    for (var i2 = i1 + 1; i2 <= holes; i2++)
                        m.AddConstr(!p[i1, j] | !p[i2, j]);
        }

        // Spin-calls Abort() while the task is running. Idempotent, so it does not matter
        // whether the first call happens before or after the solver creates its cancellation
        // token — a later call will always land after the token exists. Bounded by a deadline
        // so a broken Abort fails the test instead of hanging CI.
        private static void AbortUntilCompleted(Model m, Task t)
        {
            var deadline = Environment.TickCount64 + (long)AbortDeadline.TotalMilliseconds;
            while (!t.IsCompleted && Environment.TickCount64 < deadline)
            {
                m.Abort();
                Thread.Sleep(10);
            }
            Assert.IsTrue(t.IsCompleted, $"Solver did not abort within {AbortDeadline.TotalSeconds}s");
            t.GetAwaiter().GetResult();
        }

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

            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsTrue(abortCalled);
            Assert.AreEqual(90, obj.X);
            Assert.AreEqual(90, objVal);
            Assert.AreEqual(90, vars.Count(v => v.X));
        }

        [TestMethod]
        public void AbortOutsideSolveDoesNotThrow()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            m.Abort();
            m.Abort();
        }

        [TestMethod]
        public void AbortAfterSolveDoesNotThrow()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            m.Solve();

            m.Abort();
            m.Abort();
        }

        [TestMethod]
        public void AbortFromOtherThreadDoesNotThrow()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            Task.Run(() => m.Abort()).Wait();
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

                if (cnt == 4)
                    m.Abort();
                if (cnt > 4)
                    Assert.Fail("Enumeration continued after calling Model.Abort()");
            });

            Assert.AreEqual(4, cnt);
            Assert.AreEqual(State.Satisfiable, m.State);
        }

        [DataRow(typeof(CaDiCaL))]
        [DataRow(typeof(Kissat))]
        [DataRow(typeof(CryptoMiniSat))]
        [DataTestMethod]
        public void AbortSolveFromOtherThread(Type _solver)
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0,
                Solver = (Solver)_solver.GetConstructor(Type.EmptyTypes)!.Invoke(null)
            });
            AddPigeonhole(m, 14);

            var t = Task.Run(() => m.Solve());
            AbortUntilCompleted(m, t);

            Assert.AreEqual(State.Undecided, m.State);
        }

        [TestMethod]
        public void AbortMaximizeFromOtherThread()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            AddPigeonhole(m, 14);

            // Maximize needs a LinExpr. The pigeonhole portion is UNSAT, so the first
            // feasibility call inside Optimize is what takes forever — exactly the call
            // we want Abort to interrupt.
            var extra = m.AddVars(10);
            var obj = m.Sum(extra);

            var t = Task.Run(() => m.Maximize(obj));
            AbortUntilCompleted(m, t);

            Assert.AreEqual(State.Undecided, m.State);
        }

        [TestMethod]
        public void AbortEnumerationFromOtherThread()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var markers = m.AddVars(4);
            AddPigeonhole(m, 14);

            var t = Task.Run(() => m.EnumerateSolutions(
                markers,
                () => Assert.Fail("UNSAT instance yielded a solution")));
            AbortUntilCompleted(m, t);

            Assert.AreEqual(State.Undecided, m.State);
        }
    }
}
