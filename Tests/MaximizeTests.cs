using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SATInterface.Model;

namespace Tests
{
    [TestClass]
    public class MaximizeTests
    {
        void RunTest(int xLB, int xUB, int yLB, int yUB, int xLimit, int yLimit, int xWeight, int yWeight, OptimizationStrategy _strategy)
        {
            var m = new Model();
            m.LogOutput = false;

            var x = new UIntVar(m, xUB);
            var y = new UIntVar(m, yUB);
            var c = new UIntVar(m, checked(xUB * yUB));

            m.AddConstr(x >= xLB);
            m.AddConstr(y >= yLB);

            m.AddConstr((x < xLimit) | (y < yLimit));
            m.AddConstr(c == x * y);

            m.Maximize(x.ToLinExpr() * xWeight + yWeight * y.ToLinExpr());

            var best = (Val: 0, X: 0, Y: 0);
            for (var xt = xLB; xt <= xUB; xt++)
                for (var yt = yLB; yt <= yUB; yt++)
                    if ((xt < xLimit) || (yt < yLimit))
                    {
                        var val = checked(xt * xWeight + yWeight * yt);
                        if (val > best.Val)
                            best = (Val: val, X: xt, Y: yt);
                    }

            Assert.AreEqual(checked(x.X * y.X), c.X);
            Assert.AreEqual(checked(best.X * xWeight + best.Y * yWeight), checked(x.X * xWeight + y.X * yWeight));
        }

        [TestMethod]
        public void Example()
        {
            RunTest(0, 1000, 0, 200, 512, 100, 1, 7, OptimizationStrategy.BinarySearch);
        }

        [TestMethod]
        public void RandomBinary()
        {
            RunRandom(10, OptimizationStrategy.BinarySearch);
        }

        [TestMethod]
        public void RandomDecreasing()
        {
            RunRandom(10, OptimizationStrategy.Decreasing);
        }

        [TestMethod]
        public void RandomIncreasing()
        {
            RunRandom(10, OptimizationStrategy.Increasing);
        }

        public void RunRandom(int _iterations, OptimizationStrategy _strategy)
        {
            var RNG = new Random(0);
            for (var i = 0; i < _iterations; i++)
            {
                var xLB = RNG.Next(0, 100);
                var xUB = RNG.Next(xLB, 10000);
                var yLB = RNG.Next(0, 100);
                var yUB = RNG.Next(yLB, 10000);

                var xLimit = RNG.Next(xLB - 10, xUB + 10);
                var yLimit = RNG.Next(yLB - 10, yUB + 10);

                var xWeight = RNG.Next(0, 20);
                var yWeight = RNG.Next(0, 20);

                RunTest(xLB, xUB, yLB, yUB, xLimit, yLimit, xWeight, yWeight, _strategy);
            }
        }

        [TestMethod]
        public void UnsatAtEnd()
        {
            var m = new Model();
            m.LogOutput = false;

            var a = m.AddVar();
            var b = m.AddVar();
            m.AddConstr(a);
            m.AddConstr(!b);
            m.AddConstr(a==b);

            var v = new UIntVar(m, 10, true);
            m.Maximize(v);

            Assert.IsFalse(m.IsSatisfiable);
            Assert.IsTrue(m.IsUnsatisfiable);
        }

        [TestMethod]
        public void SatAtEnd()
        {
            var m = new Model();
            m.LogOutput = false;

            var v = new UIntVar(m, 10, true);
            m.Maximize(v);

            Assert.IsTrue(m.IsSatisfiable);
            Assert.IsFalse(m.IsUnsatisfiable);
        }
    }
}
