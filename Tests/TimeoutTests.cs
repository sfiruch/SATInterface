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
        private void BuildAndTestModel(Model m, bool _runOnSeparateThread)
        {
            m.Configuration.Verbosity = 0;
            m.Configuration.TimeLimit = TimeSpan.FromSeconds(1);

            const int holes = 50;
            const int pigeons = 51;
            var assignment = m.AddVars(holes, pigeons);

            for (var h = 0; h < holes; h++)
                m.AddConstr(m.AtMostOneOf(Enumerable.Range(0, pigeons).Select(p => assignment[h, p])));

            for (var p = 0; p < pigeons; p++)
                m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, holes).Select(h => assignment[h, p])));

            var sw = new Stopwatch();
            sw.Start();

            m.Solve();

            Assert.AreEqual(State.Undecided, m.State);
            Assert.IsTrue(sw.ElapsedMilliseconds < 2000);
            Assert.IsTrue(sw.ElapsedMilliseconds > 1000);
        }

        [TestMethod]
        public void MaximizeTimeout()
        {
            using var m = new Model();
            m.Configuration.Verbosity = 0;
            m.Configuration.TimeLimit = TimeSpan.FromSeconds(0.1);

            var cnt = 0;
            var sw = new Stopwatch();
            sw.Start();

            var a = m.AddUIntVar(1000000);
            var b = m.AddUIntVar(1000000);
            var c = m.AddUIntVar(1000000);

            m.AddConstr(a * a + b * b == c * c);

            m.Maximize(c, () =>
            {
                cnt++;
            });

            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsTrue(cnt > 0);
            Assert.IsTrue(sw.ElapsedMilliseconds < 2000);
            Assert.IsTrue(sw.ElapsedMilliseconds > 100);
        }

        [TestMethod]
        public void EnumerateTimeout()
        {
            using var m = new Model();
            m.Configuration.Verbosity = 0;
            m.Configuration.TimeLimit = TimeSpan.FromSeconds(1);

            var cnt = 0;
            var sw = new Stopwatch();
            sw.Start();

            var v = m.AddVars(1000);
            m.EnumerateSolutions(v, () =>
            {
                cnt++;
            });

            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsTrue(cnt > 10);
            Assert.IsTrue(sw.ElapsedMilliseconds < 2000);
            Assert.IsTrue(sw.ElapsedMilliseconds > 1000);
        }



        [DataRow(typeof(CaDiCaL))]
        [DataRow(typeof(Kissat))]
        [DataRow(typeof(CryptoMiniSat))]
        [DataRow(typeof(YalSAT))]
        [DataTestMethod]
        public void InternalTimeout(Type _solver)
        {
            using var m = new Model(new Configuration()
            {
                Solver = (Solver)_solver.GetConstructor(Type.EmptyTypes).Invoke(null)
            });

            BuildAndTestModel(m, false);
        }

        [TestMethod]
        public void kissatWin32PipeTimeout()
        {
            using var m = new Model(new Configuration()
            {
                Solver = new ExternalSolver("kissat.exe")
            });

            BuildAndTestModel(m, true);
        }

        [TestMethod]
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

            BuildAndTestModel(m, true);
        }
    }
}
