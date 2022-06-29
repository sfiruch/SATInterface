//Solves the Eternity II puzzle, http://www.shortestpath.se/eii/eii_details.html
//Viewer at https://e2.bucas.name/

using SATInterface;
using SATInterface.Solver;

var pieces = File.ReadAllLines("e2pieces.txt")
    .Select(l => l.Split(' ').Select(t => int.Parse(t)).ToArray())
    .ToArray();

var MaxEdge = pieces.Max(p => p.Max());

const int W = 16;
const int H = 16;

var m = new Model(new Configuration() { Solver = new Kissat(), ConsoleSolverLines = null });
var vXYPR = new BoolExpr[W, H, pieces.Length, 4];

for (var y = 0; y < H; y++)
    for (var x = 0; x < W; x++)
        for (var p = 0; p < pieces.Length; p++)
            for (var r = 0; r < 4; r++)
            {
                var rot = Rotate(pieces[p], r);
                if (x == 0 ^ rot[0] == 0)
                    vXYPR[x, y, p, r] = false;
                else if (y == 0 ^ rot[1] == 0)
                    vXYPR[x, y, p, r] = false;
                else if (x == W - 1 ^ rot[2] == 0)
                    vXYPR[x, y, p, r] = false;
                else if (y == H - 1 ^ rot[3] == 0)
                    vXYPR[x, y, p, r] = false;
                else
                    vXYPR[x, y, p, r] = m.AddVar();
            }

//each place assigned one piece
for (var y = 0; y < H; y++)
    for (var x = 0; x < W; x++)
    {
        var any = new List<BoolExpr>();
        for (var p = 0; p < pieces.Length; p++)
            for (var r = 0; r < 4; r++)
                any.Add(vXYPR[x, y, p, r]);
        m.AddConstr(m.ExactlyOneOf(any));
    }

//each piece used once
for (var p = 0; p < pieces.Length; p++)
{
    var any = new List<BoolExpr>();
    for (var y = 0; y < H; y++)
        for (var x = 0; x < W; x++)
            for (var r = 0; r < 4; r++)
                any.Add(vXYPR[x, y, p, r]);
    m.AddConstr(m.ExactlyOneOf(any));
}

for (var y = 0; y < H; y++)
    for (var x = 0; x < W - 1; x++)
        for (var e = 1; e <= MaxEdge; e++)
        {
            var left = new List<BoolExpr>();
            var right = new List<BoolExpr>();
            for (var p = 0; p < pieces.Length; p++)
                for (var r = 0; r < 4; r++)
                {
                    var rot = Rotate(pieces[p], r);
                    if (rot[2] == e)
                        left.Add(vXYPR[x, y, p, r]);
                    if (rot[0] == e)
                        right.Add(vXYPR[x + 1, y, p, r]);
                }

            m.AddConstr(m.Or(left) == m.Or(right));
        }

for (var y = 0; y < H - 1; y++)
    for (var x = 0; x < W; x++)
        for (var e = 1; e <= MaxEdge; e++)
        {
            var top = new List<BoolExpr>();
            var bottom = new List<BoolExpr>();
            for (var p = 0; p < pieces.Length; p++)
                for (var r = 0; r < 4; r++)
                {
                    var rot = Rotate(pieces[p], r);
                    if (rot[3] == e)
                        top.Add(vXYPR[x, y, p, r]);
                    if (rot[1] == e)
                        bottom.Add(vXYPR[x, y + 1, p, r]);
                }

            m.AddConstr(m.Or(top) == m.Or(bottom));
        }


//clues
m.AddConstr(vXYPR[7, 8, 139, 2]);
m.AddConstr(vXYPR[13, 13, 249, 0]);
m.AddConstr(vXYPR[2, 13, 181, 3]);
m.AddConstr(vXYPR[13, 2, 255, 3]);
m.AddConstr(vXYPR[2, 2, 208, 3]);

m.Solve();

for (var y = 0; y < H; y++)
{
    for (var x = 0; x < W; x++)
        for (var p = 0; p < pieces.Length; p++)
            for (var r = 0; r < 4; r++)
                if (vXYPR[x, y, p, r].X)
                    Console.Write($"{p,4}");
    Console.WriteLine();
}

int[] Rotate(int[] _v, int _rot) => _v[(3 - _rot)..].Concat(_v[..(3 - _rot)]).ToArray();
