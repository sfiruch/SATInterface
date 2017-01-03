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
        static int[] NUMBERS = Enumerable.Range(1, 5 * 5).ToArray();
        //static int[] NUMBERS = new[] { 5, 9, 15, 23, 46, 52, 55, 67, 72, 75, 78, 83, 85, 89, 90, 94, 100, 103, 106, 109, 115, 119, 123, 127, 130 }; //396
        //static int[] NUMBERS = new[] { 75, 130, 5, 90, 94, 123, 15, 119, 52, 85, 9, 127, 109, 46, 103, 115, 67, 83, 106, 23, 72, 55, 78, 100, 89 };
        /*static int[] NUMBERS = new[] {  12,  16, 16,  20,  32,  32,  32,
                                        64,  70,  72,  72,  92,  108,  112,
                                        120,  132,  142,  142,  144,  156,  156,
                                        168,  168,  168,  180,  198,  200,  206,
                                        218,  224,  226,  236,  244,  276,  288,
                                        290,  297,  316,  332,  360,  368,  370,
                                        376,  392,  480,  498,  520,  537,  930 };*/

        static int N = (int)Math.Sqrt(NUMBERS.Length);
        static int CONST = NUMBERS.Sum() / N;

        static void Main(string[] args)
        {
            var m = new Model();
            var v = new BoolVar[N, N, NUMBERS.Length];
            for (var y = 0; y < N; y++)
                for (var x = 0; x < N; x++)
                    for (var n = 0; n < NUMBERS.Length; n++)
                        v[x, y, n] = new BoolVar(m, $"{x},{y},{n}");

            for (var y = 0; y < N; y++)
                for (var x = 0; x < N; x++)
                    m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, NUMBERS.Length).Select(n => v[x, y, n])));

            for (var n = 0; n < NUMBERS.Length; n++)
                m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, N).SelectMany(y => Enumerable.Range(0, N).Select(x => v[x, y, n]))));

            //frénicle standard form
            for (var n1 = 0; n1 < NUMBERS.Length; n1++)
                for (var n2 = n1 + 1; n2 < NUMBERS.Length; n2++)
                {
                    m.AddConstr(!(v[0, 0, n2] & v[N - 1, 0, n1]));
                    m.AddConstr(!(v[0, 0, n2] & v[0, N - 1, n1]));
                    m.AddConstr(!(v[0, 0, n2] & v[N - 1, N - 1, n1]));

                    m.AddConstr(!(v[1, 0, n2] & v[0, 1, n1]));
                }

            var num = new UIntVar[N, N];
            for (var y = 0; y < N; y++)
                for (var x = 0; x < N; x++)
                {
                    num[x, y] = UIntVar.Const(m, NUMBERS[0] - NUMBERS.Min()) * v[x, y, 0];
                    for (var n = 1; n < NUMBERS.Length; n++)
                        num[x, y] |= UIntVar.Const(m, NUMBERS[n] - NUMBERS.Min()) * v[x, y, n];

                    num[x, y] = num[x, y].Flatten();
                }

            for (var y = 0; y < N; y++)
                m.AddConstr(m.Sum(Enumerable.Range(0, N).Select(x => num[x, y])) == CONST - NUMBERS.Min() * N);

            for (var x = 0; x < N; x++)
                m.AddConstr(m.Sum(Enumerable.Range(0, N).Select(y => num[x, y])) == CONST - NUMBERS.Min() * N);

            m.AddConstr(m.Sum(Enumerable.Range(0, N).Select(i => num[i, i])) == CONST - NUMBERS.Min() * N);
            m.AddConstr(m.Sum(Enumerable.Range(0, N).Select(i => num[N - 1 - i, i])) == CONST - NUMBERS.Min() * N);

            m.Solve();

            Console.WriteLine();

            for (var y = 0; y < N; y++)
            {
                for (var x = 0; x < N; x++)
                {
                    for (var n = 0; n < NUMBERS.Length; n++)
                        if (v[x, y, n].X)
                            Console.Write($"{NUMBERS[n],3} ");
                }

                Console.WriteLine();
            }

            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}
