using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests
{
    [TestClass]
    public class UIntTests
    {
        [TestMethod]
        public void UIntLessEqual()
        {
            foreach (var strategy in Enum.GetValues(typeof(Model.OptimizationStrategy)).Cast<Model.OptimizationStrategy>())
                for (var i = 0; i < 140; i++)
                {
                    var m = new Model();
                    m.LogOutput = false;

                    var v = new UIntVar(m, 100, true);
                    m.AddConstr(v <= i);

                    m.Maximize(v, _strategy: strategy);

                    Assert.IsTrue(m.IsSatisfiable);
                    Assert.AreEqual(Math.Min(i, 100), v.X);
                }
        }

        [TestMethod]
        public void UIntGreaterEqual()
        {
            foreach (var strategy in Enum.GetValues(typeof(Model.OptimizationStrategy)).Cast<Model.OptimizationStrategy>())
                for (var i = 0; i < 140; i++)
                {
                    var m = new Model();
                    m.LogOutput = false;

                    var v = new UIntVar(m, 100, true);
                    m.AddConstr(v >= i);

                    m.Minimize(v, _strategy: strategy);

                    if (i <= 100)
                    {
                        Assert.IsTrue(m.IsSatisfiable);
                        Assert.AreEqual(i, v.X);
                    }
                    else
                        Assert.IsTrue(m.IsUnsatisfiable);
                }
        }

        [TestMethod]
        public void UIntAddBool()
        {
            for (var i = 0; i < 100; i++)
                for (var j = 0; j < 10; j++)
                {
                    var m = new Model();
                    m.LogOutput = false;

                    var v = UIntVar.Const(m, i);
                    for (var k = 0; k < j; k++)
                        v += true;
                    m.Solve();
                    Assert.IsTrue(m.IsSatisfiable);
                    Assert.AreEqual(i + j, v.X);
                }
        }

        [TestMethod]
        public void UIntAddUInt()
        {
            for (var i = 0; i < 20; i++)
                for (var j = 0; j < 20; j++)
                {
                    var m = new Model();
                    m.LogOutput = false;

                    var v = UIntVar.Const(m, 0);
                    v += i;
                    v += j;
                    m.Solve();
                    Assert.IsTrue(m.IsSatisfiable);
                    Assert.AreEqual(i + j, v.X);
                }
        }

        [TestMethod]
        public void SumBools()
        {
            var RNG = new Random(0);
            for (var n = 0; n < 100; n++)
                for (var i = 0; i < 5; i++)
                {
                    var m = new Model();
                    m.LogOutput = false;

                    var v = m.AddVars(n);
                    var values = Enumerable.Range(0, n).Select(i => RNG.Next(2) == 0).ToArray();
                    for (var j = 0; j < n; j++)
                        m.AddConstr(v[j] == values[j]);

                    var sum = m.Sum(v);
                    m.Solve();
                    Assert.IsTrue(m.IsSatisfiable);
                    Assert.AreEqual(values.Count(i => i), sum.X, $"{n} {i} {string.Join("",values)}");
                }
        }
    }
}
