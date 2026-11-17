using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using SATInterface.Solver;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Tests
{
    [TestClass]
    public class RegressionTests
    {
        [DataRow(7)]
        [DataRow(8)]
        [DataRow(9)]
        [TestMethod]
        public void XorWithTrueConstantAndManyVariables(int _n)
        {
            using var m = new Model(new Configuration() { Verbosity = 0 });
            var v = m.AddVars(_n);
            m.AddConstr(Model.Xor([Model.True, .. v]));

            var count = 0;
            m.EnumerateSolutions(v, () =>
            {
                count++;
                Assert.IsFalse(v.Aggregate(false, (r, vi) => r ^ vi.X));
            });
            Assert.AreEqual((1 << _n) / 2, count);
        }

        [TestMethod]
        public void OrWithOperandCachedWhileFlattening()
        {
            using var m = new Model(new Configuration() { Verbosity = 0 });
            var x = m.AddVar();
            var y = m.AddVar();
            var z = m.AddVar();
            var p = x | y;

            //flattening (p & z) caches an OrExpr equal to p, which is equivalent to p
            m.AddConstr((p & z) | p);

            var count = 0;
            m.EnumerateSolutions(new[] { x, y, z }, () =>
            {
                count++;
                Assert.IsTrue(x.X || y.X);
            });
            Assert.AreEqual(6, count);
        }

        [DataRow(4)]
        [DataRow(5)]
        [DataRow(6)]
        [TestMethod]
        public void UIntVarFromBitsEnforcesUpperBound(int _ub)
        {
            using var m = new Model(new Configuration() { Verbosity = 0 });
            var u = m.AddUIntVar(m.AddVars(3), _ub);

            var count = 0;
            m.EnumerateSolutions(new[] { u }, () =>
            {
                count++;
                Assert.IsTrue(u.X <= _ub);
            });
            Assert.AreEqual(_ub + 1, count);
        }

        [TestMethod]
        public void UIntVarOrUpperBound()
        {
            using var m = new Model(new Configuration() { Verbosity = 0 });
            var a = m.AddUIntVar(4);
            var b = m.AddUIntVar(4);
            m.AddConstr((a | b) == 7);
            m.Solve();

            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.AreEqual(7, (int)(a.X | b.X));
        }

        [TestMethod]
        public void UIntVarMultiplyKeepsZeroConstant()
        {
            using var m = new Model(new Configuration() { Verbosity = 0 });
            var allBitsFalse = m.AddUIntVar(3) & 4;
            _ = allBitsFalse * m.AddUIntVar(5);

            Assert.AreEqual(0, (int)m.AddUIntConst(0).UB);
            m.AddConstr(m.AddUIntConst(0) == 5);
            m.Solve();
            Assert.AreEqual(State.Unsatisfiable, m.State);
        }

        [TestMethod]
        public void EnumerateSolutionsWithClausesFromCallback()
        {
            using var m = new Model(new Configuration() { Verbosity = 0 });
            var v = m.AddVars(3);

            //building an expression adds defining clauses, which must not cause duplicates
            var count = 0;
            m.EnumerateSolutions(v, () =>
            {
                count++;
                _ = (v[0] | v[1]).Flatten();
            });
            Assert.AreEqual(8, count);
        }

        [TestMethod]
        public void EnumerateSolutionsWithLazyConstraints()
        {
            using var m = new Model(new Configuration() { Verbosity = 0 });
            var v = m.AddVars(3);

            //reject all solutions with v[0] lazily: they must not be reported as accepted
            var accepted = 0;
            m.EnumerateSolutions(v, () =>
            {
                if (v[0].X)
                    m.AddConstr(!v[0]);
                else
                    accepted++;
            });
            Assert.AreEqual(4, accepted);
            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsFalse(v[0].X);
        }

        [TestMethod]
        public void FalseConstraintInOptimizationCallback()
        {
            using var m = new Model(new Configuration() { Verbosity = 0 });
            var v = m.AddVars(4);

            var calls = 0;
            m.Maximize(m.Sum(v), () =>
            {
                if (++calls == 2)
                    m.AddConstr(Model.False);
            });
            Assert.AreEqual(State.Unsatisfiable, m.State);

            m.Solve();
            Assert.AreEqual(State.Unsatisfiable, m.State);
        }

        [TestMethod]
        public void FalseConstraintInEnumerationCallback()
        {
            using var m = new Model(new Configuration() { Verbosity = 0 });
            var v = m.AddVars(3);

            var calls = 0;
            m.EnumerateSolutions(v, () =>
            {
                if (++calls == 2)
                    m.AddConstr(Model.False);
            });
            Assert.AreEqual(2, calls);
            Assert.AreEqual(State.Unsatisfiable, m.State);

            m.Solve();
            Assert.AreEqual(State.Unsatisfiable, m.State);
        }

        [TestMethod]
        public void ConstantAssumptions()
        {
            using var m = new Model(new Configuration() { Verbosity = 0 });
            var v = m.AddVar();

            m.Solve([Model.False]);
            Assert.AreEqual(State.Unsatisfiable, m.State);

            m.Solve([Model.True, v | !v]);
            Assert.AreEqual(State.Satisfiable, m.State);

            m.Solve([!v, Model.True]);
            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsFalse(v.X);
        }

        [DataRow(1)]
        [DataRow(2)]
        [TestMethod]
        public void InvalidMaxClauseSize(int _maxClauseSize)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Model(new Configuration() { Verbosity = 0, MaxClauseSize = _maxClauseSize }));
        }

        [TestMethod]
        public void SmallestMaxClauseSize()
        {
            using var m = new Model(new Configuration() { Verbosity = 0, MaxClauseSize = 3 });
            var v = m.AddVars(10);
            m.AddConstr(m.Or(v));

            var count = 0;
            m.EnumerateSolutions(v, () => count++);
            Assert.AreEqual((1 << 10) - 1, count);
        }

        [TestMethod]
        public void CryptoMiniSatAssumptionOnUnusedVariable()
        {
            using var m = new Model(new Configuration() { Verbosity = 0, Solver = new CryptoMiniSat() });
            var used = m.AddVar();
            var unused = m.AddVar();
            m.AddConstr(used);

            m.Solve([!unused]);
            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsFalse(unused.X);

            m.Solve([unused]);
            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsTrue(unused.X);
        }

        [TestMethod]
        [Timeout(60000)]
        public void ExternalSolverExitingWithoutReadingInput()
        {
            //enough clauses to fill the pipe buffer, so writing fails once the process exits
            using var m = new Model(new Configuration() { Verbosity = 0, Solver = new ExternalSolver("cmd.exe", "/c exit 0") });
            var v = m.AddVars(1000);
            for (var i = 0; i < v.Length; i++)
                for (var j = 1; j <= 100; j++)
                    m.AddConstr(v[i] | v[(i + j) % v.Length]);

            m.Solve();
            Assert.AreEqual(State.Undecided, m.State);
        }

        [TestMethod]
        [Timeout(60000)]
        public void ExternalSolverMissingOutputFile()
        {
            var output = Path.Combine(Path.GetTempPath(), $"satinterface-missing-{Guid.NewGuid()}.out");
            using var m = new Model(new Configuration() { Verbosity = 0, Solver = new ExternalSolver("cmd.exe", "/c exit 0", null, output) });
            m.AddConstr(m.AddVar());

            Assert.ThrowsException<FileNotFoundException>(() => m.Solve());
        }

        [TestMethod]
        [Timeout(60000)]
        public void ExternalSolverIsKilledAfterTimeLimit()
        {
            var output = Path.Combine(Path.GetTempPath(), $"satinterface-timeout-{Guid.NewGuid()}.out");
            using var m = new Model(new Configuration()
            {
                Verbosity = 0,
                TimeLimit = TimeSpan.FromSeconds(1),
                Solver = new ExternalSolver("cmd.exe", "/c ping -n 30 127.0.0.1 >nul", null, output)
            });
            m.AddConstr(m.AddVar());

            var sw = Stopwatch.StartNew();
            m.Solve();
            Assert.AreEqual(State.Undecided, m.State);
            Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(15), sw.Elapsed.ToString());
        }
    }
}
