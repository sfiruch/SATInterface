using SATInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagicSquare
{
    class Program
    {
        static void Main(string[] args)
        {
            var NUMBERS = Enumerable.Range(1, 7 * 7).ToArray();

            var N = (int)Math.Sqrt(NUMBERS.Length);
            var MAGIC_CONST = NUMBERS.Sum() / N;

            var m = new Model();
            var v = new BoolVar[N, N, NUMBERS.Length];
            for (var y = 0; y < N; y++)
                for (var x = 0; x < N; x++)
                    for (var n = 0; n < NUMBERS.Length; n++)
                        v[x, y, n] = new BoolVar(m, $"{x},{y},{n}");

            var num = new UIntVar[N, N];
            for (var y = 0; y < N; y++)
                for (var x = 0; x < N; x++)
                {
                    num[x, y] = UIntVar.Const(m, NUMBERS[0]) * v[x, y, 0];
                    for (var n = 1; n < NUMBERS.Length; n++)
                        num[x, y] |= UIntVar.Const(m, NUMBERS[n]) * v[x, y, n];

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

            if (m.IsSatisfiable)
                for (var y = 0; y < N; y++)
                {
                    for (var x = 0; x < N; x++)
                        for (var n = 0; n < NUMBERS.Length; n++)
                            if (v[x, y, n].X)
                                Console.Write($"{NUMBERS[n],4}");

                    Console.WriteLine();
                }

            Console.ReadLine();
        }
    }
}
