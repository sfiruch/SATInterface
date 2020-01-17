﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SATInterface
{
    public class Model
    {
        //TODO add support for linear combinations as data type
        //TODO add support for encoding of PBE constraints

        internal int VarCount = 0;
        private List<int[]> clauses = new List<int[]>();
        private Dictionary<int, BoolVar> vars = new Dictionary<int, BoolVar>();
        internal bool proofSat = false;
        internal bool proofUnsat = false;

        public bool IsSatisfiable => proofSat;
        public bool IsUnsatisfiable => proofUnsat;

        public Model()
        {
        }

        //TODO common subexpression elimination
        private void AddConstrInternal(BoolExpr _c)
        {
            if (ReferenceEquals(_c, BoolExpr.False))
                proofUnsat = true;
            else if (_c is BoolVar boolVar)
                clauses.Add(new[] { boolVar.Id });
            else if (_c is NotExpr notExpr)
                clauses.Add(new[] { -notExpr.inner.Id });
            else if (_c is OrExpr orExpr)
            {
                //TODO splitting might lead to more CSE matches?
                //if(orExpr.elements.Length>4)
                //{
                //    var ors = new List<BoolExpr>();
                //    for(var i=0;i<orExpr.elements.Length;i+=3)
                //        ors.Add(OrExpr.Create(orExpr.elements.Skip(i).Take(3)).Flatten());
                //    AddConstr(OrExpr.Create(ors));
                //    return;
                //}

                var sb = new int[orExpr.elements.Length];
                for (var i = 0; i < orExpr.elements.Length; i++)
                    if (orExpr.elements[i] is BoolVar bv)
                        sb[i] = bv.Id;
                    else if (orExpr.elements[i] is NotExpr ne)
                        sb[i] = -ne.inner.Id;
                    else
                        throw new Exception(orExpr.elements[i].GetType().ToString());

                //TODO perform CSE
                //Array.Sort(sb);

                clauses.Add(sb);
            }
            else
                throw new Exception(_c.GetType().ToString());
        }

        public void AddConstr(BoolExpr _clause)
        {
            if (proofUnsat)
                return;

            if (_clause is AndExpr andExpr)
                foreach (var e in andExpr.elements)
                    AddConstrInternal(e);
            else if (!ReferenceEquals(_clause, BoolExpr.True))
                AddConstrInternal(_clause);

            proofSat = false;
        }

        public BoolExpr AddVar() => new BoolVar(this);

        public BoolExpr[] AddVars(int _n1)
        {
            var res = new BoolVar[_n1];
            for (var i1 = 0; i1 < _n1; i1++)
                res[i1] = new BoolVar(this);
            return res;
        }

        public BoolExpr[,] AddVars(int _n1, int _n2)
        {
            var res = new BoolVar[_n1, _n2];
            for (var i1 = 0; i1 < _n1; i1++)
                for (var i2 = 0; i2 < _n2; i2++)
                    res[i1, i2] = new BoolVar(this);
            return res;
        }

        public BoolExpr[,,] AddVars(int _n1, int _n2, int _n3)
        {
            var res = new BoolVar[_n1, _n2, _n3];
            for (var i1 = 0; i1 < _n1; i1++)
                for (var i2 = 0; i2 < _n2; i2++)
                    for (var i3 = 0; i3 < _n3; i3++)
                        res[i1, i2, i3] = new BoolVar(this);
            return res;
        }

        public BoolExpr[,,,] AddVars(int _n1, int _n2, int _n3, int _n4)
        {
            var res = new BoolVar[_n1, _n2, _n3, _n4];
            for (var i1 = 0; i1 < _n1; i1++)
                for (var i2 = 0; i2 < _n2; i2++)
                    for (var i3 = 0; i3 < _n3; i3++)
                        for (var i4 = 0; i4 < _n4; i4++)
                            res[i1, i2, i3, i4] = new BoolVar(this);
            return res;
        }

        internal void RegisterVariable(BoolVar boolVar) => vars[boolVar.Id] = boolVar;

        public bool LogOutput = true;

        public enum OptimizationStrategy
        {
            BinarySearch,
            Increasing,
            Decreasing
        }


        public void Minimize(UIntVar _obj, Action? _solutionCallback = null, OptimizationStrategy _strategy = OptimizationStrategy.BinarySearch)
            => Maximize(_obj.UB - _obj, _solutionCallback,
                _strategy switch
                {
                    OptimizationStrategy.Increasing => OptimizationStrategy.Decreasing,
                    OptimizationStrategy.Decreasing => OptimizationStrategy.Increasing,
                    _ => _strategy
                }, _minimization: true);

        public void Maximize(UIntVar _obj, Action? _solutionCallback = null, OptimizationStrategy _strategy = OptimizationStrategy.BinarySearch)
            => Maximize(_obj, _solutionCallback, _strategy, _minimization: false);

        private void Maximize(UIntVar _obj, Action? _solutionCallback, OptimizationStrategy _strategy, bool _minimization)
        {
            if (_obj.UB == UIntVar.Unbounded)
                throw new ArgumentException($"Optimization supports optimization of bounded variables only.");

            if (proofUnsat)
                return;

            using (var cms = new CaDiCaL())
            {
                cms.Verbosity = (LogOutput ? 1 : 0);

                var mVars = vars.Count;
                var mClauses = clauses.Count;

                cms.AddVars(vars.Count);
                foreach (var line in clauses)
                    cms.AddClause(line);

                var bestAssignment = cms.Solve();
                if (bestAssignment == null)
                {
                    proofUnsat = true;
                    return;
                }

                //found initial, feasible solution
                Debug.Assert(bestAssignment.Length == vars.Count);
                for (var i = 0; i < vars.Count; i++)
                    vars[i + 1].Value = bestAssignment[i];
                proofSat = true;

                _solutionCallback?.Invoke();

                //start search
                var lb = _obj.X;
                var ub = _obj.UB;

                var originalVars = new Dictionary<int, BoolVar>(vars);
                var originalClauses = clauses.ToList();
                for (; ; )
                {
                    if (LogOutput)
                    {
                        if (_minimization)
                            Console.WriteLine($"Minimizing objective, range {_obj.UB - ub} - {_obj.UB - lb}");
                        else
                            Console.WriteLine($"Maximizing objective, range {lb} - {ub}");
                    }

                    int cur = _strategy switch
                    {
                        OptimizationStrategy.BinarySearch => (lb + 1 + ub) / 2,
                        OptimizationStrategy.Decreasing => ub,
                        OptimizationStrategy.Increasing => lb + 1,
                        _ => throw new ArgumentException(nameof(_strategy))
                    };

                    //add additional clauses
                    int[]? assumptions;
                    if (_strategy == OptimizationStrategy.Increasing)
                    {
                        AddConstr(_obj >= cur);
                        assumptions = null;
                    }
                    else
                    {
                        var objGE = new BoolVar(this);
                        AddConstr(objGE == (_obj >= cur));
                        assumptions = new int[] { objGE.Id };
                    }

                    cms.AddVars(vars.Count - mVars);
                    mVars = vars.Count;

                    for (var i = mClauses; i < clauses.Count; i++)
                        cms.AddClause(clauses[i]);
                    mClauses = clauses.Count;

                    var assignment = proofUnsat ? null : cms.Solve(assumptions);
                    if (assignment != null)
                    {
                        for (var i = 0; i < vars.Count; i++)
                            vars[i + 1].Value = assignment[i];

                        proofSat = true;

                        Debug.Assert(_obj.X >= cur);

                        _solutionCallback?.Invoke();

                        lb = _obj.X;
                        bestAssignment = assignment;
                    }
                    else
                    {
                        ub = cur - 1;
                    }

                    Debug.Assert(lb <= ub);
                    if (lb == ub)
                        break;
                }

                //restore best known solution
                proofSat = true;
                proofUnsat = false;
                vars = originalVars;
                clauses = originalClauses;
                for (var i = 0; i < vars.Count; i++)
                    vars[i + 1].Value = bestAssignment[i];
            }
        }

        public void Solve()
        {
            if (proofUnsat)
                return;

            proofSat = false;

            //set up model
            using (var cms = new CaDiCaL())
            {
                cms.Verbosity = (LogOutput ? 1 : 0);

                cms.AddVars(vars.Count);
                foreach (var line in clauses)
                    cms.AddClause(line);

                var res = cms.Solve();
                if (res != null)
                {
                    proofSat = true;
                    Debug.Assert(res.Length == vars.Count);

                    for (var i = 0; i < vars.Count; i++)
                        vars[i + 1].Value = res[i];
                }
                else
                    proofUnsat = true;
            }
        }

        public void SolveWithExternalSolver(string _executable, string? _arguments = null, string? _newLine = null, string? _tmpInputFilename = null, string? _tmpOutputFilename = null)
        {
            if (proofUnsat)
                return;

            proofSat = false;

            if (_tmpInputFilename != null)
                Write(_tmpInputFilename);

            var p = Process.Start(new ProcessStartInfo()
            {
                FileName = _executable,
                Arguments = _arguments,
                RedirectStandardInput = _tmpInputFilename == null,
                RedirectStandardOutput = _tmpOutputFilename == null,
                UseShellExecute = false
            });

            Thread? satWriterThread = null;
            if (_tmpInputFilename == null)
            {
                satWriterThread = new Thread(new ParameterizedThreadStart(delegate
                {
                    p.StandardInput.AutoFlush = false;
                    p.StandardInput.NewLine = _newLine ?? "\n";
                    Write(p.StandardInput);
                    p.StandardInput.Close();
                }))
                {
                    IsBackground = true,
                    Name = "SAT Writer Thread"
                };
                satWriterThread.Start();
            }
            if (_tmpInputFilename != null)
                p.WaitForExit();

            var log = new List<string>();
            try
            {
                using (StreamReader output = _tmpOutputFilename == null ? p.StandardOutput : File.OpenText(_tmpOutputFilename))
                {
                    for (var line = output.ReadLine(); line != null; line = output.ReadLine())
                    {
                        var tk = line.Split(' ').Where(e => e != "").ToArray();
                        if (tk.Length == 0)
                        {
                            //skip empty lines
                        }
                        if (tk.Length > 1 && tk[0] == "c" && _tmpOutputFilename == null)
                        {
                            if (LogOutput)
                                Console.WriteLine(line);
                        }
                        if (tk.Length == 2 && tk[0] == "s")
                        {
                            if (LogOutput && _tmpOutputFilename == null)
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
                                    vars[n].Value = true;
                                else if (n < 0)
                                    vars[-n].Value = false;
                        }
                    }
                }
            }
            finally
            {
                satWriterThread?.Abort();
                p.WaitForExit();

                if (_tmpInputFilename == null)
                    p.StandardInput.Dispose();
                if (_tmpOutputFilename == null)
                    p.StandardOutput.Dispose();
                p.Dispose();

                if (_tmpInputFilename != null)
                    File.Delete(_tmpInputFilename);
                if (_tmpOutputFilename != null)
                    File.Delete(_tmpOutputFilename);
            }
        }

        public void Write(string _path)
        {
            using (var fo = File.CreateText(_path))
                Write(fo);
        }

        public void Write(StreamWriter _out)
        {
            _out.WriteLine("c Created by SATInterface");
            _out.Flush();
            _out.WriteLine($"p cnf {vars.Count} {clauses.Count}");
            _out.Flush();
            foreach (var line in clauses)
            {
                _out.Write(string.Join(' ', line));
                _out.WriteLine(" 0");
                _out.Flush();
            }
        }

        //code by Noldorin/Simon
        //- http://stackoverflow.com/a/812035/742404
        internal static uint RotateLeft(uint value, int count) => (value << count) | (value >> (32 - count));
        internal static uint RotateRight(uint value, int count) => (value >> count) | (value << (32 - count));


        public UIntVar Sum(params BoolExpr[] _count) => Sum((IEnumerable<BoolExpr>)_count);

        public UIntVar Sum(IEnumerable<BoolExpr> _count)
        {
            var simplified = _count.Where(b => !ReferenceEquals(b, BoolExpr.False)).ToArray();
            var trueCount = simplified.Count(b => ReferenceEquals(b, BoolExpr.True));
            simplified = simplified.Where(b => !ReferenceEquals(b, BoolExpr.True)).ToArray();

            switch (simplified.Length)
            {
                case 0:
                    return UIntVar.Const(this, trueCount);
                case 1:
                    return UIntVar.Convert(this, simplified[0]) + trueCount;
                default:
                    var firstHalf = simplified.Take(simplified.Length / 2);
                    var secondHalf = simplified.Skip(simplified.Length / 2);
                    return (Sum(firstHalf) + Sum(secondHalf)) + trueCount;
            }
        }


        public UIntVar Sum(IEnumerable<UIntVar> _elems)
        {
            var cnt = _elems.Count();
            switch (cnt)
            {
                case 0:
                    return UIntVar.Const(this, 0);
                case 1:
                    return _elems.Single();
                default:
                    return Sum(_elems.Take(cnt / 2)) + Sum(_elems.Skip(cnt / 2));
            }
        }


        public enum ExactlyOneOfMethod
        {
            Commander,
            UnaryCount,
            BinaryCount,
            TwoFactor,
            Pairwise,
            OneHot,
            Sequential
        }

        public enum AtMostOneOfMethod
        {
            Pairwise,
            Commander,
            OneHot,
            Sequential
        }

        public enum ExactlyKOfMethod
        {
            BinaryCount,
            UnaryCount,
            Pairwise,
            Sequential
        }


        public BoolExpr AtMostOneOf(params BoolExpr[] _expr) => AtMostOneOf(_expr.AsEnumerable());

        public BoolExpr AtMostOneOf(IEnumerable<BoolExpr> _expr, AtMostOneOfMethod _method = AtMostOneOfMethod.Sequential)
        {
            var expr = _expr.Where(e => !ReferenceEquals(e, BoolExpr.False)).ToArray();

            var trueCount = expr.Count(e => ReferenceEquals(e, BoolExpr.True));
            if (trueCount > 1)
                return BoolExpr.False;

            if (trueCount == 1)
                return !OrExpr.Create(expr.Where(e => !ReferenceEquals(e, BoolExpr.True))).Flatten();

            Debug.Assert(trueCount == 0);

            switch (expr.Length)
            {
                case 0:
                case 1:
                    return BoolExpr.True;
                case 2:
                    return !expr[0] | !expr[1];
                case 3:
                    return AtMostOneOfPairwise(_expr);
            }

            switch (_method)
            {
                case AtMostOneOfMethod.Commander:
                    return AtMostOneOfCommander(expr);
                case AtMostOneOfMethod.Pairwise:
                    return AtMostOneOfPairwise(expr);
                case AtMostOneOfMethod.OneHot:
                    return AtMostOneOfOneHot(expr);
                case AtMostOneOfMethod.Sequential:
                    return AtMostOneOfSequential(expr);
                default:
                    throw new ArgumentException();
            }
        }

        private BoolExpr AtMostOneOfSequential(IEnumerable<BoolExpr> _expr)
        {
            var v0 = BoolExpr.False;
            var v1 = BoolExpr.False;
            foreach (var e in _expr)
            {
                v1 = ((v0 & e) | v1).Flatten();
                v0 = (v0 | e).Flatten();
            }
            return !v1;
        }

        private BoolExpr AtMostOneOfCommander(IEnumerable<BoolExpr> _expr)
        {
            if (_expr.Count() <= 5)
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
                            valid.Add(OrExpr.Create(!groups[i][j], !groups[i][k]));

                    //AddConstr((!commanders[i]) | new OrExpr(groups[i])); //2
                    AddConstr(commanders[i] | (!OrExpr.Create(groups[i]))); //3
                }

            valid.Add(ExactlyOneOfCommander(commanders));
            return AndExpr.Create(valid);
        }

        public BoolExpr ExactlyOneOf(params BoolExpr[] _expr) => ExactlyOneOf(_expr.AsEnumerable());

        public BoolExpr ExactlyOneOf(IEnumerable<BoolExpr> _expr, ExactlyOneOfMethod _method = ExactlyOneOfMethod.Commander)
        {
            var expr = _expr.Where(e => !ReferenceEquals(e, BoolExpr.False)).ToArray();

            var trueCount = expr.Count(e => ReferenceEquals(e, BoolExpr.True));
            if (trueCount > 1)
                return BoolExpr.False;

            if (trueCount == 1)
                return !OrExpr.Create(expr.Where(e => !ReferenceEquals(e, BoolExpr.True))).Flatten();

            Debug.Assert(trueCount == 0);

            switch (expr.Length)
            {
                case 0:
                    return BoolExpr.False;
                case 1:
                    return expr[0];
                case 2:
                    return expr[0] ^ expr[1];
                case 3:
                    return ExactlyOneOfPairwise(_expr);
            }

            switch (_method)
            {
                case ExactlyOneOfMethod.UnaryCount:
                    return ExactlyKOf(_expr, 1, ExactlyKOfMethod.UnaryCount);
                case ExactlyOneOfMethod.BinaryCount:
                    return ExactlyKOf(_expr, 1, ExactlyKOfMethod.BinaryCount);
                case ExactlyOneOfMethod.Sequential:
                    return ExactlyKOf(_expr, 1, ExactlyKOfMethod.Sequential);
                case ExactlyOneOfMethod.Commander:
                    return ExactlyOneOfCommander(_expr);
                case ExactlyOneOfMethod.TwoFactor:
                    return ExactlyOneOfTwoFactor(_expr);
                case ExactlyOneOfMethod.Pairwise:
                    return ExactlyOneOfPairwise(_expr);
                case ExactlyOneOfMethod.OneHot:
                    return ExactlyOneOfOneHot(_expr.ToArray());
                default:
                    throw new ArgumentException();
            }
        }

        private BoolExpr ExactlyOneOfOneHot(BoolExpr[] _expr)
        {
            var ors = new List<BoolExpr>();
            for (var i = 0; i < _expr.Length; i++)
            {
                var ands = new List<BoolExpr>();
                for (var j = 0; j < _expr.Length; j++)
                    ands.Add((i == j) ? _expr[j] : !_expr[j]);
                ors.Add(AndExpr.Create(ands).Flatten());
            }
            return OrExpr.Create(ors);
        }

        private BoolExpr AtMostOneOfOneHot(BoolExpr[] _expr)
        {
            var ors = new List<BoolExpr>();
            for (var i = 0; i < _expr.Length; i++)
            {
                var ands = new List<BoolExpr>();
                for (var j = 0; j < _expr.Length; j++)
                    if (i != j)
                        ands.Add(!_expr[j]);
                ors.Add(AndExpr.Create(ands).Flatten());
            }
            return OrExpr.Create(ors);
        }

        private BoolExpr ExactlyOneOfPairwise(IEnumerable<BoolExpr> _expr)
        {
            return OrExpr.Create(_expr).Flatten() & AtMostOneOfPairwise(_expr);
        }

        private BoolExpr AtMostOneOfPairwise(IEnumerable<BoolExpr> _expr)
        {
            var expr = _expr.ToArray();
            var pairs = new List<BoolExpr>();
            for (var i = 0; i < expr.Length; i++)
                for (var j = i + 1; j < expr.Length; j++)
                    pairs.Add(OrExpr.Create(!expr[i], !expr[j]));

            return AndExpr.Create(pairs);
        }


        //Formulation by Chen: A New SAT Encoding of the At-Most-One Constraint
        //- https://pdfs.semanticscholar.org/11ea/d39e2799fcb85a9064037080c0f2a1733d82.pdf
        private BoolExpr ExactlyOneOfTwoFactor(IEnumerable<BoolExpr> _expr)
        {
            if (_expr.Count() < 6)
                return ExactlyOneOf(_expr);

            var expr = _expr.ToArray();
            var W = (int)Math.Ceiling(Math.Sqrt(expr.Length));
            var H = (int)Math.Ceiling(expr.Length / (double)W);

            var rows = new List<BoolExpr>();
            var cols = new List<BoolExpr>();
            for (var y = 0; y < H; y++)
            {
                var c = new List<BoolExpr>();
                for (var x = 0; x < W; x++)
                {
                    var i = W * y + x;
                    if (i < expr.Length)
                        c.Add(expr[i]);
                }
                cols.Add(OrExpr.Create(c).Flatten());
            }

            for (var x = 0; x < W; x++)
            {
                var c = new List<BoolExpr>();
                for (var y = 0; y < H; y++)
                {
                    var i = W * y + x;
                    if (i < expr.Length)
                        c.Add(expr[i]);
                }
                rows.Add(OrExpr.Create(c).Flatten());
            }

            return ExactlyOneOfTwoFactor(rows) &
                ExactlyOneOfTwoFactor(cols);
        }


        public BoolExpr ExactlyKOf(IEnumerable<BoolExpr> _expr, int _k, ExactlyKOfMethod _method = ExactlyKOfMethod.Sequential)
        {
            var expr = _expr.Where(e => !ReferenceEquals(e, BoolExpr.False)).ToArray();

            var trueCount = expr.Count(e => ReferenceEquals(e, BoolExpr.True));
            if (trueCount > 0)
            {
                _k -= trueCount;
                expr = expr.Where(e => !ReferenceEquals(e, BoolExpr.True)).ToArray();
            }

            if (_k < 0 || _k > expr.Length)
                return BoolExpr.False;
            else if (_k == 0)
                return !OrExpr.Create(expr).Flatten();
            else if (_k == expr.Length)
                return AndExpr.Create(expr).Flatten();

            Debug.Assert(_k >= 1 && _k < expr.Length);

            switch (_method)
            {
                case ExactlyKOfMethod.BinaryCount:
                    return Sum(expr) == _k;

                case ExactlyKOfMethod.UnaryCount:
                    var uc = UnaryCount(expr);
                    return uc[_k - 1] & !uc[_k];
                //return AndExpr.Create(Enumerable.Range(0, uc.Length).Select(i => (i < _k) ? uc[i] : !uc[i]));

                case ExactlyKOfMethod.Sequential:
                    var v = Enumerable.Repeat(BoolExpr.False, _k + 1).ToArray();
                    foreach (var e in expr)
                    {
                        var vnext = new BoolExpr[_k + 1];
                        vnext[0] = (v[0] | e).Flatten();
                        for (var i = 1; i < _k + 1; i++)
                            vnext[i] = ((v[i - 1] & e) | v[i]).Flatten();
                        v = vnext;
                    }
                    return v[_k - 1] & !v[_k];

                case ExactlyKOfMethod.Pairwise:

                    if (_k == 1)
                        return ExactlyOneOfPairwise(expr);
                    else if (_k == 2)
                    {
                        //at most 2
                        var and = new List<BoolExpr>();
                        for (var i = 0; i < expr.Length; i++)
                            for (var j = i + 1; j < expr.Length; j++)
                                for (var k = j + 1; k < expr.Length; k++)
                                    and.Add(!expr[i] | !expr[j] | !expr[k]);

                        //at least 2
                        var or = new List<BoolExpr>();
                        for (var i = 0; i < expr.Length; i++)
                            for (var j = i + 1; j < expr.Length; j++)
                                or.Add(expr[i] & expr[j]);

                        return AndExpr.Create(and) & OrExpr.Create(or);
                    }
                    else
                    {
                        var or = new List<BoolExpr>();
                        for (var i = 0; i < expr.Length; i++)
                            or.Add((expr[i]
                                & AndExpr.Create(Enumerable.Range(0, i - 1).Select(j => !expr[j]))
                                & ExactlyKOf(Enumerable.Range(i + 1, expr.Length - i - 1).Select(j => expr[j]), _k - 1, _method)
                                ).Flatten());
                        return OrExpr.Create(or).Flatten();
                    }

                default:
                    throw new ArgumentException(nameof(_method));
            }
        }


        //Formulation by Klieber & Kwon: Efficient CNF Encoding for Selecting 1 from N Objects  
        //- https://www.cs.cmu.edu/~wklieber/papers/2007_efficient-cnf-encoding-for-selecting-1.pdf
        private BoolExpr ExactlyOneOfCommander(IEnumerable<BoolExpr> _expr)
        {
            if (_expr.Count() < 5)
                return ExactlyOneOfPairwise(_expr);

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
                            valid.Add(OrExpr.Create(!groups[i][j], !groups[i][k]).Flatten());

                    AddConstr((!commanders[i]) | OrExpr.Create(groups[i])); //2
                    AddConstr(commanders[i] | (!OrExpr.Create(groups[i]))); //3
                }

            valid.Add(ExactlyOneOfCommander(commanders));

            return AndExpr.Create(valid);
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
                    var R = new BoolExpr[len + 2];
                    R[0] = BoolExpr.True;
                    for (var i = 1; i < R.Length - 1; i++)
                        R[i] = new BoolVar(this);
                    R[R.Length - 1] = BoolExpr.False;

                    var A = new BoolExpr[] { BoolExpr.True }.Concat(UnaryCount(_e.Take(len / 2))).Concat(new BoolExpr[] { BoolExpr.False }).ToArray();
                    var B = new BoolExpr[] { BoolExpr.True }.Concat(UnaryCount(_e.Skip(len / 2))).Concat(new BoolExpr[] { BoolExpr.False }).ToArray();
                    for (var a = 0; a < A.Length - 1; a++)
                        for (var b = 0; b < B.Length - 1; b++)
                        {
                            var r = a + b;
                            if (r >= 0 && r < R.Length)
                            {
                                var C1 = OrExpr.Create(!A[a], !B[b], R[r]).Flatten();
                                var C2 = OrExpr.Create(A[a + 1], B[b + 1], !R[r + 1]).Flatten();
                                AddConstr(C1 & C2);
                            }
                        }

                    return R.Skip(1).Take(R.Length - 2).ToArray();
            }
        }
    }
}
