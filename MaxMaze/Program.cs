using SATInterface;
using SATInterface.Solver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaxMaze
{
    class Program
    {
        const int W = 30;
        const int H = 30;
        const string input = " ....................#        " +
                            ".....................# XXXXXX " +
                            ".###....###########..#   X    " +
                            "...#..................#X X.XXX" +
                            "...#...................# X    " +
                            "...#...................  XXXX " +
                            "...#....................X   X " +
                            "......................... #   " +
                            "...........................#XX" +
                            ".........######.............##" +
                            ".........#...................." +
                            ".........#....#..............." +
                            ".........#....#..............." +
                            ".........#....#..............." +
                            "..#...........#..............." +
                            "..###....#....#........#######" +
                            ".........###.##..............." +
                            ".............................." +
                            ".............................." +
                            "....##.....................#.." +
                            "...#####.................###.." +
                            "...#####...................#.." +
                            "...#######....#..............." +
                            "....#######..#................" +
                            "......####..#X.......#........" +
                            ".......###.#XX.......#........" +
                            "........#..#####....###......." +
                            "...........#     ............." +
                            "...........# XXXX............." +
                            "...........#     ............ ";

        static void Main()
        {
            using var m = new Model();

            var free = new BoolExpr[W, H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    free[x, y] = input[30 * y + x] switch
                    {
                        '.' => m.AddVar(),
                        ' ' => (BoolExpr)true,
                        '#' or 'X' => (BoolExpr)false,
                        _ => throw new Exception(),
                    };

            free[0, 0] = true;
            free[W - 1, H - 1] = true;

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var a = x > 0 ? free[x - 1, y] : false;
                    var b = x < W - 1 ? free[x + 1, y] : false;
                    var c = y > 0 ? free[x, y - 1] : false;
                    var d = y < H - 1 ? free[x, y + 1] : false;

                    if (x == 0 && y == 0)
                        m.AddConstr(b != d);
                    else if (x == W - 1 && y == H - 1)
                        m.AddConstr(a != c);
                    else
                        m.AddConstr(!free[x, y] | m.Sum(new[] { a, b, c, d }) == 2);
                }

            m.Maximize(m.Sum(free.Cast<BoolExpr>()), () =>
            {
                var sb = new StringBuilder();
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                        sb.Append(free[x, y].X ? "." : ReferenceEquals(free[x, y], Model.False) ? "█" : "▒");
                    sb.AppendLine();
                }

                var loops = 0;
                var notVisited = new bool[W, H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        notVisited[x, y] = free[x, y].X;

                void Visit(int _x, int _y, Stack<(int X, int Y)> _visited)
                {
                    if (_x < 0 || _y < 0 || _x >= W || _y >= H)
                        return;

                    if (_x == W - 1 && _y == H - 1)
                        return;

                    if (_visited.Count > 2)
                    {
                        var start = _visited.Last();
                        if (_x == start.X && _y == start.Y)
                        {
                            m.AddConstr(m.Or(_visited.Select(c => !free[c.X, c.Y])));
                            loops++;
                            return;
                        }
                    }

                    if (!notVisited[_x, _y])
                        return;

                    notVisited[_x, _y] = false;

                    _visited.Push((_x, _y));

                    Visit(_x - 1, _y, _visited);
                    Visit(_x + 1, _y, _visited);
                    Visit(_x, _y - 1, _visited);
                    Visit(_x, _y + 1, _visited);

                    _visited.Pop();
                }

                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        if (notVisited[x, y])
                            Visit(x, y, new Stack<(int X, int Y)>());

                sb.AppendLine($"Found {loops} loops");
                Console.WriteLine(sb.ToString());
            });
        }
    }
}
