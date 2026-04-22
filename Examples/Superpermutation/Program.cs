using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Superpermutation
{
    class Program
    {
        //from https://codereview.stackexchange.com/questions/226804/linq-for-generating-all-possible-permutations
        private static IEnumerable<T[]> Permutate<T>(IEnumerable<T> source)
        {
            return permutate(source, Enumerable.Empty<T>());
            IEnumerable<T[]> permutate(IEnumerable<T> remainder, IEnumerable<T> prefix) =>
                !remainder.Any() ? new[] { prefix.ToArray() } :
                remainder.SelectMany((c, i) => permutate(
                    remainder.Take(i).Concat(remainder.Skip(i + 1)).ToArray(),
                    prefix.Append(c)));
        }

        static void Main(string[] args)
        {
            //https://oeis.org/A180632
            //3   4    5   6      7       8       9
            //9  33  153 872  -5906  -46205 -408966
            const int N = 4;
            const int LEN = 33;

            using var m = new Model(new Configuration()
            { 
                Solver = new SATInterface.Solver.Kissat()
            });

            var v = m.AddVars(N, LEN);

            for (var x = 0; x < LEN; x++)
                m.AddConstr(m.Sum(Enumerable.Range(0, N).Select(n => v[n, x])) == 1);

            foreach (var permutation in Permutate(Enumerable.Range(0, N)))
            {
                var pos = m.AddVars(LEN - N + 1);
                m.AddConstr(m.Sum(pos) == 1);

                for (var x = 0; x < LEN - N + 1; x++)
                    for (var i = 0; i < N; i++)
                        m.AddConstr(!pos[x] | v[permutation[i], x + i]);
            }

            for (var x = 1; x < LEN; x++)
                for (var i = 0; i < N; i++)
                    m.AddConstr(!(v[i, x] & v[i, x - 1]));

            for (var i = 0; i < N; i++)
                m.AddConstr(v[i, i]);

            m.Solve();

            if (m.State == State.Satisfiable)
                for (var x = 0; x < LEN; x++)
                    for (var n = 0; n < N; n++)
                        if (v[n, x].X)
                            Console.Write((n + 1));
        }
    }
}
