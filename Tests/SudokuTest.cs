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
    public class SudokuTest
    {
        [TestMethod]
        public void WorldsHardestSudoku()
        {
            var m = new Model();
            m.LogOutput = false;

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

            Assert.IsTrue(m.IsSatisfiable);

            var block = new bool[3, 3][];
            for (var y = 0; y < 3; y++)
                for (var x = 0; x < 3; x++)
                    block[x, y] = new bool[9];

            for (var y = 0; y < 9; y++)
            {
                var row = new bool[9];
                for (var x = 0; x < 9; x++)
                {
                    var cnt = 0;
                    for (var n = 0; n < 9; n++)
                        if (v[x, y, n].X)
                        {
                            cnt++;
                            row[n] = true;
                            block[x / 3, y / 3][n] = true;
                        }
                    Assert.AreEqual(1, cnt);
                }
                Assert.AreEqual(9, row.Count(v => v));
            }

            for (var y = 0; y < 3; y++)
                for (var x = 0; x < 3; x++)
                    Assert.AreEqual(9, block[x, y].Count(v => v));

            for (var y = 0; y < 9; y++)
            {
                var col = new bool[9];
                for (var x = 0; x < 9; x++)
                    for (var n = 0; n < 9; n++)
                        if (v[x, y, n].X)
                            col[n] = true;

                Assert.AreEqual(9, col.Count(v => v));
            }
        }
    }
}
