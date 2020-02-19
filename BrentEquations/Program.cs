using SATInterface;
using System;
using System.Diagnostics;
using System.Linq;

//  This example strives to solve "Brent Equations" (see https://arxiv.org/abs/1108.2830)
//  to find an algorithm to multiply two matrices with a given number of elementary
//  multiplications. The famous Strassen algorithm uses seven products to multiply two
//  2x2 matrices.
//
//  This model was graciously shared by A. Kemper, https://github.com/a1880

namespace BrentEquations
{
    class Program
    {
        //  dimensions of the matrix multiplication
        //  <10s simple test cases include 1x1x1_1, 1x2x1_2, 1x2x2_4, 2x2x2_7, 2x3x2_11, 3x3x3_27
        //  multi-hour tedious cases 2x3x3_15, 3x3x3_23
        //  research cases 3x3x3_22, 3x3x3_21
        const int ARows = 3;
        const int ACols = 3;
        const int BCols = 3;
        const int NoOfProducts = 22;

        const int BRows = ACols;
        const int CRows = ARows;
        const int CCols = BCols;

        static void Main(string[] args)
        {
            var watch = new Stopwatch();

            Console.WriteLine("akBrent  -  Matrix Multiplication Solver");
            Console.WriteLine("");
            Console.WriteLine($"Problem <{ARows}x{ACols}x{BCols}_{NoOfProducts}>");
            Console.WriteLine("");

            watch.Start();

            using var m = new Model();

            //  decision variables are 3D arrays pf BoolVar
            var f = m.AddVars(ARows, ACols, NoOfProducts);
            var g = m.AddVars(BRows, BCols, NoOfProducts);
            var d = m.AddVars(CRows, CCols, NoOfProducts);

            var triples = new BoolExpr[NoOfProducts];

            for (int ra = 0; ra < ARows; ra++)
                for (int ca = 0; ca < ACols; ca++)
                    for (int rb = 0; rb < BRows; rb++)
                        for (int cb = 0; cb < BCols; cb++)
                            for (int rc = 0; rc < CRows; rc++)
                                for (int cc = 0; cc < CCols; cc++)
                                {
                                    for (int k = 0; k < NoOfProducts; k++)
                                        triples[k] = (f[ra, ca, k] & g[rb, cb, k] & d[rc, cc, k]).Flatten();

                                    if ((ra == rc) && (ca == rb) && (cb == cc))
                                        //odd
                                        m.AddConstr(m.SumUInt(triples).Bits[0]);
                                    else
                                        //even
                                        m.AddConstr(!m.SumUInt(triples).Bits[0]);
                                }

            Console.WriteLine("");
            Console.WriteLine("Preparation complete");
            Console.WriteLine($"Elapsed: {watch.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine("");
            Console.WriteLine("Starting search");

            m.Solve();

            if (m.IsSatisfiable)
            {
                Console.WriteLine("");
                Console.WriteLine("Solution found:");
                Console.WriteLine("");
                ShowBoolArray("F", f);
                ShowBoolArray("G", g);
                ShowBoolArray("D", d);

                VerifySolution(f, g, d);
            }
            else
            {
                Console.WriteLine("");
                Console.WriteLine("No solution found. Sorry");
                Console.WriteLine("");
            }

            Console.WriteLine("");
            watch.Stop();
            Console.WriteLine($"Elapsed: {watch.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine("");
            Console.WriteLine("Ciao!");
        }

        private static void ShowBoolArray(string name, BoolExpr[,,] a)
        {
            int rows = a.GetLength(0);
            int cols = a.GetLength(1);
            int products = a.GetLength(2);

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    for (int k = 0; k < products; k++)
                    {
                        Console.WriteLine($"{name}{r + 1}{c + 1}{'a'+k} = {(a[r, c, k].X ? "1" : "0")}");
                    }
        }

        static void VerifySolution(BoolExpr[,,] f, BoolExpr[,,] g, BoolExpr[,,] d)
        {
            for (int ra = 0; ra < ARows; ra++)
                for (int ca = 0; ca < ACols; ca++)
                    for (int rb = 0; rb < BRows; rb++)
                        for (int cb = 0; cb < BCols; cb++)
                            for (int rc = 0; rc < CRows; rc++)
                                for (int cc = 0; cc < CCols; cc++)
                                {
                                    int sum = 0;
                                    for (int k = 0; k < NoOfProducts; k++)
                                    {
                                        sum +=
                                            (f[ra, ca, k].X &&
                                             g[rb, cb, k].X &&
                                             d[rc, cc, k].X) ? 1 : 0;
                                    }

                                    bool odd = ((ra == rc) && (ca == rb) && (cb == cc));
                                    if (odd != ((sum % 2) == 1))
                                        throw new Exception("Invalid solution");
                                }
        }
    }
}
