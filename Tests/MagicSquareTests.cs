using Microsoft.VisualStudio.TestTools.UnitTesting;
using SATInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class MagicSquareTests
    {
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(6)]
        [DataTestMethod]
        public void MagicSquare(int size)
        {
            var NUMBERS = Enumerable.Range(1, size * size).ToArray();

            var N = (int)Math.Sqrt(NUMBERS.Length);
            var MAGIC_CONST = NUMBERS.Sum() / N;

            using var m = new Model(new Configuration()
            {
                Verbosity = 0
            });
            var v = m.AddVars(N, N, NUMBERS.Length);

            var num = new UIntVar[N, N];
            for (var y = 0; y < N; y++)
                for (var x = 0; x < N; x++)
                {
                    num[x, y] = m.AddUIntConst(NUMBERS[0]) * v[x, y, 0];
                    for (var n = 1; n < NUMBERS.Length; n++)
                        num[x, y] |= m.AddUIntConst(NUMBERS[n]) * v[x, y, n];

                    num[x, y] = num[x, y].Flatten();
                }

            //assign one number to each cell
            for (var y = 0; y < N; y++)
                for (var x = 0; x < N; x++)
                    m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, NUMBERS.Length).Select(n => v[x, y, n])));

            //use each number once
            for (var n = 0; n < NUMBERS.Length; n++)
                m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, N).SelectMany(y => Enumerable.Range(0, N).Select(x => v[x, y, n]))));

            //columns must sum to MAGIC_CONST
            for (var y = 0; y < N; y++)
                m.AddConstr(m.Sum(Enumerable.Range(0, N).Select(x => num[x, y])) == MAGIC_CONST);

            //rows must sum to MAGIC_CONST
            for (var x = 0; x < N; x++)
                m.AddConstr(m.Sum(Enumerable.Range(0, N).Select(y => num[x, y])) == MAGIC_CONST);

            //diagonals must sum to MAGIC_CONST
            m.AddConstr(m.Sum(Enumerable.Range(0, N).Select(i => num[i, i])) == MAGIC_CONST);
            m.AddConstr(m.Sum(Enumerable.Range(0, N).Select(i => num[N - 1 - i, i])) == MAGIC_CONST);

            //enforce frénicle standard form
            for (var n1 = 0; n1 < NUMBERS.Length; n1++)
                for (var n2 = n1 + 1; n2 < NUMBERS.Length; n2++)
                {
                    m.AddConstr(!(v[0, 0, n2] & v[N - 1, 0, n1]));
                    m.AddConstr(!(v[0, 0, n2] & v[0, N - 1, n1]));
                    m.AddConstr(!(v[0, 0, n2] & v[N - 1, N - 1, n1]));
                    m.AddConstr(!(v[1, 0, n2] & v[0, 1, n1]));
                }

            m.Solve();

            Assert.AreEqual(State.Satisfiable, m.State);

            for (var y = 0; y < N; y++)
            {
                var sum = 0;
                for (var x = 0; x < N; x++)
                {
                    var cnt = 0;
                    for (var n = 0; n < NUMBERS.Length; n++)
                        if (v[x, y, n].X)
                        {
                            sum += NUMBERS[n];
                            cnt++;
                        }

                    Assert.AreEqual(1, cnt);
                }

                Assert.AreEqual(MAGIC_CONST, sum);
            }

            for (var x = 0; x < N; x++)
            {
                var sum = 0;
                for (var y = 0; y < N; y++)
                    for (var n = 0; n < NUMBERS.Length; n++)
                        if (v[x, y, n].X)
                            sum += NUMBERS[n];

                Assert.AreEqual(MAGIC_CONST, sum);
            }

            {
                var sum = 0;
                for (var x = 0; x < N; x++)
                    for (var n = 0; n < NUMBERS.Length; n++)
                        if (v[x, x, n].X)
                            sum += NUMBERS[n];

                Assert.AreEqual(MAGIC_CONST, sum);
            }

            {
                var sum = 0;
                for (var x = 0; x < N; x++)
                    for (var n = 0; n < NUMBERS.Length; n++)
                        if (v[N - 1 - x, x, n].X)
                            sum += NUMBERS[n];

                Assert.AreEqual(MAGIC_CONST, sum);
            }
        }
    }
}
