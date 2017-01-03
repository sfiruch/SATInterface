﻿//SATInterface is a library to formulate SAT problems in .NET 
//
//By default the bundeled excellent CryptoMiniSAT solver is used as backend, but any solver
//that supports DIMACS can be used (e.g. Lingeling, RISS, MiniSAT, ...). CryptoMiniSAT
//is MIT licensed and available from https://github.com/msoos/cryptominisat

var m = new Model();
var v = new BoolExpr[9, 9, 9];
for (var y = 0; y < 9; y++)
    for (var x = 0; x < 9; x++)
        for (var n = 0; n < 9; n++)
            v[x, y, n] = new BoolVar(m);

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