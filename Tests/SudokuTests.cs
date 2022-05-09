using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class SudokuTests
    {
        private void VerifySudoku(BoolExpr[,,] _v)
        {
            var r = new int[9, 9];
            for (var y = 0; y < 9; y++)
                for (var x = 0; x < 9; x++)
                    for (var n = 0; n < 9; n++)
                        if (_v[x, y, n].X)
                        {
                            Assert.AreEqual(0, r[x, y]);
                            r[x, y] = n + 1;
                        }

            for (var y = 0; y < 9; y++)
                Assert.AreEqual(1 + 2 + 3 + 4 + 5 + 6 + 7 + 8 + 9, Enumerable.Range(0, 9).Sum(x => r[x, y]));

            for (var x = 0; x < 9; x++)
                Assert.AreEqual(1 + 2 + 3 + 4 + 5 + 6 + 7 + 8 + 9, Enumerable.Range(0, 9).Sum(y => r[x, y]));

            for (var y = 0; y < 9; y += 3)
                for (var x = 0; x < 9; x += 3)
                    Assert.AreEqual(1 + 2 + 3 + 4 + 5 + 6 + 7 + 8 + 9,
                        r[x + 0, y + 0] + r[x + 1, y + 0] + r[x + 2, y + 0] +
                        r[x + 0, y + 1] + r[x + 1, y + 1] + r[x + 2, y + 1] +
                        r[x + 0, y + 2] + r[x + 1, y + 2] + r[x + 2, y + 2]);
        }

        [TestMethod]
        public void SimpleSudoku()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var v = m.AddVars(9, 9, 9);

            //instead of a variable, use the constant "True" for first number 1
            v[0, 0, 0] = true;

            //here's alternative way to set the second number
            m.AddConstr(v[1, 0, 1]);

            //assign one number to each cell
            for (var y = 0; y < 9; y++)
                for (var x = 0; x < 9; x++)
                    m.AddConstr(m.Sum(Enumerable.Range(0, 9).Select(n => v[x, y, n])) == 1);

            //each number occurs once per row (alternative formulation)
            for (var y = 0; y < 9; y++)
                for (var n = 0; n < 9; n++)
                    m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, 9).Select(x => v[x, y, n])));

            //each number occurs once per column (configured formulation)
            for (var x = 0; x < 9; x++)
                for (var n = 0; n < 9; n++)
                    m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, 9).Select(y => v[x, y, n]), Model.ExactlyOneOfMethod.PairwiseTree));

            //each number occurs once per 3x3 block
            for (var n = 0; n < 9; n++)
                for (var y = 0; y < 9; y += 3)
                    for (var x = 0; x < 9; x += 3)
                        m.AddConstr(m.Sum(
                            v[x + 0, y + 0, n], v[x + 1, y + 0, n], v[x + 2, y + 0, n],
                            v[x + 0, y + 1, n], v[x + 1, y + 1, n], v[x + 2, y + 1, n],
                            v[x + 0, y + 2, n], v[x + 1, y + 2, n], v[x + 2, y + 2, n]) == 1);

            m.Solve();

            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.AreEqual(true, v[0, 0, 0].X);
            Assert.AreEqual(true, v[1, 0, 1].X);
            VerifySudoku(v);
        }

        [TestMethod]
        public void WorldsHardestSudoku()
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(9, 9, 9);

            //According to http://www.telegraph.co.uk/news/science/science-news/9359579/Worlds-hardest-sudoku-can-you-crack-it.html
            //this is the "World's hardest Sudoku"...
            var sudoku =
                "8........" +
                "..36....." +
                ".7..9.2.." +
                ".5...7..." +
                "....457.." +
                "...1...3." +
                "..1....68" +
                "..85...1." +
                ".9....4..";
            for (var y = 0; y < 9; y++)
                for (var x = 0; x < 9; x++)
                    if (sudoku[y * 9 + x] != '.')
                        v[x, y, sudoku[y * 9 + x] - '1'] = true;

            //assign one number to each cell
            for (var y = 0; y < 9; y++)
                for (var x = 0; x < 9; x++)
                    m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, 9).Select(n => v[x, y, n])));

            //each number occurs once per row
            for (var y = 0; y < 9; y++)
                for (var n = 0; n < 9; n++)
                    m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, 9).Select(x => v[x, y, n])));

            //each number occurs once per column
            for (var x = 0; x < 9; x++)
                for (var n = 0; n < 9; n++)
                    m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, 9).Select(y => v[x, y, n])));

            //each number occurs once per 3x3 block
            for (var n = 0; n < 9; n++)
                for (var y = 0; y < 9; y += 3)
                    for (var x = 0; x < 9; x += 3)
                        m.AddConstr(m.ExactlyOneOf(v[x + 0, y + 0, n], v[x + 1, y + 0, n], v[x + 2, y + 0, n],
                            v[x + 0, y + 1, n], v[x + 1, y + 1, n], v[x + 2, y + 1, n],
                            v[x + 0, y + 2, n], v[x + 1, y + 2, n], v[x + 2, y + 2, n]));

            m.Solve();

            Assert.AreEqual(State.Satisfiable, m.State);
            VerifySudoku(v);
        }
    }
}
