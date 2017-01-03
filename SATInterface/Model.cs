using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;

namespace SATInterface
{
    public class Model
    {
        internal int VarIdCounter = 1;
        private List<BoolExpr> clauses = new List<BoolExpr>();
        internal bool proofSat = false;
        internal bool proofUnsat = false;

        public bool IsSatisfiable
        {
            get
            {
                return proofSat;
            }
        }

        public bool IsUnsatisfiable
        {
            get
            {
                return proofUnsat;
            }
        }

        public Model()
        {
        }

        public void AddConstr(BoolExpr _clause)
        {
            if (proofUnsat)
                return;

            var simp = _clause.Simplify();
            if (ReferenceEquals(simp, BoolExpr.FALSE))
                proofUnsat = true;
            else if (simp is AndExpr)
            {
                foreach (var v in simp.EnumVars())
                    v.AssignModelId(this);

                clauses.AddRange(((AndExpr)simp).elements);
            }
            else if (!ReferenceEquals(simp, BoolExpr.TRUE))
            {
                foreach (var v in simp.EnumVars())
                    v.AssignModelId(this);

                clauses.Add(simp);
            }

            proofSat = false;
        }

        public bool LogOutput = true;
        public int Threads = GetNumerOfPhysicalCores();
        public int LogLines = int.MaxValue;


        //Code by Kevin Kibler
        //- http://stackoverflow.com/questions/1542213/how-to-find-the-number-of-cpu-cores-via-net-c
        private static int GetNumerOfPhysicalCores() => new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor").Get().OfType<ManagementBaseObject>().Sum(i => int.Parse(i["NumberOfCores"].ToString()));


        public void Solve() => Solve("cryptominisat5_simple.exe", $"--verb={(LogOutput ? "1":"0")} --threads={Threads}");


