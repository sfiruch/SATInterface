﻿using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sudoku
{
    class Program
    {
        static void Main(string[] args)
        {
            var m = new Model();
            var v = new BoolExpr[9, 9, 9];
            for (var y = 0; y < 9; y++)
                for (var x = 0; x < 9; x++)
                    for (var n = 0; n < 9; n++)
                        v[x, y, n] = new BoolVar(m);

            //http://www.telegraph.co.uk/news/science/science-news/9359579/Worlds-hardest-sudoku-can-you-crack-it.html

            var sudoku =    "8........" +
                            "..36....." +
                            ".7..9.2.." +
                            ".5...7..." +
                            "....457.." +
                            "...1...3." +
                            "..1....68" +
                            "..85...1." +
                            ".9....4..";

            for (var y = 0; y < 9; y++)
                for (var x = 0; x < 9; x++)
                    if (sudoku[y * 9 + x] != '.')
                        v[x, y, sudoku[y * 9 + x] - '1'] = true;

            for (var y = 0; y < 9; y++)
                for (var x = 0; x < 9; x++)
                    m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0,9).Select(n => v[x,y,n])));

            for (var y = 0; y < 9; y++)
                for (var n = 0; n < 9; n++)
                    m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, 9).Select(x => v[x, y, n])));

            for (var x = 0; x < 9; x++)
                for (var n = 0; n < 9; n++)
                    m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, 9).Select(y => v[x, y, n])));

            for (var n = 0; n < 9; n++)
                for(var y=0;y<9;y+=3)
                    for (var x = 0; x < 9; x += 3)
                        m.AddConstr(m.ExactlyOneOf(v[x+0, y+0, n], v[x + 1, y + 0, n], v[x + 2, y + 0, n],
                            v[x + 0, y + 1, n], v[x + 1, y + 1, n], v[x + 2, y + 1, n],
                            v[x + 0, y + 2, n], v[x + 1, y + 2, n], v[x + 2, y + 2, n]));

            m.Solve();

            for(var y=0;y<9;y++)
            {
                for (var x = 0; x < 9; x++)
                    for (var n = 0; n < 9; n++)
                        if (v[x, y, n].X)
                            Console.Write($" {n+1}");
                Console.WriteLine();
            }

            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}