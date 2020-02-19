using SATInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PCB
{
    class Program
    {
        const int W = 25;
        const int H = 18;
        const int L = 2;
        const int C = 12;

        static readonly string[] FIELD = new string[]
        {
            "E........................",
            ".....FJ...........#......",
            "#..............####GHI...",
            "####....##...##########..",
            "..###..####...#######L...",
            "....#..####..............",
            ".A......##.............D.",
            ".B.....................C.",
            ".C........G......###...B.",
            ".D...............###...A.",
            "......###................",
            ".....K#..................",
            ".....##.......F..........",
            "......#..............K...",
            "......##...####......H...",
            "..JI......######.........",
            ".............##..........",
            "L.................####..E",
        };

        static void Main(string[] args)
        {
            using var m = new Model();

            var vXYLC = new BoolExpr[W, H, L, C];
            var sourceXYC = new bool[W, H, C];
            var sinkXYC = new bool[W, H, C];

            var hasSource = new bool[C];

            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                    if (FIELD[y][x] >= 'A' && FIELD[y][x] <= 'Z')
                    {
                        if (hasSource[FIELD[y][x] - 'A'])
                            sinkXYC[x, y, FIELD[y][x] - 'A'] = true;
                        else
                        {
                            sourceXYC[x, y, FIELD[y][x] - 'A'] = true;
                            hasSource[FIELD[y][x] - 'A'] = true;
                        }
                    }

            Debug.Assert(hasSource.All(v => v));

            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                    for (var c = 0; c < C; c++)
                        for (var l = 0; l < L; l++)
                        {
                            if (l == 0 && FIELD[y][x] == 'A' + c)
                                vXYLC[x, y, l, c] = true;
                            else if (FIELD[y][x] == '.')
                                vXYLC[x, y, l, c] = m.AddVar();
                            else
                                vXYLC[x, y, l, c] = false;
                        }


            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                    for (var l = 0; l < L; l++)
                    {
                        var sum = new List<BoolExpr>();
                        for (var c = 0; c < C; c++)
                        {
                            sum.Add(vXYLC[x, y, l, c]);

                            var sV = new List<BoolExpr>();

                            if (x > 0)
                                sV.Add(vXYLC[x - 1, y, l, c]);
                            if (y > 0)
                                sV.Add(vXYLC[x, y - 1, l, c]);
                            if (l > 0)
                                sV.Add(vXYLC[x, y, l - 1, c]);
                            if (x < W - 1)
                                sV.Add(vXYLC[x + 1, y, l, c]);
                            if (y < H - 1)
                                sV.Add(vXYLC[x, y + 1, l, c]);
                            if (l < L - 1)
                                sV.Add(vXYLC[x, y, l + 1, c]);

                            if (l == 0 && sourceXYC[x, y, c])
                                    sV.Add(true);
                                if (l == 0 && sinkXYC[x, y, c])
                                    sV.Add(true);

                            m.AddConstr(!vXYLC[x, y, l, c] || m.ExactlyKOf(sV, 2));
                        }

                        m.AddConstr(m.AtMostOneOf(sum));
                    }

            var obj1 = new List<BoolExpr>();
            var obj3 = new List<BoolExpr>();
            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                    for (var c = 0; c < C; c++)
                    {
                        obj1.Add(vXYLC[x, y, 0, c]);
                        obj3.Add(vXYLC[x, y, 1, c]);
                    }

            var vObj = m.Sum(obj1) + m.Sum(obj3) * 3;

            m.Configuration.OptimizationStrategy = OptimizationStrategy.Decreasing;
            m.Minimize(vObj, () =>
            {
                Console.WriteLine();
                Console.WriteLine($"Obj={vObj.X}");
                Console.WriteLine();
                for (var y = H - 1; y >= 0; y--)
                {
                    for (var l = 0; l < L; l++)
                    {
                        for (var x = 0; x < W; x++)
                        {
                            var chr = '.';
                            for (var c = 0; c < C; c++)
                                if (vXYLC[x, y, l, c].X)
                                    chr = (char)('A' + c);
                            Console.Write(chr);
                        }
                        Console.Write("        ");
                    }
                    Console.WriteLine();
                }
                Console.WriteLine();
            });
        }
    }
}
