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
    public class ExternalSolverTests
    {
        private void BuildAndTestModel(Model m)
        {
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

            const int N = 100;
            var vX = m.AddVars(N);
            m.AddConstr(m.Sum(vX.Cast<BoolExpr>()) <= 2);

            var sum = new LinExpr();
            for (var n = 0; n < N; n++)
                sum.AddTerm(vX[n], (n == (N / 2) || n == (N / 2 + 1)) ? 25 : n % 10);

            m.Maximize(sum);
            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.AreEqual(50, sum.X);


            m.AddConstr(vA);
            m.Solve();
            Assert.AreEqual(State.Unsatisfiable, m.State);
        }

        [TestMethod]
        public void kissatInternal()
        {
            using var m = new Model(new Configuration()
            {
                Solver = new Kissat()
            });

            BuildAndTestModel(m);
        }

        [TestMethod]
        public void kissatWin32Pipe()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0,
                Solver = new ExternalSolver("kissat.exe")
            });

            BuildAndTestModel(m);
        }

        [TestMethod]
        public void kissatWSLPipe()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0,
                Solver = new ExternalSolver(
                    $@"{(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "sysnative") : Environment.GetFolderPath(Environment.SpecialFolder.System))}\wsl.exe",
                    "./kissat")
            });

            BuildAndTestModel(m);
        }

        [TestMethod]
        public void kissatWSLFile()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0,
                //m.SolverArguments = $"./plingeling | tee {m.UseTmpOutputFile} | sed '/^v / d'";

                Solver = new ExternalSolver(
                    $@"{(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "sysnative") : Environment.GetFolderPath(Environment.SpecialFolder.System))}\wsl.exe",
                    "./kissat input.cnf | tee solution.sol",
                    "input.cnf",
                    "solution.sol")
            });

            BuildAndTestModel(m);
        }
    }
}
