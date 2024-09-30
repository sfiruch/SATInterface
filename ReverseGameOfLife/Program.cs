//inspired by https://www.youtube.com/watch?v=g8pjrVbdafY

using SATInterface;
using SATInterface.Solver;

string[] final = [
    "...xx..xx..xx...",
    "..x.x..xx..x.x..",
    ".x............x.",
    "x....x.........x",
    "xx...x........xx",
    ".....x..........",
    "........xxx.....",
    ".....x..........",
    "xx...x........xx",
    "x....x.........x",
    ".x............x.",
    "..x.x..xx..x.x..",
    "...xx..xx..xx...",
];

const int W = 40;
const int H = 40;
const int T = 21;

var m = new Model(new Configuration() { Solver = new Kissat() });

var v = new BoolExpr[W, H, T];
for (var y = 0; y < H; y++)
    for (var x = 0; x < W; x++)
        if (x == 0 || y == 0 || x == W - 1 || y == H - 1)
            for (var t = 0; t < T; t++)
                v[x, y, t] = false;
        else
            v[x, y, 0] = m.AddVar();

for (var t = 1; t < T; t++)
    for (var y = 1; y < H - 1; y++)
        for (var x = 1; x < W - 1; x++)
        {
            var neighbours = m.SumUInt(
                    v[x - 1, y - 1, t - 1], v[x, y - 1, t - 1], v[x + 1, y - 1, t - 1],
                    v[x - 1, y, t - 1], v[x, y, t - 1], v[x + 1, y, t - 1],
                    v[x - 1, y + 1, t - 1], v[x, y + 1, t - 1], v[x + 1, y + 1, t - 1]
                );
            v[x, y, t] = ((v[x, y, t - 1] & neighbours == 2) | neighbours == 3).Flatten();
        }

for (var y = 0; y < H; y++)
    for (var x = 0; x < W; x++)
    {
        var dst = false;
        var sx = x - (W - final[0].Length) / 2;
        var sy = y - (H - final.Length) / 2;
        if (sx >= 0 && sy >= 0 && sx < final[0].Length && sy < final.Length)
            dst = final[sy][sx] == 'x';

        m.AddConstr(dst ? v[x, y, T - 1] : !v[x, y, T - 1]);
    }

m.Solve();

for (var t = 0; t < T; t++)
{
    Console.WriteLine("---------------------------------------");
    for (var y = 0; y < H; y++)
    {
        for (var x = 0; x < W; x++)
            Console.Write(v[x, y, t].X ? 'x' : '.');
        Console.WriteLine();
    }
}