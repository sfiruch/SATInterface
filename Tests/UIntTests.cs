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
        public void UIntGeLe()
        {
            var rng = new Random(123);
            for (var i = 0; i < 1000; i++)
            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });

                var UB = rng.Next(100000) + 1;
                var v = m.AddUIntVar(UB);

                var cLB = rng.Next(UB + 20) - 10;
                m.AddConstr(v >= cLB);

                var cUB = rng.Next(UB + 20) - 10;
                m.AddConstr(v <= cUB);

                m.Solve();

                if (cLB <= cUB && cLB <= UB && cUB >= 0)
                {
                    Assert.AreEqual(State.Satisfiable, m.State,$"UB={UB},{cLB}<=x<={cUB}");
                    Assert.IsTrue(v.X >= cLB, $"UB={UB},{cLB}<=x<={cUB}");
                    Assert.IsTrue(v.X <= cUB, $"UB={UB},{cLB}<=x<={cUB}");
                    Assert.IsTrue(v.X <= UB, $"UB={UB},{cLB}<=x<={cUB}");
                }
                else
                    Assert.AreEqual(State.Unsatisfiable, m.State, $"UB={UB},{cLB}<=x<={cUB}");
            }
        }

        [TestMethod]
        public void UIntLessEqual()
        {
            foreach (var strategy in Enum.GetValues(typeof(OptimizationFocus)).Cast<OptimizationFocus>())
                for (var i = 0; i < 140; i++)
                {
                    using var m = new Model(new Configuration()
                    {
                        Verbosity = 0,
                        OptimizationFocus = strategy
                    });
                    var v = m.AddUIntVar(100);
                    m.AddConstr(v <= i);

                    m.Maximize(v);

                    Assert.AreEqual(State.Satisfiable, m.State);
                    Assert.AreEqual(Math.Min(i, 100), v.X);
                }
        }

        [TestMethod]
        public void UIntSumPerformance()
        {
            using var m = new Model();
            m.SumUInt(Enumerable.Range(0, 40000).Select(i => m.AddVar()).ToArray());
        }

        [TestMethod]
        public void UIntGreaterEqual()
        {
            foreach (var strategy in Enum.GetValues(typeof(OptimizationFocus)).Cast<OptimizationFocus>())
                for (var i = 0; i < 140; i++)
                {
                    using var m = new Model(new Configuration()
                    {
                        Verbosity = 0,
                        OptimizationFocus = strategy
                    });
                    var v = m.AddUIntVar(100);
                    m.AddConstr(v >= i);

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
        public void ConversionFromLinExpr()
        {
            var m = new Model();

            var J0 = m.AddUIntVar(7);
            var J1 = m.AddUIntVar(7);

            m.AddConstr(J0 == 7);

            m.AddConstr(J1 == ((J0 + 1).ToUInt(m) & 0x7));
            m.Solve();

            Assert.AreEqual(State.Satisfiable, m.State);
            Assert.AreEqual(7, J0.X);
            Assert.AreEqual(0, J1.X);
        }

        [TestMethod]
        public void UIntAddBool()
        {
            for (var i = 0; i < 100; i++)
                for (var j = 0; j < 10; j++)
                {
                    using var m = new Model(new Configuration()
                    {
                        Verbosity = 0
                    });
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
                    using var m = new Model(new Configuration()
                    {
                        Verbosity = 0
                    });

                    var v = m.AddUIntConst(0);
                    v += m.AddUIntConst(i);
                    v += m.AddUIntConst(j);
                    m.Solve();
                    Assert.AreEqual(State.Satisfiable, m.State);
                    Assert.AreEqual(i + j, v.X);
                }
        }

        [TestMethod]
        public void SumUInt()
        {
            var rng = new Random(0);
            for (var i = 0; i < 100; i++)
            {
                using var m = new Model(new Configuration()
                {
                    Verbosity = 0
                });

                var sumUBs = 0;

                var vars = new List<UIntVar>();
                for (var j = 0; j < 10; j++)
                {
                    var ub = rng.Next(100);
                    sumUBs += ub;
                    vars.Add(m.AddUIntVar(ub));
                }

                var sumVar = m.Sum(vars);
                m.Maximize(sumVar);

                Assert.AreEqual(State.Satisfiable, m.State);
                Assert.AreEqual(sumUBs, sumVar.X);
            }
        }

        [TestMethod]
        public void SumBools()
        {
            var RNG = new Random(0);
            for (var n = 0; n < 100; n++)
                for (var i = 0; i < 5; i++)
                {
                    using var m = new Model(new Configuration()
                    {
                        Verbosity = 0
                    });
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
