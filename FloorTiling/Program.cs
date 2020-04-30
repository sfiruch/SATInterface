using SATInterface;
using System;
using System.Collections.Generic;

namespace FloorTiling
{
    class Program
    {
        static void Main(string[] args)
        {
            //https://yetanothermathprogrammingconsultant.blogspot.com/2020/03/tiling.html

            using var m = new Model();

            var N = 14;
            var SIZE = new[] { 1, 2, 3, 4, 5, 6 };
            var COUNT = new[] { 6, 5, 4, 3, 2, 1 };

            //var N = 28;
            //var SIZE = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            //var COUNT = new[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 };

            var vXYS = m.AddVars(N, N, SIZE.Length);
            for (var s = 0; s < SIZE.Length; s++)
                for (var y = 0; y < N; y++)
                    for (var x = 0; x < N; x++)
                        if (x + SIZE[s] > N || y + SIZE[s] > N)
                            vXYS[x, y, s] = false;

            for (var s = 0; s < SIZE.Length; s++)
            {
                var sum = new List<BoolExpr>();
                for (var y = 0; y < N; y++)
                    for (var x = 0; x < N; x++)
                        sum.Add(vXYS[x, y, s]);

                m.AddConstr(m.Sum(sum) <= COUNT[s]);
            }

            //var area = (LinExpr)0;
            //for (var s = 0; s < SIZE.Length; s++)
            //{
            //    for (var y = 0; y < N; y++)
            //        for (var x = 0; x < N; x++)
            //            area += vXYS[x, y, s] * (SIZE[s] * SIZE[s]);

            //}
            //m.AddConstr(area == N * N);


            for (var y = 0; y < N; y++)
                for (var x = 0; x < N; x++)
                {
                    var sum = new List<BoolExpr>();
                    for (var s = 0; s < SIZE.Length; s++)
                        for (var ya = 0; ya < SIZE[s] && ya <= y; ya++)
                            for (var xa = 0; xa < SIZE[s] && xa <= x; xa++)
                                sum.Add(vXYS[x - xa, y - ya, s]);

                    //m.AddConstr(m.Or(sum));
                    m.AddConstr(m.ExactlyOneOf(sum));
                    //m.AddConstr(m.Sum(sum) == 1);
                }

            m.Solve();

            if (m.State==State.Satisfiable)
                for (var y = 0; y < N; y++)
                {
                    for (var x = 0; x < N; x++)
                        for (var s = 0; s < SIZE.Length; s++)
                            for (var ya = 0; ya < SIZE[s] && ya <= y; ya++)
                                for (var xa = 0; xa < SIZE[s] && xa <= x; xa++)
                                    if (vXYS[x - xa, y - ya, s].X)
                                        Console.Write((char)('A' + s));
                    Console.WriteLine();
                }
        }
    }
}
