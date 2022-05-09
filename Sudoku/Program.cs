// SATInterface is a library to formulate SAT problems in .NET 
// - https://github.com/deiruch/SATInterface
//
// The interface includes these excellent SAT solvers, but any solver
// that supports DIMACS can be used (e.g. Lingeling, Clasp, RISS, MiniSAT, ...).
//
// * CaDiCaL is MIT-licensed and available from https://github.com/arminbiere/cadical
// * Kissat is MIT-licensed and available from https://github.com/arminbiere/kissat
// * CryptoMiniSat is MIT-licensed and available from https://github.com/msoos/cryptominisat
//
// Here's a usage example: Sudoku

using System;
using System.Linq;
using SATInterface;
using SATInterface.Solver;

using var m = new Model(new Configuration()
{
    Verbosity = 2
});

var v = m.AddVars(9, 9, 9);

//instead of a variable, use the constant "True" for first number 1
v[0, 0, 0] = true;

//here's alternative way to set the second number
m.AddConstr(v[1, 0, 1]);

//assign one number to each cell
for (var y = 0; y < 9; y++)
    for (var x = 0; x < 9; x++)
        m.AddConstr(m.Sum(Enumerable.Range(0, 9).Select(n => v[x, y, n])) == 1);

//each number occurs once per row (alternative formulation)
for (var y = 0; y < 9; y++)
    for (var n = 0; n < 9; n++)
        m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, 9).Select(x => v[x, y, n])));

//each number occurs once per column (configured formulation)
for (var x = 0; x < 9; x++)
    for (var n = 0; n < 9; n++)
        m.AddConstr(m.ExactlyOneOf(Enumerable.Range(0, 9).Select(y => v[x, y, n]), Model.ExactlyOneOfMethod.PairwiseTree));

//each number occurs once per 3x3 block
for (var n = 0; n < 9; n++)
    for (var y = 0; y < 9; y += 3)
        for (var x = 0; x < 9; x += 3)
            m.AddConstr(m.Sum(
                v[x + 0, y + 0, n], v[x + 1, y + 0, n], v[x + 2, y + 0, n],
                v[x + 0, y + 1, n], v[x + 1, y + 1, n], v[x + 2, y + 1, n],
                v[x + 0, y + 2, n], v[x + 1, y + 2, n], v[x + 2, y + 2, n]) == 1);

m.Solve();

if (m.State == State.Satisfiable)
    for (var y = 0; y < 9; y++)
    {
        for (var x = 0; x < 9; x++)
            for (var n = 0; n < 9; n++)
                if (v[x, y, n].X)
                    Console.Write($" {n + 1}");
        Console.WriteLine();
    }
