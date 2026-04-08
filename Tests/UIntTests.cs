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

        [TestMethod]
        public void LookUpTableBigIntegerConstIndex()
        {
            // Regression: constant index with fewer bits than needed to address the table
            // caused spurious solutions (e.g. index=2 also matched entry 6 in a 10-entry table).
            using var m = new Model(new Configuration { Verbosity = 0 });
            var index = m.AddUIntConst(2);
            var v = m.LookUpTable(index, (Span<System.Numerics.BigInteger>)[0, 100, 200, 300, 400, 500, 600, 700, 800, 1000000]);

            var solutions = new List<System.Numerics.BigInteger>();
            m.EnumerateSolutions(v.Bits, () => solutions.Add(v.X));

            Assert.AreEqual(1, solutions.Count, $"Expected exactly 1 solution but got: {string.Join(", ", solutions)}");
            Assert.AreEqual(new System.Numerics.BigInteger(200), solutions[0]);
        }

        [TestMethod]
        public void LookUpTableBigIntegerVariableIndex()
        {
            // Variable index constrained to each position in turn must yield exactly that entry's value.
            System.Numerics.BigInteger[] table = [0, 100, 200, 300, 400, 500, 600, 700, 800, 1000000];
            for (var expected = 0; expected < table.Length; expected++)
            {
                using var m = new Model(new Configuration { Verbosity = 0 });
                var index = m.AddUIntVar(table.Length - 1);
                var v = m.LookUpTable(index, (Span<System.Numerics.BigInteger>)table);
                m.AddConstr(index == expected);

                var solutions = new List<System.Numerics.BigInteger>();
                m.EnumerateSolutions(v.Bits, () => solutions.Add(v.X));

                Assert.AreEqual(1, solutions.Count, $"index={expected}: got {string.Join(", ", solutions)}");
                Assert.AreEqual(table[expected], solutions[0], $"index={expected}");
            }
        }

        [TestMethod]
        public void LookUpTableUIntVarConstIndex()
        {
            // Same regression as LookUpTableBigIntegerConstIndex but for the Span<UIntVar> overload.
            System.Numerics.BigInteger[] rawTable = [0, 100, 200, 300, 400, 500, 600, 700, 800, 1000000];
            using var m = new Model(new Configuration { Verbosity = 0 });
            var index = m.AddUIntConst(2);
            var tableVars = rawTable.Select(v => m.AddUIntConst(v)).ToArray();
            var result = m.LookUpTable(index, (Span<UIntVar>)tableVars);

            var solutions = new List<System.Numerics.BigInteger>();
            m.EnumerateSolutions(result.Bits, () => solutions.Add(result.X));

            Assert.AreEqual(1, solutions.Count, $"Expected exactly 1 solution but got: {string.Join(", ", solutions)}");
            Assert.AreEqual(new System.Numerics.BigInteger(200), solutions[0]);
        }

        [TestMethod]
        public void LookUpTableUIntVarVariableIndex()
        {
            // Variable index constrained to each position must yield exactly that entry's value (Span<UIntVar> overload).
            System.Numerics.BigInteger[] rawTable = [0, 100, 200, 300, 400, 500, 600, 700, 800, 1000000];
            for (var expected = 0; expected < rawTable.Length; expected++)
            {
                using var m = new Model(new Configuration { Verbosity = 0 });
                var index = m.AddUIntVar(rawTable.Length - 1);
                var tableVars = rawTable.Select(v => m.AddUIntConst(v)).ToArray();
                var result = m.LookUpTable(index, (Span<UIntVar>)tableVars);
                m.AddConstr(index == expected);

                var solutions = new List<System.Numerics.BigInteger>();
                m.EnumerateSolutions(result.Bits, () => solutions.Add(result.X));

                Assert.AreEqual(1, solutions.Count, $"index={expected}: got {string.Join(", ", solutions)}");
                Assert.AreEqual(rawTable[expected], solutions[0], $"index={expected}");
            }
        }
    }
}
