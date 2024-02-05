using SATInterface;
using System;
using System.Collections.Generic;
using System.Linq;

//Lagae, Ares, and Philip Dutré. "The tile packing problem." Geombinatorics 17.1 (2007): 8-18.

const int C = 3;
const int N = C * C * C * C;

const int W = C * C;
const int H = C * C;

using var m = new Model(new Configuration()
{
    Solver = new SATInterface.Solver.Kissat()
});

var vXYC = m.AddVars(W, H, C);

//symmetry breaking
for (var c = 0; c < C; c++)
{
    vXYC[0, 0, c] = vXYC[1, 0, c] = vXYC[0, 1, c] = vXYC[1, 1, c] = vXYC[0, 2, c] = (c == 0);
    vXYC[1, 2, c] = (c == 1);
}

for (var y = 0; y < H; y++)
    for (var x = 0; x < W; x++)
        m.AddConstr(m.Sum(Enumerable.Range(0, C).Select(c => vXYC[x, y, c])) == 1);

for (var n = 0; n < N; n++)
{
    var c1 = n % C;
    var c2 = (n / C) % C;
    var c3 = (n / C / C) % C;
    var c4 = (n / C / C / C) % C;
    var l = new List<BoolExpr>();
    for (var y = 0; y < H; y++)
        for (var x = 0; x < W; x++)
            l.Add((vXYC[x, y, c1] & vXYC[(x + 1) % W, y, c2] & vXYC[x, (y + 1) % H, c3] & vXYC[(x + 1) % W, (y + 1) % H, c4]).Flatten());

    m.AddConstr(m.Sum(l) == 1);
}

m.Solve();

for (var y = 0; y < H; y++)
{
    for (var x = 0; x < W; x++)
        for (var c = 0; c < C; c++)
            if (vXYC[x, y, c].X)
                Console.Write(c);
    Console.WriteLine();
}
