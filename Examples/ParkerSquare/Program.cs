// https://www.youtube.com/watch?v=aOT_bG-vWyg

using SATInterface;
using SATInterface.Solver;
using System.Numerics;

const int N = 3;

var squares = new List<BigInteger>();
for (var s = BigInteger.One; ; s++)
{
    var square = s * s;
    squares.Add(square);

    if (square < 9_424_900)
        continue;

    Console.WriteLine($"{squares.Count} squares: {squares[0]} - {squares[^1]}");

    var vN2 = new UIntVar[N, N];
    var vSelected = new BoolExpr[N, N, squares.Count];
    using var m = new Model(new Configuration()
    {
        Solver = new Kissat(),
        //ConsoleSolverLines = null
    });

    for (var y = 0; y < N; y++)
        for (var x = 0; x < N; x++)
        {
            vN2[x, y] = m.AddUIntVar(square);
            for (var i = 0; i < squares.Count; i++)
            {
                vSelected[x, y, i] = m.AddVar();
                m.AddConstr(vN2[x, y] == m.AddUIntConst(squares[i]) | !vSelected[x, y, i]);
            }

            m.AddConstr(m.Or(Enumerable.Range(0, squares.Count).Select(i => vSelected[x, y, i])));
        }

    for (var i = 0; i < squares.Count - 1; i++)
        m.AddConstr(m.Sum(Enumerable.Range(0, N).SelectMany(x => Enumerable.Range(0, N).Select(y => vSelected[x, y, i]))) <= 1);
    m.AddConstr(m.Sum(Enumerable.Range(0, N).SelectMany(x => Enumerable.Range(0, N).Select(y => vSelected[x, y, squares.Count - 1]))) == 1);

    var sum = m.AddUIntVar(square * N);
    for (var i = 0; i < N; i++)
    {
        m.AddConstr(sum == m.Sum(Enumerable.Range(0, N).Select(x => vN2[x, i])));
        m.AddConstr(sum == m.Sum(Enumerable.Range(0, N).Select(y => vN2[i, y])));
    }
    m.AddConstr(sum == m.Sum(Enumerable.Range(0, N).Select(i => vN2[i, i])));
    m.AddConstr(sum == m.Sum(Enumerable.Range(0, N).Select(i => vN2[N - 1 - i, i])));

    m.AddConstr(vN2[0, 0] < vN2[N - 1, 0]);
    m.AddConstr(vN2[0, 0] < vN2[0, N - 1]);
    m.AddConstr(vN2[0, 0] < vN2[N - 1, N - 1]);
    m.AddConstr(vN2[N - 1, 0] < vN2[0, N - 1]);

    m.Solve();

    if (m.State == State.Satisfiable)
    {
        for (var y = 0; y < N; y++)
        {
            for (var x = 0; x < N; x++)
                Console.Write($" {vN2[x, y].X,10}");
            Console.WriteLine();
        }
        return;
    }
}