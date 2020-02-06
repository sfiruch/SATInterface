using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tests
{
    [TestClass]
    public class MaxMazeTests
    {
        public static void MaxMaze(int _n, int _expected)
        {
            var model = new Model();
            var free = model.AddVars(_n, _n);

            free[0, 0] = true;
            free[_n - 1, _n - 1] = true;

            free[_n / 2, _n / 2] = false;

            for (int y = 0; y < _n; y++)
                for (int x = 0; x < _n; x++)
                {
                    BoolExpr a = x > 0 ? free[x - 1, y] : false;
                    BoolExpr b = x < _n - 1 ? free[x + 1, y] : false;
                    BoolExpr c = y > 0 ? free[x, y - 1] : false;
                    BoolExpr d = y < _n - 1 ? free[x, y + 1] : false;

                    if (x == 0 && y == 0)
                        model.AddConstr(b != d);
                    else if (x == _n - 1 && y == _n - 1)
                        model.AddConstr(a != c);
                    else if (!ReferenceEquals(free[x, y], Model.False))
                        model.AddConstr(!free[x, y] | model.ExactlyKOf(new[] { a, b, c, d }, 2));
                }

            //cut: quad
            for (int y = 0; y < _n - 1; y++)
                for (int x = 0; x < _n - 1; x++)
                    model.AddConstr(!free[x, y] | !free[x + 1, y] | !free[x, y + 1] | !free[x + 1, y + 1]);

            var obj = model.Sum(free.Cast<BoolExpr>());
            model.Maximize(obj);

            Assert.IsTrue(model.IsSatisfiable);
            Assert.AreEqual(_expected, obj.X);
        }

        [TestMethod]
        public void Size10()
        {
            MaxMaze(10, 63);
        }

        [TestMethod]
        public void Size9()
        {
            MaxMaze(9, 51);
        }

        [TestMethod]
        public void Size8()
        {
            MaxMaze(8, 39);
        }
    }
}
