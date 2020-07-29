using SATInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

//This example model solves "Brent Equations" mod 2 (the first phase of 
//https://arxiv.org/abs/1108.2830) to find an algorithm to multiply two
//matrices with a given number of elementary multiplications. The famous
//Strassen algorithm uses seven products to multiply two 2x2 matrices.
//
//This simplified example was adapted from a model graciously shared by
//Axel Kemper (https://www.linkedin.com/in/axel-kemper-b4757996/)

namespace BrentEquations
{
    class Program
    {
        //dimensions of the matrix multiplication
        //simple test cases: 1x1x1_1, 1x2x1_2, 1x2x2_4, 2x2x2_7, 2x3x2_11, 3x3x3_27
        //tedious cases: 2x3x3_15, 3x3x3_23
        //research cases: 3x3x3_22, 3x3x3_21
        const int ARows = 3;
        const int ACols = 3;
        const int BCols = 3;
        const int NoOfProducts = 23;

        const int BRows = ACols;
        const int CRows = ARows;
        const int CCols = BCols;

        static void Main(string[] args)
        {
            var watch = new Stopwatch();

            Console.WriteLine("akBrent - Matrix Multiplication Solver");
            Console.WriteLine("");

            watch.Start();

            using var m = new Model();

            var a = m.AddVars(ARows, ACols, NoOfProducts);
            var b = m.AddVars(BRows, BCols, NoOfProducts);
            var c = m.AddVars(CRows, CCols, NoOfProducts);

            //symmetry breaking: permutations of k
            for (var k = 1; k < NoOfProducts; k++)
            {
                var akm1 = m.AddUIntVar(UIntVar.Unbounded, Enumerable.Range(0, ARows).SelectMany(r => Enumerable.Range(0, ACols).Select(c => a[r, c, k - 1])).ToArray());
                var ak = m.AddUIntVar(UIntVar.Unbounded, Enumerable.Range(0, ARows).SelectMany(r => Enumerable.Range(0, ACols).Select(c => a[r, c, k])).ToArray());
                m.AddConstr(akm1 >= ak);
            }

            //symmetry breaking: transpose
            if (ARows == ACols)
            {
                var originalA = m.AddUIntVar(UIntVar.Unbounded, Enumerable.Range(0, ARows).SelectMany(r => Enumerable.Range(0, ACols).Select(c => a[r, c, 0])).ToArray());
                var transposeA = m.AddUIntVar(UIntVar.Unbounded, Enumerable.Range(0, ARows).SelectMany(r => Enumerable.Range(0, ACols).Select(c => a[c, r, 0])).ToArray());
                m.AddConstr(originalA <= transposeA);
            }

            for (var ra = 0; ra < ARows; ra++)
                for (var ca = 0; ca < ACols; ca++)
                    for (var rb = 0; rb < BRows; rb++)
                        for (var cb = 0; cb < BCols; cb++)
                            for (var rc = 0; rc < CRows; rc++)
                                for (var cc = 0; cc < CCols; cc++)
                                {
                                    var triples = new BoolExpr[NoOfProducts];
                                    for (var k = 0; k < NoOfProducts; k++)
                                        triples[k] = (a[ra, ca, k] & b[rb, cb, k] & c[rc, cc, k]).Flatten();

                                    if ((ra == rc) && (ca == rb) && (cb == cc))
                                        //odd
                                        m.AddConstr(m.Xor(triples));
                                        //m.AddConstr(m.SumUInt(triples).Bits[0]);
                                    else
                                        //even
                                        m.AddConstr(!m.Xor(triples));
                                        //m.AddConstr(!m.SumUInt(triples).Bits[0]);
                                        //{
                                        //    var sum = m.Sum(triples);
                                        //    m.AddConstr(sum == 2 | sum==0);
                                        //}
                                        //m.AddConstr(m.ExactlyKOf(triples,2,Model.ExactlyKOfMethod.UnaryCount).Flatten() | !m.Or(triples).Flatten());
                                }

            Console.WriteLine($"Problem <{ARows}x{ACols}x{BCols}_{NoOfProducts}> setup in {watch.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine("");

            m.Write($"be{ARows}x{ACols}x{BCols}_{NoOfProducts}.dimacs");
            m.Configuration.Verbosity = 2;

            m.Solve();

            Console.WriteLine("");
            if (m.State == State.Satisfiable)
            {
                Console.WriteLine("Solution found. Non-zero coefficients:");
                Console.WriteLine("");
                PrintArray("A", a);
                PrintArray("B", b);
                PrintArray("C", c);

                VerifySolution(a, b, c);
            }
            else
                Console.WriteLine("No solution found.");

            Console.WriteLine("");
            watch.Stop();
            Console.WriteLine($"Elapsed: {watch.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine("");
            Console.WriteLine("Ciao!");
        }

        static void PrintArray(string name, BoolExpr[,,] a)
        {
            Console.Write($"{name}:");
            for (var r = 0; r < a.GetLength(0); r++)
                for (var c = 0; c < a.GetLength(1); c++)
                    for (var k = 0; k < a.GetLength(2); k++)
                        if (a[r, c, k].X)
                            Console.Write($" {r + 1},{c + 1},{(char)('a' + k)}");
            Console.WriteLine();
        }

        static void VerifySolution(BoolExpr[,,] f, BoolExpr[,,] g, BoolExpr[,,] d)
        {
            for (var ra = 0; ra < ARows; ra++)
                for (var ca = 0; ca < ACols; ca++)
                    for (var rb = 0; rb < BRows; rb++)
                        for (var cb = 0; cb < BCols; cb++)
                            for (var rc = 0; rc < CRows; rc++)
                                for (var cc = 0; cc < CCols; cc++)
                                {
                                    var sum = 0;
                                    for (var k = 0; k < NoOfProducts; k++)
                                    {
                                        sum +=
                                            (f[ra, ca, k].X &&
                                             g[rb, cb, k].X &&
                                             d[rc, cc, k].X) ? 1 : 0;
                                    }

                                    var odd = ((ra == rc) && (ca == rb) && (cb == cc));
                                    if (odd != ((sum % 2) == 1))
                                        throw new Exception("Invalid solution");
                                }
        }
    }
}
