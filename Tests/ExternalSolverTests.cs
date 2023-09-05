using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using SATInterface.Solver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tests
{
    [TestClass]
    [Ignore]
    public class ExternalSolverTests
    {
        private void TestSat(Solver _s)
        {
            using var m = new Model(new Configuration()
            {
                Solver = _s,
                Verbosity = 0
            });

            m.Solve();
            Assert.AreEqual(State.Satisfiable, m.State);

            var vA = m.AddVar();
            var vB = m.AddVar();
            m.AddConstr(!vA);
            m.AddConstr(vB);
            m.Solve();
            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.IsFalse(vA.X);
            Assert.IsTrue(vB.X);
        }

        private void TestUnsat(Solver _s)
        {
            using var m = new Model(new Configuration()
            {
                Solver = _s,
                Verbosity = 0
            });

            var vA = m.AddVar();
            var vB = m.AddVar();
            m.AddConstr(!vA);
            m.AddConstr(vB);
            m.AddConstr(vA);
            m.Solve();
            Assert.AreEqual(State.Unsatisfiable, m.State);
        }

        private void TestMaximize(Solver _s)
        {
            using var m = new Model(new Configuration()
            {
                Solver = _s,
                Verbosity = 0
            });

            m.Solve();

            const int N = 100;
            var vX = m.AddVars(N);
            m.AddConstr(m.Sum(vX.Cast<BoolExpr>()) <= 2);

            m.Solve();

            var sum = new LinExpr();
            for (var n = 0; n < N; n++)
                sum.AddTerm(vX[n], (n == (N / 2) || n == (N / 2 + 1)) ? 25 : n % 10);

            m.Maximize(sum);
            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.AreEqual(50, sum.X);
        }

        [TestMethod]
        public void TestKissatInternal()
        {
            TestSat(new Kissat());
            TestUnsat(new Kissat());
            TestMaximize(new Kissat());
        }

        [TestMethod]
        public void TestKissatWin32Pipe()
        {
            TestSat(new ExternalSolver("kissat.exe"));
            TestUnsat(new ExternalSolver("kissat.exe"));
            TestMaximize(new ExternalSolver("kissat.exe"));
        }

        [TestMethod]
        [Ignore]
        public void TestKissatWSLPipe()
        {
            TestSat(new ExternalSolver(
                    $@"{(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "sysnative") : Environment.GetFolderPath(Environment.SpecialFolder.System))}\wsl.exe",
                    "./kissat"));
            TestUnsat(new ExternalSolver(
                    $@"{(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "sysnative") : Environment.GetFolderPath(Environment.SpecialFolder.System))}\wsl.exe",
                    "./kissat"));
            TestMaximize(new ExternalSolver(
                    $@"{(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "sysnative") : Environment.GetFolderPath(Environment.SpecialFolder.System))}\wsl.exe",
                    "./kissat"));
        }

        [TestMethod]
        [Ignore]
        public void TestKissatWSLFile()
        {
            TestSat(new ExternalSolver(
                    $@"{(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "sysnative") : Environment.GetFolderPath(Environment.SpecialFolder.System))}\wsl.exe",
                    "./kissat input.cnf | tee solution.sol",
                    "input.cnf",
                    "solution.sol"));
            TestUnsat(new ExternalSolver(
                    $@"{(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "sysnative") : Environment.GetFolderPath(Environment.SpecialFolder.System))}\wsl.exe",
                    "./kissat input.cnf | tee solution.sol",
                    "input.cnf",
                    "solution.sol"));
            TestMaximize(new ExternalSolver(
                    $@"{(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "sysnative") : Environment.GetFolderPath(Environment.SpecialFolder.System))}\wsl.exe",
                    "./kissat input.cnf | tee solution.sol",
                    "input.cnf",
                    "solution.sol"));
        }

        [TestMethod]
        public void TestKissatWin32File()
        {
            var input = Path.GetTempFileName();
            var output = Path.GetTempFileName();

            TestSat(new ExternalSolver(
                    $"cmd.exe",
                    $"/c kissat.exe {input} > {output}",
                    input,
                    output));
            TestUnsat(new ExternalSolver(
                    $"cmd.exe",
                    $"/c kissat.exe {input} > {output}",
                    input,
                    output));
            TestMaximize(new ExternalSolver(
                    $"cmd.exe",
                    $"/c kissat.exe {input} > {output}",
                    input,
                    output));
        }
    }
}
