[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Build Status](https://github.com/deiruch/SATInterface/workflows/.NET/badge.svg)](https://github.com/deiruch/SATInterface/actions)
[![NuGet Package](https://img.shields.io/nuget/v/deiruch.SATInterface.svg)](https://www.nuget.org/packages/deiruch.SATInterface/)

# SATInterface
SATInterface is a .NET library to formulate SAT problems

## Installation
Add a reference to the NuGet Package [deiruch.SATInterface](https://www.nuget.org/packages/deiruch.SATInterface/).

## Features
- Maximize or minimize linear objective functions (3 strategies)
- Enumerate all solutions
- Supports linear combinations
- Convenient .NET operator overloading
- Simplify Boolean formulas
- Translate Boolean formulas to CNF
- Includes algorithms for
  - Counting (Totalizer)
  - At-most-one-constraints (7 implementations)
  - Exactly-one-constraints (9 implementations)
  - Exactly-k-constraints (4 implementations)
  - Unsigned integer arithmetic (Addition, Subtraction, Multiplication, Shifting)
- Export to DIMACS files
- Includes Kissat (https://github.com/arminbiere/kissat), CaDiCaL (https://github.com/arminbiere/cadical) and CryptoMiniSAT (see https://github.com/msoos/cryptominisat)

## Usage example: Sudoku
~~~~cs
using var m = new Model();
var v = m.AddVars(9, 9, 9);

//fix the first number to 1
v[0, 0, 0] = true;

//here's alternative way to set the second number
m.AddConstr(v[1, 0, 1]);

//assign one number to each cell
for (var y = 0; y < 9; y++)
    for (var x = 0; x < 9; x++)
        m.AddConstr(m.Sum(Enumerable.Range(0, 9).Select(n => v[x, y, n])) == 1);

//each number occurs once per row
for (var y = 0; y < 9; y++)
    for (var n = 0; n < 9; n++)
        m.AddConstr(m.Sum(Enumerable.Range(0, 9).Select(x => v[x, y, n])) == 1);

//each number occurs once per column
for (var x = 0; x < 9; x++)
    for (var n = 0; n < 9; n++)
        m.AddConstr(m.Sum(Enumerable.Range(0, 9).Select(y => v[x, y, n])) == 1);

//each number occurs once per 3x3 block
for (var n = 0; n < 9; n++)
    for (var y = 0; y < 9; y += 3)
        for (var x = 0; x < 9; x += 3)
            m.AddConstr(m.Sum(v[x + 0, y + 0, n], v[x + 1, y + 0, n], v[x + 2, y + 0, n],
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
~~~~
