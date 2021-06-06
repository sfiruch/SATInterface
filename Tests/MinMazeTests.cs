using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tests
{
    [TestClass]
    public class MinMazeTests
    {
        [DataRow(10, 37)]
        [DataRow(9, 33)]
        [DataRow(8, 29)]
        [DataTestMethod]
        public void MinMaze(int _n, int _expected)
        {
            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });

            var free = m.AddVars(_n, _n);

            free[0, 0] = true;
            free[_n - 1, _n - 1] = true;

            for (var y = 0; y < _n - 1; y++)
                free[1, y] = free[_n - 2, y + 1] = false;

            for (int y = 0; y < _n; y++)
                for (int x = 0; x < _n; x++)
                {
                    BoolExpr a = x > 0 ? free[x - 1, y] : false;
                    BoolExpr b = x < _n - 1 ? free[x + 1, y] : false;
                    BoolExpr c = y > 0 ? free[x, y - 1] : false;
                    BoolExpr d = y < _n - 1 ? free[x, y + 1] : false;

                    if (x == 0 && y == 0)
                        m.AddConstr(b != d);
                    else if (x == _n - 1 && y == _n - 1)
                        m.AddConstr(a != c);
                    else if (!ReferenceEquals(free[x, y], Model.False))
                        m.AddConstr(!free[x, y] | m.ExactlyKOf(new[] { a, b, c, d }, 2));
                }

            //cut: quad
            for (int y = 0; y < _n - 1; y++)
                for (int x = 0; x < _n - 1; x++)
                    m.AddConstr(!free[x, y] | !free[x + 1, y] | !free[x, y + 1] | !free[x + 1, y + 1]);

            var obj = m.Sum(free.Cast<BoolExpr>());
            m.Minimize(obj);

            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.AreEqual(_expected, obj.X);
        }
    }
}