        public void Solve(string _executable, string _arguments)
        {
            if (proofUnsat)
                return;

            var vars = clauses.SelectMany(c => c.EnumVars()).Distinct().ToDictionary(v => v.Id, v => v);

            proofSat = false;

            var p = Process.Start(new ProcessStartInfo()
            {
                FileName = _executable,
                Arguments = _arguments,

                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            //p.PriorityClass = ProcessPriorityClass.BelowNormal;

            p.StandardInput.AutoFlush = false;
            Write(p.StandardInput);
            p.StandardInput.Close();

            
            if (LogOutput && LogLines != int.MaxValue)
                Console.WriteLine(new string('\n',LogLines));

            var log = new List<string>();
            var oldCursor = Console.CursorTop-LogLines;

            for (var line = p.StandardOutput.ReadLine(); line != null; line = p.StandardOutput.ReadLine())
            {
                var tk = line.Split(' ').Where(e => e != "").ToArray();
                if (tk.Length == 0)
                {
                    //skip empty lines
                }
                if (tk.Length > 1 && tk[0] == "c")
                {
                    if (LogOutput && LogLines != int.MaxValue)
                    {
                        if(line.Length>Console.BufferWidth-1)
                            line = line.Substring(0, Console.BufferWidth - 1);
                        if (log.Any() && line.Length<log.Last().TrimEnd(' ').Length)
                            log.Add(line+new string(' ',log.Last().TrimEnd(' ').Length - line.Length));
                        else
                            log.Add(line);

                        if (log.Count > LogLines)
                        {
                            log.RemoveAt(0);
                            Console.CursorTop = oldCursor;
                            for (var i = 0; i < log.Count; i++)
                                Console.WriteLine(log[i]);
                        }
                        else
                        {
                            Console.CursorTop = oldCursor + log.Count - 1;
                            Console.WriteLine(line);
                        }
                    }
                    else if(LogOutput)
                        Console.WriteLine(line);
                }
                if (tk.Length == 2 && tk[0] == "s")
                {
                    if(LogOutput)
                        Console.WriteLine(line);
                    if (tk[1] == "SATISFIABLE")
                        proofSat = true;
                    else if (tk[1] == "UNSATISFIABLE")
                        proofUnsat = true;
                    else
                        throw new Exception(tk[2]);
                }
                else if (tk.Length >= 2 && tk[0] == "v")
                {
                    foreach (var n in tk.Skip(1).Select(s => int.Parse(s)))
                        if (n > 0)
                        {
                            vars[n].Value = true;
                            vars.Remove(n);
                        }
                        else if (n < 0)
                        {
                            vars[-n].Value = false;
                            vars.Remove(-n);
                        }
                }
            }

            p.WaitForExit();

            if (proofSat && vars.Any())
                throw new Exception("Undefined vars");
        }

        public void Write(string _path)
        {
            using (var fo = File.CreateText(_path))
                Write(fo);
        }

        public void Write(StreamWriter _out)
        {
            _out.WriteLine("c by .SAT");
            if (clauses.Contains(BoolExpr.FALSE))
            {
                _out.WriteLine("c UNSATISFIABLE");
                _out.WriteLine("p cnf 0 1");
                _out.WriteLine("0");
                _out.Flush();
                return;
            }

            var vars = clauses.SelectMany(c => c.EnumVars()).Distinct().ToArray();

            _out.WriteLine($"p cnf {vars.Length} {clauses.Count}");
            foreach (var c in clauses)
            {
                if (c is BoolVar)
                    _out.Write(((BoolVar)c).Id + " ");
                else if (c is NotExpr)
                    _out.Write(-((BoolVar)((NotExpr)c).inner).Id + " ");
                else if (c is OrExpr)
                {
                    foreach (var e in ((OrExpr)c).elements)
                        if (e is BoolVar)
                            _out.Write(((BoolVar)e).Id + " ");
                        else if (e is NotExpr)
                            _out.Write(-((BoolVar)((NotExpr)e).inner).Id + " ");
                        else
                            throw new Exception(e.GetType().ToString());
                }
                else
                    throw new Exception(c.GetType().ToString());

                _out.WriteLine("0");
                _out.Flush();
            }
        }

        public UIntVar Sum(IEnumerable<BoolExpr> _count)
        {
            var simplified = _count.Select(b => b.Simplify()).Where(b => !ReferenceEquals(b, BoolExpr.FALSE)).ToArray();
            var trueCount = simplified.Count(b => ReferenceEquals(b, BoolExpr.TRUE));

            UIntVar sum;
            if (trueCount == 0)
                sum = new UIntVar(this, 0, _enforceUB: false);
            else
                sum = UIntVar.Const(this, trueCount);

            simplified = simplified.Where(b => !ReferenceEquals(b, BoolExpr.TRUE)).ToArray();
            switch (simplified.Length)
            {
                case 0:
                    return sum;
                case 1:
                    return sum + simplified[0];
                /*case 2:
                    return sum + simplified[0] + simplified[1];*/
                default:
                    var firstHalf = simplified.Take(simplified.Length / 2);
                    var secondHalf = simplified.Skip(simplified.Length / 2);
                    return (Sum(firstHalf) + Sum(secondHalf)) + sum;
            }
        }


        public UIntVar Sum(IEnumerable<UIntVar> _elems)
        {
            var cnt = _elems.Count();
            switch(cnt)
            {
                case 0:
                    return UIntVar.Const(this, 0);
                case 1:
                    return _elems.Single();
                case 2:
                    return _elems.ElementAt(0) + _elems.ElementAt(1);
                case 3:
                    return _elems.ElementAt(0) + _elems.ElementAt(1) + _elems.ElementAt(2);
                default:
                    return Sum(_elems.Take(cnt / 2)) + Sum(_elems.Skip(cnt / 2));
            }
        }


        public enum ExactlyOneOfMethod
        {
            Commander, //61s
            UnaryCount, //106s
            BinaryCount, //165s
            TwoFactor, //forever...
            Pairwise,
            OneHot
        }

        public enum AtMostOneOfMethod
        {
            Pairwise,
            Commander
        }

        public enum ExactlyKOfMethod
        {
            BinaryCount,
            UnaryCount
        }


        public BoolExpr AtMostOneOf(params BoolExpr[] _expr) => AtMostOneOf(_expr.AsEnumerable());

        public BoolExpr AtMostOneOf(IEnumerable<BoolExpr> _expr, AtMostOneOfMethod _method = AtMostOneOfMethod.Commander)
        {
            switch(_method)
            {
                case AtMostOneOfMethod.Commander:
                    return AtMostOneOfCommander(_expr);
                case AtMostOneOfMethod.Pairwise:
                    return AtMostOneOfPairwise(_expr);
                default:
                    throw new ArgumentException();
            }
        }

        public BoolExpr AtMostOneOfCommander(IEnumerable<BoolExpr> _expr)
        {
            if (_expr.Count() < 6)
                return AtMostOneOfPairwise(_expr);

            var expr = _expr.ToArray();
            var groups = new BoolExpr[(expr.Length + 2) / 3][];
            for (var i = 0; i < groups.Length; i++)
                groups[i] = expr.Skip(i * 3).Take(3).ToArray();

            var commanders = new BoolExpr[groups.Length];
            var valid = new List<BoolExpr>();

            for (var i = 0; i < commanders.Length; i++)
                if (groups[i].Length == 1)
                    commanders[i] = groups[i].Single();
                else
                {
                    commanders[i] = new BoolVar(this);

                    //1
                    for (var j = 0; j < groups[i].Length; j++)
                        for (var k = j + 1; k < groups[i].Length; k++)
                            valid.Add(new OrExpr(!groups[i][j], !groups[i][k]));

                    //AddConstr((!commanders[i]) | new OrExpr(groups[i])); //2
                    AddConstr(commanders[i] | (!new OrExpr(groups[i]))); //3
                }

            valid.Add(ExactlyOneOfCommander(commanders));

            return new AndExpr(valid);

        }

        public BoolExpr ExactlyOneOf(params BoolExpr[] _expr) => ExactlyOneOf(_expr.AsEnumerable());

        public BoolExpr ExactlyOneOf(IEnumerable<BoolExpr> _expr, ExactlyOneOfMethod _method=ExactlyOneOfMethod.Commander)
        {
            switch(_method)
            {
                case ExactlyOneOfMethod.UnaryCount:
                    return ExactlyKOf(_expr, 1, ExactlyKOfMethod.UnaryCount);
                case ExactlyOneOfMethod.BinaryCount:
                    return ExactlyKOf(_expr, 1, ExactlyKOfMethod.BinaryCount);
                case ExactlyOneOfMethod.Commander:
                    return ExactlyOneOfCommander(_expr);
                case ExactlyOneOfMethod.TwoFactor:
                    return ExactlyOneOfTwoFactor(_expr);
                case ExactlyOneOfMethod.Pairwise:
                    return ExactlyOneOfPairwise(_expr);
                case ExactlyOneOfMethod.OneHot:
                    return ExactlyOneOfOneHot(_expr);
                default:
                    throw new ArgumentException();
            }
        }

        private BoolExpr ExactlyOneOfOneHot(IEnumerable<BoolExpr> _expr)
        {
            return new OrExpr(_expr.Select(hot => (BoolExpr)new AndExpr(_expr.Select(e => ReferenceEquals(e,hot) ? e:!e))));
        }

        private BoolExpr ExactlyOneOfPairwise(IEnumerable<BoolExpr> _expr)
        {
            return new OrExpr(_expr) & AtMostOneOfPairwise(_expr);
        }

        private BoolExpr AtMostOneOfPairwise(IEnumerable<BoolExpr> _expr)
        {
            var expr = _expr.ToArray();
            var pairs = new List<BoolExpr>();
            for (var i = 0; i < expr.Length; i++)
                for (var j = i + 1; j < expr.Length; j++)
                    pairs.Add(new OrExpr(!expr[i], !expr[j]));

            return new AndExpr(pairs);
        }


        //Formulation by Chen: A New SAT Encoding of the At-Most-One Constraint
        //- https://pdfs.semanticscholar.org/11ea/d39e2799fcb85a9064037080c0f2a1733d82.pdf
        private BoolExpr ExactlyOneOfTwoFactor(IEnumerable<BoolExpr> _expr)
        {
            if(_expr.Count()<6)
                return ExactlyOneOfPairwise(_expr);

            var expr = _expr.ToArray();
            var W = (int)Math.Ceiling(Math.Sqrt(expr.Length));
            var H = (int)Math.Ceiling(expr.Length / (double)W);

            var xv = Enumerable.Range(0, W).Select(x => new BoolVar(this)).ToArray();
            var yv = Enumerable.Range(0, H).Select(y => new BoolVar(this)).ToArray();

            var valid = new List<BoolExpr>();

            {
                var i = 0;
                for (var y = 0; y < H; y++)
                    for (var x = 0; x < W; x++, i++)
                        if (i < expr.Length)
                            valid.Add(expr[i] == (xv[x] & yv[y]));
                        else
                            AddConstr(!(xv[x] & yv[y]));

                Debug.Assert(i >= expr.Length);
            }

            valid.Add(ExactlyOneOfTwoFactor(xv));
            valid.Add(ExactlyOneOfTwoFactor(yv));

            return new AndExpr(valid);
        }


        public BoolExpr ExactlyKOf(IEnumerable<BoolExpr> _expr, int _k, ExactlyKOfMethod _method=ExactlyKOfMethod.UnaryCount)
        {
            var expr = _expr.Select(e => e.Simplify()).Where(e => !ReferenceEquals(e, BoolExpr.FALSE)).ToArray();

            var trueCount = expr.Count(e => ReferenceEquals(e, BoolExpr.TRUE));
            if(trueCount>0)
            {
                _k -= trueCount;
                expr = expr.Where(e => !ReferenceEquals(e, BoolExpr.TRUE)).ToArray();
            }

            if (_k < 0 || _k > expr.Length)
                return BoolExpr.FALSE;
            else if (_k == 0)
                return !new OrExpr(expr);
            else if (_k == expr.Length)
                return new AndExpr(expr);
            else switch (expr.Length)
            {
                case 0:
                    throw new Exception();
                case 1:
                    throw new Exception();
                case 2:
                    if (_k != 1)
                        throw new Exception();
                    return BoolExpr.Xor(expr[0], expr[1]);
                default:

                    switch(_method)
                    {
                        case ExactlyKOfMethod.BinaryCount:
                            return Sum(expr.Select(b =>
                            {
                                if (b is BoolVar)
                                    return (UIntVar)b;
                                else
                                    return UIntVar.Const(this, 1) * b;
                            })) == _k;
                        case ExactlyKOfMethod.UnaryCount:
                            var uc = UnaryCount(expr);
                            return new AndExpr(Enumerable.Range(0,uc.Length).Select(i => (i<_k) ? uc[i] : !uc[i]));
                        default:
                            throw new ArgumentException();
                    }
            }
        }


        //Formulation by Klieber & Kwon: Efficient CNF Encoding for Selecting 1 from N Objects  
        //- https://www.cs.cmu.edu/~wklieber/papers/2007_efficient-cnf-encoding-for-selecting-1.pdf
        private BoolExpr ExactlyOneOfCommander(IEnumerable<BoolExpr> _expr)
        {
            if(_expr.Count()<6)
                return ExactlyOneOfPairwise(_expr);

            var expr = _expr.ToArray();
            var groups = new BoolExpr[(expr.Length + 2) / 3][];
            for (var i = 0; i < groups.Length; i ++)
                groups[i] = expr.Skip(i*3).Take(3).ToArray();

            var commanders = new BoolExpr[groups.Length];
            var valid = new List<BoolExpr>();

            for (var i = 0; i < commanders.Length; i++)
                if(groups[i].Length==1)
                    commanders[i] = groups[i].Single();
                else
                {
                    commanders[i] = new BoolVar(this);

                    //1
                    for (var j = 0; j < groups[i].Length; j++)
                        for (var k = j + 1; k < groups[i].Length; k++)
                            valid.Add(new OrExpr(!groups[i][j], !groups[i][k]));

                    AddConstr((!commanders[i]) | new OrExpr(groups[i])); //2
                    AddConstr(commanders[i] | (!new OrExpr(groups[i]))); //3
                }

            valid.Add(ExactlyOneOfCommander(commanders));

            return new AndExpr(valid);
        }


        //Formulation by Bailleux & Boufkhad
        //- https://pdfs.semanticscholar.org/a948/1bf4ce2b5c20d2e282dd69dcb92bddcc36c9.pdf
        public BoolExpr[] UnaryCount(IEnumerable<BoolExpr> _e)
        {
            var len = _e.Count();
            switch (len)
            {
                case 0:
                    return new BoolExpr[0];
                case 1:
                    return new BoolExpr[] { _e.Single() };
                default:
                    var R = new BoolExpr[len+2];
                    R[0] = true;
                    for (var i = 1; i < R.Length-1; i++)
                        R[i] = new BoolVar(this);
                    R[R.Length-1] = false;

                    var A = new BoolExpr[] { true }.Concat(UnaryCount(_e.Take(len / 2))).Concat(new BoolExpr[] { false }).ToArray();
                    var B = new BoolExpr[] { true }.Concat(UnaryCount(_e.Skip(len / 2))).Concat(new BoolExpr[] { false }).ToArray();
                    for (var a = 0; a < A.Length - 1; a++)
                        for (var b = 0; b < B.Length - 1; b++)
                        {
                            var r = a + b;
                            if (r >=0 && r<R.Length)
                            {
                                var C1 = !A[a] | !B[b] | R[r];
                                var C2 = A[a + 1] | B[b + 1] | !R[r + 1];

                                AddConstr(C1 & C2);
                            }
                        }

                    return R.Skip(1).Take(R.Length-2).ToArray();
            }
        }

        /*
        public BoolExpr[] UnaryCountSorting(BoolExpr[] _v) => SortDescending(_v);

        public BoolExpr[] SortAscending(BoolExpr[] _v) => SortDescending(_v).Reverse().ToArray();

        public BoolExpr[] SortDescending(BoolExpr[] _v)
        {
            switch(_v.Length)
            {
                case 0:
                case 1:
                    return _v;
                case 2:
                    return new BoolExpr[] { BoolExpr.Max(_v[0], _v[1]), BoolExpr.Min(_v[0], _v[1]) };
                default:

                    var v2size=_v.Length-1;
                    v2size |= v2size >> 1;
                    v2size |= v2size >> 2;
                    v2size |= v2size >> 4;
                    v2size |= v2size >> 8;
                    v2size |= v2size >> 16;
                    v2size++;

                    var v1size = v2size >> 1;

                    var v2 = _v;
                    if(v2size!=_v.Length)
                        v2 = _v.Concat(Enumerable.Repeat(BoolExpr.FALSE,v2size-_v.Length)).ToArray();

                    var firstHalf = SortDescending(v2.Take(v1size).ToArray());
                    var secondHalf = SortDescending(v2.Skip(v1size).ToArray());

                    //bitonic sort
                    var result = new BoolExpr[v2size];
                    for (var i = 0; i < v1size; i++)
                    {
                        AddConstr((result[i] = new BoolVar(this)) == BoolExpr.Max(firstHalf[i], secondHalf[v1size - i - 1]));
                        AddConstr((result[v2size-i-1] = new BoolVar(this)) == BoolExpr.Min(firstHalf[i], secondHalf[v1size - i - 1]));
                    }

                    for(var s=v1size>>1;s>=1;s>>=1)
                        for(var b=0;b<v2size;b+=s+s)
                            for(var i=0;i<s;i++)
                            {
                                var max = BoolExpr.Max(result[i + b], result[i + s + b]);
                                var min = BoolExpr.Min(result[i + b], result[i + s + b]);
                                AddConstr((result[i + b] = new BoolVar(this)) == max);
                                AddConstr((result[i+s+b] = new BoolVar(this)) == min);
                            }

                    return result.Take(_v.Length).ToArray();
            }
        }*/
    }
}
