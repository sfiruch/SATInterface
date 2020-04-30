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
            foreach (var strategy in Enum.GetValues(typeof(OptimizationFocus)).Cast<OptimizationFocus>())
                for (var i = 0; i < 140; i++)
                {
                    using var m = new Model();
                    var v = m.AddUIntVar(100, true);
                    m.AddConstr(v <= i);

                    m.Configuration.OptimizationFocus = strategy;
                    m.Maximize(v);

                    Assert.AreEqual(State.Satisfiable, m.State);
                    Assert.AreEqual(Math.Min(i, 100), v.X);
                }
        }

        [TestMethod]
        public void UIntGreaterEqual()
        {
            foreach (var strategy in Enum.GetValues(typeof(OptimizationFocus)).Cast<OptimizationFocus>())
                for (var i = 0; i < 140; i++)
                {
                    using var m = new Model();
                    var v = m.AddUIntVar(100, true);
                    m.AddConstr(v >= i);

                    m.Configuration.OptimizationFocus = strategy;
                    m.Minimize(v);

                    if (i <= 100)
                    {
                        Assert.AreEqual(State.Satisfiable, m.State);
                        Assert.AreEqual(i, v.X);
                    }
                    else
                        Assert.AreEqual(State.Unsatisfiable, m.State);
                }
        }

        [TestMethod]
        public void UIntAddBool()
        {
            for (var i = 0; i < 100; i++)
                for (var j = 0; j < 10; j++)
                {
                    using var m = new Model();
                    var v = m.AddUIntConst(i);
                    for (var k = 0; k < j; k++)
                        v += true;
                    m.Solve();
                    Assert.AreEqual(State.Satisfiable, m.State);
                    Assert.AreEqual(i + j, v.X);
                }
        }

        [TestMethod]
        public void UIntAddUInt()
        {
            for (var i = 0; i < 20; i++)
                for (var j = 0; j < 20; j++)
                {
                    using var m = new Model();
                    m.Configuration.Verbosity = 0;

                    var v = m.AddUIntConst(0);
                    v += i;
                    v += j;
                    m.Solve();
                    Assert.AreEqual(State.Satisfiable, m.State);
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
                    using var m = new Model();
                    var v = m.AddVars(n);
                    var values = Enumerable.Range(0, n).Select(i => RNG.Next(2) == 0).ToArray();
                    for (var j = 0; j < n; j++)
                        m.AddConstr(v[j] == values[j]);

                    var sum = m.SumUInt(v);
                    m.Solve();
                    Assert.AreEqual(State.Satisfiable, m.State);
                    Assert.AreEqual(values.Count(i => i), sum.X, $"{n} {i} {string.Join("", values)}");
                }
        }
    }
}
