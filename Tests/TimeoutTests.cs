using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using SATInterface.Solver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Tests
{
    [TestClass]
    public class TimeoutTests
    {
        private void BuildAndTestModel(Model m)
        {
            m.Configuration.Verbosity = 0;
            m.Configuration.TimeLimit = TimeSpan.FromSeconds(0.2);

            const int holes = 50;
            const int pigeons = 51;
            var assignment = m.AddVars(holes, pigeons);

            for (var h = 0; h < holes; h++)
                m.AddConstr(m.AtMostOneOf(Enumerable.Range(0, pigeons).Select(p => assignment[h, p])));

            for (var p = 0; p < pigeons; p++)
                m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, holes).Select(h => assignment[h, p])));

            var start = Environment.TickCount64;
            m.Solve();
            var elapsed = Environment.TickCount64 - start;

            Assert.AreEqual(State.Undecided, m.State);
            Assert.IsTrue(elapsed >= 190 && elapsed < 2100, $"Completed in {elapsed}ms");
        }

        [TestMethod]
        public void MaximizeTimeout()
        {
            using var m = new Model();
            m.Configuration.Verbosity = 0;
            m.Configuration.TimeLimit = TimeSpan.FromSeconds(0.1);

            var cnt = 0;

            var a = m.AddUIntVar(1000000);
            var b = m.AddUIntVar(1000000);
            var c = m.AddUIntVar(1000000);

            m.AddConstr(a * a + b * b == c * c);

            var start = Environment.TickCount64;
            m.Maximize(c, () =>
            {
                cnt++;
            });
            var elapsed = Environment.TickCount64 - start;

            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsTrue(cnt > 0);
            Assert.IsTrue(elapsed >= 100 && elapsed < 2100, $"Completed in {elapsed}ms");
        }

        [TestMethod]
        public void EnumerateTimeout()
        {
            using var m = new Model();
            m.Configuration.Verbosity = 0;
            m.Configuration.TimeLimit = TimeSpan.FromSeconds(1);

            var cnt = 0;

            var v = m.AddVars(1000);
            var start = Environment.TickCount64;
            m.EnumerateSolutions(v, () =>
            {
                cnt++;
            });
            var elapsed = Environment.TickCount64 - start;

            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsTrue(cnt > 0);
            Assert.IsTrue(elapsed >= 1000 && elapsed < 3000, $"Completed in {elapsed}ms");
        }



        [DataRow(typeof(CaDiCaL))]
        [DataRow(typeof(Kissat))]
        [DataRow(typeof(YalSAT))]
        [DataTestMethod]
        public void InternalTimeout(Type _solver)
        {
            using var m = new Model(new Configuration()
            {
                Solver = (Solver)_solver.GetConstructor(Type.EmptyTypes).Invoke(null)
            });

            BuildAndTestModel(m);
        }

        [TestMethod]
        [Ignore]
        public void kissatWin32PipeTimeout()
        {
            using var m = new Model(new Configuration()
            {
                Solver = new ExternalSolver("kissat.exe")
            });

            BuildAndTestModel(m);
        }

        [TestMethod]
        [Ignore]
        public void kissatWin32FileTimeout()
        {
            var input = Path.GetTempFileName();
            var output = Path.GetTempFileName();
            using var m = new Model(new Configuration()
            {
                Solver = new ExternalSolver(
                    $"cmd.exe",
                    $"/c kissat.exe {input} > {output}",
                    input,
                    output)
            });

            BuildAndTestModel(m);
        }
    }
}
