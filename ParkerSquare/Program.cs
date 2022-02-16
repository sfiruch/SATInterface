// https://www.youtube.com/watch?v=aOT_bG-vWyg

using SATInterface;
using SATInterface.Solver;

const int N = 3;

using var m = new Model(new Configuration()
{
    Solver = new Kissat(),
    ConsoleSolverLines = null
});


var vN = new UIntVar[N, N];
var vN2 = new UIntVar[N, N];

for (var y = 0; y < N; y++)
    for (var x = 0; x < N; x++)
    {
        vN[x, y] = m.AddUIntVar(1 << 15, false);
        m.AddConstr(m.Or(vN[x, y].Bits));
        vN2[x, y] = vN[x, y] * vN[x, y];
        m.AddConstr(!vN2[x,y].Bits[1]);
    }


var sum = m.AddUIntVar(UIntVar.Unbounded, false);
for (var i = 0; i < N; i++)
{
    m.AddConstr(sum == m.Sum(Enumerable.Range(0, N).Select(x => vN2[x, i])));
    m.AddConstr(sum == m.Sum(Enumerable.Range(0, N).Select(y => vN2[i, y])));
}
m.AddConstr(sum == m.Sum(Enumerable.Range(0, N).Select(i => vN2[i, i])));
m.AddConstr(sum == m.Sum(Enumerable.Range(0, N).Select(i => vN2[N - 1 - i, i])));

m.AddConstr(vN[0, 0] < vN[N - 1, 0]);
m.AddConstr(vN[0, 0] < vN[0, N - 1]);
m.AddConstr(vN[0, 0] < vN[N - 1, N - 1]);

m.AddConstr(vN[N - 1, 0] < vN[0, N - 1]);

m.Solve();

if (m.State != State.Satisfiable)
    return;

for (var y = 0; y < N; y++)
{
    for (var x = 0; x < N; x++)
        Console.Write($" {vN2[x, y].X,10}");
    Console.WriteLine();
}