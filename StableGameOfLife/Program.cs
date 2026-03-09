using SATInterface;

string[] target = [
    "...............................",
    "..xxxxxx....xxxxxxxx.....xxxxx.",
    ".xxxxxxxx...xxxxxxxx....xxxxxx.",
    ".xx....xx......xx......xxx.....",
    ".xx....xx......xx......xxx.....",
    ".xxxxxxxx......xx.......xxxx...",
    ".xxxxxxxx......xx........xxxx..",
    ".xx....xx......xx..........xxx.",
    ".xx....xx......xx..........xxx.",
    ".xx....xx......xx......xxxxxx..",
    ".xx....xx......xx......xxxxx...",
    "...............................",
];

var W = target[0].Length;
var H = target.Length;

var m = new Model();

var v = m.AddVars(W, H);

for (var y = 0; y < H; y++)
    for (var x = 0; x < W; x++)
    {
        var l = new List<BoolExpr>();
        if (x > 0 && y > 0)
            l.Add(v[x - 1, y - 1]);
        if (y > 0)
            l.Add(v[x, y - 1]);
        if (x < W - 1 && y > 0)
            l.Add(v[x + 1, y - 1]);
        if (x > 0)
            l.Add(v[x - 1, y]);
        if (x < W - 1)
            l.Add(v[x + 1, y]);
        if (x > 0 && y < H - 1)
            l.Add(v[x - 1, y + 1]);
        if (y < H - 1)
            l.Add(v[x, y + 1]);
        if (x < W - 1 && y < H - 1)
            l.Add(v[x + 1, y + 1]);

        var neighbours = m.Sum(l);
        m.AddConstr(v[x, y] == ((v[x, y] & neighbours == 2) | neighbours == 3));
    }

m.Minimize(m.Sum(Enumerable.Range(0, W).SelectMany(x => Enumerable.Range(0, H).Select(y => target[y][x] == 'x' ? !v[x, y] : v[x, y]))),
    () =>
    {
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
                Console.Write(v[x, y].X ? 'x' : '.');
            Console.WriteLine();
        }
    });