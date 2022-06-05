using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SATInterface
{
    /// <summary>
    /// Builds an environment plus associated model, containing variables and
    /// constraints, as well as solver configuration and state.
    /// </summary>
    public class Model : IDisposable
    {
        public static readonly BoolExpr True = new BoolVar(-1);
        public static readonly BoolExpr False = new BoolVar(-2);

        private List<BoolVar> vars = new List<BoolVar>();
        public State State { get; internal set; } = State.Undecided;

        internal bool InOptimization = false;
        internal bool AbortOptimization = false;
        internal bool UnsatWithAssumptions = false;

        public readonly Configuration Configuration;

        /// <summary>
        /// Number of variables in this model.
        /// </summary>
        public int VariableCount => vars.Count;

        /// <summary>
        /// Number of clauses in this model.
        /// </summary>
        public int ClauseCount { get; internal set; }


        private FileStream? DIMACSBuffer;
        private StreamWriter? DIMACSOutput;

        /// <summary>
        /// Allocate a new model.
        /// </summary>
        public Model(Configuration? _configuration = null)
        {
            Configuration = _configuration ?? new Configuration();
            Configuration.Validate();

            Configuration.Solver.Model = this;
            Configuration.Solver.ApplyConfiguration();

            if (Configuration.EnableDIMACSWriting)
            {
                var tmp = Path.GetTempFileName();
                new FileInfo(tmp).Attributes |= FileAttributes.Temporary;
                DIMACSBuffer = File.Create(tmp, 65536, FileOptions.DeleteOnClose);
                DIMACSOutput = new StreamWriter(DIMACSBuffer, Encoding.UTF8);
            }
        }

        private void AddConstrInternal(BoolExpr _c)
        {
            if (ReferenceEquals(_c, False))
            {
                State = State.Unsatisfiable;
                UnsatWithAssumptions = false;
            }
            else if (ReferenceEquals(_c, True))
            {
                //ignore
            }
            else if (_c is AndExpr andExpr)
            {
                foreach (var e in andExpr.Elements)
                    AddConstrInternal(e);
            }
            else if (_c is BoolVar boolVar)
            {
                if (!ReferenceEquals(boolVar.Model, this))
                    throw new ArgumentException("Mixing variables from different models is not supported.");

                Debug.Assert(boolVar.Id > 0);

                AddClauseToSolver(stackalloc[] { boolVar.Id });
            }
            else if (_c is NotExpr notExpr)
            {
                if (!ReferenceEquals(notExpr.inner.Model, this))
                    throw new ArgumentException("Mixing variables from different models is not supported.");

                Debug.Assert(notExpr.inner.Id > 0);

                AddClauseToSolver(stackalloc[] { -notExpr.inner.Id });
            }
            else if (_c is OrExpr orExpr)
            {
                if (OrCache.TryGetValue(orExpr, out var res))
                    AddConstrInternal(res);
                else
                {
                    Span<int> sb = stackalloc int[orExpr.Elements.Length];
                    for (var i = 0; i < sb.Length; i++)
                        if (orExpr.Elements[i] is BoolVar bv)
                        {
                            Debug.Assert(bv.Id > 0);

                            sb[i] = bv.Id;

                            if (!ReferenceEquals(bv.Model, this))
                                throw new ArgumentException("Mixing variables from different models is not supported.");
                        }
                        else if (orExpr.Elements[i] is NotExpr ne)
                        {
                            Debug.Assert(ne.inner.Id > 0);

                            sb[i] = -ne.inner.Id;

                            if (!ReferenceEquals(ne.inner.Model, this))
                                throw new ArgumentException("Mixing variables from different models is not supported.");
                        }
                        else
                            throw new Exception();

                    AddClauseToSolver(sb);
                }
            }
            else
                throw new NotImplementedException(_c.GetType().ToString());
        }

        private void AddClauseToSolver(ReadOnlySpan<int> _x)
        {
            Configuration.Solver.AddClause(_x);
            ClauseCount++;
            if (DIMACSOutput is not null)
                DIMACSOutput.WriteLine(string.Join(' ', _x.ToArray().Append(0)));
        }

        /// <summary>
        /// Adds the supplied constraint to the model.
        /// </summary>
        /// <param name="_clause"></param>
        public void AddConstr(BoolExpr _clause)
        {
            if (State == State.Unsatisfiable && !UnsatWithAssumptions)
                return;

            AddConstrInternal(_clause);

            if (!InOptimization && State == State.Satisfiable)
                State = State.Undecided;
        }

        /// <summary>
        /// Creates a unsigned integer constant. Most operations with such a constant will be short-
        /// circuited by the framework.
        /// </summary>
        /// <param name="_c">The constant value</param>
        /// <returns></returns>
        public UIntVar AddUIntConst(int _c)
        {
            if (UIntConstCache.TryGetValue(_c, out var u))
                return u;
            return UIntConstCache[_c] = UIntVar.Const(this, _c);
        }

        /// <summary>
        /// Creates a new unsigned integer variable from the supplied bits
        /// </summary>
        /// <param name="_ub">Upper bound of this variable or UIntVar.Unbounded when >2^30</param>
        /// <param name="_bits">The bits making up this variable</param>
        /// <param name="_enforceUB">If TRUE, additional constraints enforcing the upper bound will be added to the model</param>
        public UIntVar AddUIntVar(int _ub, BoolExpr[] _bits, bool _enforceUB = false) => new UIntVar(this, _ub, _bits, _enforceUB);

        /// <summary>
        /// Creates a new unsigned integer variable with the specified upper bound.
        /// </summary>
        /// <param name="_ub">Upper bound of this variable or UIntVar.Unbounded when >2^30</param>
        /// <param name="_enforceUB">If TRUE, additional constraints enforcing the upper bound will be added to the model</param>
        public UIntVar AddUIntVar(int _ub, bool _enforceUB = true) => new UIntVar(this, _ub, _enforceUB);

        /// <summary>
        /// Allocate a new boolean variable. This variable takes the value True or False in a SAT model.
        /// </summary>
        public BoolExpr AddVar() => new BoolVar(this);

        /// <summary>
        /// Allocate a new one-dimensional array of boolean variables.
        /// </summary>
        public BoolExpr[] AddVars(int _n1)
        {
            var res = new BoolExpr[_n1];
            for (var i1 = 0; i1 < _n1; i1++)
                res[i1] = AddVar();
            return res;
        }

        /// <summary>
        /// Allocate a new two-dimensional array of boolean variables.
        /// </summary>
        public BoolExpr[,] AddVars(int _n1, int _n2)
        {
            var res = new BoolExpr[_n1, _n2];
            for (var i2 = 0; i2 < _n2; i2++)
                for (var i1 = 0; i1 < _n1; i1++)
                    res[i1, i2] = AddVar();
            return res;
        }

        /// <summary>
        /// Allocate a new three-dimensional array of boolean variables.
        /// </summary>
        public BoolExpr[,,] AddVars(int _n1, int _n2, int _n3)
        {
            var res = new BoolExpr[_n1, _n2, _n3];
            for (var i3 = 0; i3 < _n3; i3++)
                for (var i2 = 0; i2 < _n2; i2++)
                    for (var i1 = 0; i1 < _n1; i1++)
                        res[i1, i2, i3] = AddVar();
            return res;
        }

        /// <summary>
        /// Allocate a new four-dimensional array of boolean variables.
        /// </summary>
        public BoolExpr[,,,] AddVars(int _n1, int _n2, int _n3, int _n4)
        {
            var res = new BoolExpr[_n1, _n2, _n3, _n4];
            for (var i4 = 0; i4 < _n4; i4++)
                for (var i3 = 0; i3 < _n3; i3++)
                    for (var i2 = 0; i2 < _n2; i2++)
                        for (var i1 = 0; i1 < _n1; i1++)
                            res[i1, i2, i3, i4] = AddVar();
            return res;
        }

        internal void RegisterVariable(BoolVar boolVar)
        {
            vars.Add(boolVar);
            Debug.Assert(boolVar.Id == vars.Count);

            if (!InOptimization && State == State.Satisfiable)
                State = State.Undecided;
        }

        /// <summary>
        /// Minimizes the supplied LinExpr by solving multiple models sequentially.
        /// </summary>
        /// <param name="_obj"></param>
        /// <param name="_solutionCallback">Invoked for every incumbent solution.</param>
        public void Minimize(LinExpr _obj, Action? _solutionCallback = null)
            => Optimize(-_obj, _solutionCallback, _minimization: true);

        /// <summary>
        /// Maximizes the supplied LinExpr by solving multiple models sequentially.
        /// </summary>
        /// <param name="_obj"></param>
        /// <param name="_solutionCallback">Invoked for every incumbent solution.</param>
        public void Maximize(LinExpr _obj, Action? _solutionCallback = null)
            => Optimize(_obj, _solutionCallback, _minimization: false);

        /// <summary>
        /// This method can be called from a callback during optimization or enumeration to abort
        /// the optimization/enumeration early. The last solution or best-known solution will be retained.
        /// </summary>
        public void Abort()
        {
            Debug.Assert(!AbortOptimization);

            if (!InOptimization)
                throw new InvalidOperationException("Optimization/enumeration can only be aborted from a callback.");

            AbortOptimization = true;
        }

        private IEnumerable<bool[]> InvokeSampler(long _timeout, int[]? _assumptions)
        {
            try
            {
                if (Configuration.ConsoleSolverLines.HasValue && Configuration.Verbosity > 0)
                    Log.LimitOutputTo(Configuration.ConsoleSolverLines.Value);

                if (Environment.TickCount64 >= _timeout)
                    yield break;

                foreach (var sol in Configuration.Solver.RandomSample(VariableCount, _timeout, _assumptions))
                    yield return sol;
            }
            finally
            {
                if (Configuration.ConsoleSolverLines.HasValue && Configuration.Verbosity > 0)
                    Log.LimitOutputTo();
            }
        }

        private (State State, bool[]? Vars) InvokeSolver(long _timeout, int[]? _assumptions)
        {
            try
            {
                if (Configuration.ConsoleSolverLines.HasValue && Configuration.Verbosity > 0)
                    Log.LimitOutputTo(Configuration.ConsoleSolverLines.Value);

                if (Environment.TickCount64 >= _timeout)
                    return (State.Undecided, null);

                return Configuration.Solver.Solve(VariableCount, _timeout, _assumptions);
            }
            finally
            {
                if (Configuration.ConsoleSolverLines.HasValue && Configuration.Verbosity > 0)
                    Log.LimitOutputTo();
            }
        }

        /// <summary>
        /// Enumerates all valid assignment, with differing assignments for _modelVariables
        /// </summary>
        /// <param name="_modelVariables">Solutions must differ in this set of variables</param>
        /// <param name="_solutionCallback">Invoked for every valid assignment</param>
        public void EnumerateSolutions(IEnumerable<UIntVar> _modelVariables, Action _solutionCallback)
            => EnumerateSolutions(_modelVariables.SelectMany(v => v.Bits), _solutionCallback);

        /// <summary>
        /// Enumerates all valid assignment, with differing assignments for _modelVariables
        /// </summary>
        /// <param name="_modelVariables">Solutions must differ in this set of variables</param>
        /// <param name="_solutionCallback">Invoked for every valid assignment</param>
        public void EnumerateSolutions(IEnumerable<BoolExpr> _modelVariables, Action _solutionCallback)
        {
            if (State == State.Unsatisfiable && !UnsatWithAssumptions)
                return;

            var timeout = long.MaxValue;
            if (Configuration.TimeLimit != TimeSpan.Zero)
                timeout = Environment.TickCount64 + (long)Configuration.TimeLimit.TotalMilliseconds;

            try
            {
                InOptimization = true;
                AbortOptimization = false;

                var modelVariables = _modelVariables.Select(v => v.Flatten()).ToArray();
                var assumptions = new List<int>();

                bool[]? lastAssignment = null;
                for (; ; )
                {
                    (var state, var assignment) = InvokeSolver(timeout, assumptions.ToArray());

                    if (state == State.Undecided)
                        AbortOptimization = true;

                    if (assignment is null)
                        break;

                    for (var i = 0; i < assignment.Length; i++)
                        vars[i].Value = assignment[i];

                    State = State.Satisfiable;

                    var mClauses = ClauseCount;
                    _solutionCallback.Invoke();

                    if (State == State.Unsatisfiable)
                        break;

                    if (AbortOptimization)
                        break;

                    if (mClauses == ClauseCount)
                    {
                        //no new lazy-added constraints
                        lastAssignment = assignment;

                        var somethingDifferent = new BoolVar(this);
                        AddConstrInternal(somethingDifferent == OrExpr.Create(modelVariables.Select(v => v != v.X).ToArray()));
                        assumptions.Add(somethingDifferent.Id);
                    }
                    else
                    {
                        //maybe there's another way to find this assignment, respecting
                        //the lazy constraints?
                    }
                }

                if (lastAssignment is not null)
                {
                    for (var i = 0; i < lastAssignment.Length; i++)
                        vars[i].Value = lastAssignment[i];

                    State = State.Satisfiable;
                }
                else
                    State = AbortOptimization ? State.Undecided : State.Unsatisfiable;
                UnsatWithAssumptions = false;
            }
            finally
            {
                InOptimization = false;
            }
        }



        /// <summary>
        /// Returns all solutions produced by the solver. Only useful with solvers that actually
        /// produce multiple solutions directly. Only tested with CMSGen and CryptoMiniSat.
        /// </summary>
        /// <param name="_solutionCallback">Invoked for every valid assignment</param>
        public void MultiSolve(Action _solutionCallback)
        {
            //TODO integrate with enumSolutions properly

            if (State == State.Unsatisfiable && !UnsatWithAssumptions)
                return;

            var timeout = long.MaxValue;
            if (Configuration.TimeLimit != TimeSpan.Zero)
                timeout = Environment.TickCount64 + (long)Configuration.TimeLimit.TotalMilliseconds;

            try
            {
                InOptimization = true;
                AbortOptimization = false;

                State = State.Undecided;
                foreach (var assignment in InvokeSampler(timeout, null))
                {
                    for (var i = 0; i < assignment.Length; i++)
                        vars[i].Value = assignment[i];

                    State = State.Satisfiable;

                    var mClauses = ClauseCount;
                    _solutionCallback.Invoke();

                    if (State == State.Unsatisfiable)
                        break;

                    if (AbortOptimization)
                        break;

                    if (mClauses != ClauseCount)
                        throw new NotImplementedException("Lazy-added constraints not yet supported in multi sampling");
                }

                UnsatWithAssumptions = false;
            }
            finally
            {
                InOptimization = false;
            }
        }



        private void Optimize(LinExpr _obj, Action? _solutionCallback, bool _minimization)
        {
            if (State == State.Unsatisfiable && !UnsatWithAssumptions)
                return;

            var timeout = long.MaxValue;
            if (Configuration.TimeLimit != TimeSpan.Zero)
                timeout = Environment.TickCount64 + (long)Configuration.TimeLimit.TotalMilliseconds;

            try
            {
                InOptimization = true;
                AbortOptimization = false;

                if (Configuration.Verbosity > 0)
                {
                    if (_minimization)
                        Console.WriteLine($"Minimizing objective, range {-_obj.UB} - {-_obj.LB}");
                    else
                        Console.WriteLine($"Maximizing objective, range {_obj.LB} - {_obj.UB}");
                }

                bool[]? bestAssignment;
                for (; ; )
                {
                    (State, bestAssignment) = InvokeSolver(timeout, null);
                    if (State != State.Satisfiable)
                    {
                        UnsatWithAssumptions = false;
                        return;
                    }

                    Debug.Assert(bestAssignment is not null);

                    //found initial, potentially feasible solution
                    for (var i = 0; i < bestAssignment.Length; i++)
                        vars[i].Value = bestAssignment[i];

                    State = State.Satisfiable;
                    var mClauses = ClauseCount;

                    //callback might add lazy constraints or abort
                    _solutionCallback?.Invoke();

                    if (State == State.Unsatisfiable)
                    {
                        UnsatWithAssumptions = false;
                        return;
                    }

                    if (AbortOptimization)
                    {
                        if (State == State.Satisfiable && ClauseCount != mClauses)
                        {
                            State = State.Undecided;
                            UnsatWithAssumptions = false;
                        }
                        return;
                    }

                    //if it didn't, we have a feasible solution
                    if (mClauses == ClauseCount)
                        break;
                }

                //start search
                var lb = _obj.X;
                var ub = _obj.UB;
                int objGELB = _obj.LB;
                var assumptionGE = new List<int>();
                while (lb != ub && !AbortOptimization)
                {
                    if (Configuration.Verbosity > 0)
                    {
                        if (_minimization)
                            Console.WriteLine($"Minimizing objective, range {-ub} - {-lb}");
                        else
                            Console.WriteLine($"Maximizing objective, range {lb} - {ub}");
                    }

                    int cur = Configuration.OptimizationFocus switch
                    {
                        OptimizationFocus.Balanced => (lb + 1 + ub) / 2,
                        OptimizationFocus.Incumbent => lb + 1,
                        OptimizationFocus.Bound => ub,
                        _ => throw new NotImplementedException()
                    };

                    //perhaps we already added this GE constraint, and the current
                    //round is only a repetition with additional lazy constraints?
                    if (assumptionGE.Count == 0 || objGELB != cur)
                    {
                        objGELB = cur;
                        var ge = (_obj >= cur).Flatten();
                        if (ge is BoolVar v)
                            assumptionGE.Add(v.Id);
                        else if (ge is NotExpr ne && ne.inner is BoolVar iv)
                            assumptionGE.Add(-iv.Id);
                        else
                            throw new Exception();
                    }

                    (var subState, var assignment) = State == State.Unsatisfiable ? (State, null) : InvokeSolver(timeout, assumptionGE.ToArray());
                    if (subState == State.Satisfiable)
                    {
                        Debug.Assert(assignment is not null);

                        State = State.Satisfiable;
                        for (var i = 0; i < assignment.Length; i++)
                            vars[i].Value = assignment[i];

                        Debug.Assert(_obj.X >= cur);

                        //callback might add lazy constraints
                        var mClauses = ClauseCount;
                        _solutionCallback?.Invoke();

                        if (State == State.Satisfiable && ClauseCount == mClauses)
                        {
                            //no new lazy constraints
                            lb = _obj.X;
                            bestAssignment = assignment;
                        }

                        if (AbortOptimization)
                            break;
                    }
                    else if (subState == State.Unsatisfiable)
                    {
                        ub = cur - 1;
                        assumptionGE.Clear();
                    }
                    else
                    {
                        Debug.Assert(subState == State.Undecided);
                        break;
                    }

                    Debug.Assert(lb <= ub);
                }

                //restore best known solution
                State = State.Satisfiable;
                UnsatWithAssumptions = false;
                for (var i = 0; i < bestAssignment.Length; i++)
                    vars[i].Value = bestAssignment[i];
            }
            finally
            {
                InOptimization = false;
            }
        }

        /// <summary>
        /// Finds an satisfying assignment (SAT) or proves the model
        /// is not satisfiable (UNSAT) with the built-in solver.
        /// </summary>
        public void Solve(BoolExpr[]? _assumptions = null)
        {
            if (State == State.Unsatisfiable && !UnsatWithAssumptions)
                return;
            if (State == State.Satisfiable && (_assumptions is null || _assumptions.All(a => a.X)))
                return;

            var timeout = long.MaxValue;
            if (Configuration.TimeLimit != TimeSpan.Zero)
                timeout = Environment.TickCount64 + (long)Configuration.TimeLimit.TotalMilliseconds;

            int[]? assumptions = null;
            if (_assumptions is not null)
            {
                assumptions = new int[_assumptions.Length];
                for (var i = 0; i < _assumptions.Length; i++)
                    assumptions[i] = _assumptions[i] switch
                    {
                        BoolVar bv => bv.Id,
                        NotExpr ne => -ne.inner.Id,
                        BoolExpr be => be.Flatten() switch
                        {
                            BoolVar ibv => ibv.Id,
                            NotExpr ine => -ine.inner.Id,
                            _ => throw new Exception()
                        }
                    };
            }

            (State, var assignment) = InvokeSolver(timeout, assumptions);
            if (State == State.Satisfiable)
            {
                Debug.Assert(assignment is not null);
                Debug.Assert(assignment.Length == vars.Count);

                for (var i = 0; i < assignment.Length; i++)
                    vars[i].Value = assignment[i];
            }

            UnsatWithAssumptions = assumptions is not null;
        }

        /// <summary>
        /// Writes the model as DIMACS file
        /// </summary>
        /// <param name="_out"></param>
        public void Write(string _path)
        {
            using (var fo = File.CreateText(_path))
                Write(fo);
        }

        /// <summary>
        /// Serializes the model in DIMACS format into a stream
        /// </summary>
        /// <param name="_out"></param>
        public void Write(TextWriter _out)
        {
            if (DIMACSBuffer is null)
                throw new Exception("Configuration.EnableDIMACSWriting must be set");

            _out.WriteLine("c Created by SATInterface");
            _out.WriteLine($"p cnf {VariableCount} {ClauseCount}");

            DIMACSOutput!.Flush();
            DIMACSBuffer.Position = 0;
            Span<char> buf = stackalloc char[4096];
            using (var fin = new StreamReader(DIMACSBuffer, Encoding.UTF8, false, -1, true))
                while (!fin.EndOfStream)
                {
                    var bytes = fin.ReadBlock(buf);
                    _out.Write(buf[0..bytes]);
                }
        }

        /// <summary>
        /// Returns an expression equivalent to a conjunction of the supplied
        /// expressions.
        /// </summary>
        /// <param name="_elems"></param>
        /// <returns></returns>
        public BoolExpr And(IEnumerable<BoolExpr> _elems) => AndExpr.Create(_elems.ToArray());

        /// <summary>
        /// Returns an expression equivalent to a conjunction of the supplied
        /// expressions.
        /// </summary>
        /// <param name="_elems"></param>
        /// <returns></returns>
        public BoolExpr And(params BoolExpr[] _elems) => AndExpr.Create(_elems);

        /// <summary>
        /// Returns an expression equivalent to a disjunction of the supplied
        /// expressions.
        /// </summary>
        /// <param name="_elems"></param>
        /// <returns></returns>
        public BoolExpr Or(IEnumerable<BoolExpr> _elems) => OrExpr.Create(_elems.ToArray());


        /// <summary>
        /// Returns an expression equivalent to the exclusive-or of the
        /// supplied expressions.
        /// </summary>
        public BoolExpr Xor(BoolExpr _a, BoolExpr _b)
            => _a ^ _b;

        /// <summary>
        /// Returns an expression equivalent to the exclusive-or of the
        /// supplied expressions.
        /// </summary>
        public BoolExpr Xor(BoolExpr _a, BoolExpr _b, BoolExpr _c)
            => _a ^ _b ^ _c;

        /// <summary>
        /// Returns an expression equivalent to the exclusive-or of the
        /// supplied expressions.
        /// </summary>
        public BoolExpr Xor(params BoolExpr[] _elems)
        {
            if (_elems.Length == 0)
                return False;

            if (_elems.Length == 1)
                return _elems[0];

            if (_elems.Length == 2)
                return _elems[0] ^ _elems[1];

            var res = False;
            foreach (var v in _elems)
                res ^= v;
            return res;
        }

        /// <summary>
        /// Returns an expression equivalent to a disjunction of the supplied
        /// expressions.
        /// </summary>
        public BoolExpr Or(params BoolExpr[] _elems) => OrExpr.Create(_elems);

        /// <summary>
        /// Returns the sum of the supplied expressions.
        /// </summary>
        public LinExpr Sum(IEnumerable<BoolExpr> _elems) => Sum(_elems.ToArray());

        /// <summary>
        /// Returns the sum of the supplied expressions.
        /// </summary>
        public LinExpr Sum(IEnumerable<LinExpr> _elems)
        {
            var le = new LinExpr();
            foreach (var l in _elems)
                le.AddTerm(l);
            return le;
        }

        /// <summary>
        /// Returns the sum of the supplied expressions as UIntVar.
        /// </summary>
        public UIntVar SumUInt(IEnumerable<BoolExpr> _elems) => SumUInt(_elems.ToArray());

        /// <summary>
        /// Returns the sum of the supplied expressions as UIntVar.
        /// </summary>
        public UIntVar SumUInt(params BoolExpr[] _elems) => SumUInt(_elems.AsSpan());

        /// <summary>
        /// Returns the sum of the supplied expressions as UIntVar.
        /// </summary>
        public UIntVar SumUInt(ReadOnlySpan<BoolExpr> _elems)
        {
            UIntVar SumTwo(BoolExpr _a, BoolExpr _b)
            {
                if (UIntSumTwoCache.TryGetValue((_a, _b), out var res))
                    return res;
                if (UIntSumTwoCache.TryGetValue((_b, _a), out res))
                    return res;

                var carry = (_a & _b).Flatten();
                var sum = (_a ^ _b).Flatten();

                //unitprop
                if (Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.PartialArith))
                    AddConstr(OrExpr.Create(!carry, !sum));

                if (Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.FullArith))
                {
                    AddConstr(OrExpr.Create(carry, sum, !_a));
                    AddConstr(OrExpr.Create(carry, sum, !_b));
                }

                return UIntSumTwoCache[(_a, _b)]
                = new UIntVar(this, 2, new[] { sum, carry }, false);
            }

            UIntVar SumThree(BoolExpr _a, BoolExpr _b, BoolExpr _c)
            {
                if (UIntSumThreeCache.TryGetValue((_a, _b, _c), out var res))
                    return res;
                if (UIntSumThreeCache.TryGetValue((_a, _c, _b), out res))
                    return res;
                if (UIntSumThreeCache.TryGetValue((_b, _a, _c), out res))
                    return res;
                if (UIntSumThreeCache.TryGetValue((_b, _c, _a), out res))
                    return res;
                if (UIntSumThreeCache.TryGetValue((_c, _a, _b), out res))
                    return res;
                if (UIntSumThreeCache.TryGetValue((_c, _b, _a), out res))
                    return res;

                var sum = (_a ^ _b ^ _c).Flatten();
                var carry = !(AtMostOneOf(new[] { _a, _b, _c }, AtMostOneOfMethod.Pairwise).Flatten());

                //unitprop
                if (Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.FullArith))
                {
                    AddConstr(OrExpr.Create(!carry, !sum, _a));
                    AddConstr(OrExpr.Create(!carry, !sum, _b));
                    AddConstr(OrExpr.Create(!carry, !sum, _c));
                    AddConstr(OrExpr.Create(carry, sum, !_a));
                    AddConstr(OrExpr.Create(carry, sum, !_b));
                    AddConstr(OrExpr.Create(carry, sum, !_c));
                }

                return UIntSumThreeCache[(_a, _b, _c)] = new UIntVar(this, 3, new[] { sum, carry }, false);
            }

            var beCount = 0;
            var trueCount = 0;
            foreach (var e in _elems)
                if (ReferenceEquals(e, True))
                    trueCount++;
                else if (!ReferenceEquals(e, False))
                    beCount++;

            BoolExpr[]? arr = null;
            if (beCount != _elems.Length)
            {
                arr = ArrayPool<BoolExpr>.Shared.Rent(beCount);
                var i = 0;
                foreach (var e in _elems)
                    if (!ReferenceEquals(e, True) && !ReferenceEquals(e, False))
                        arr[i++] = e;
                _elems = arr.AsSpan().Slice(0, beCount);
            }

            UIntVar res;
            switch (_elems.Length)
            {
                case 0:
                    res = AddUIntConst(trueCount);
                    trueCount = 0;
                    break;
                case 1:
                    res = UIntVar.ITE(_elems[0], AddUIntConst(trueCount + 1), AddUIntConst(trueCount));
                    trueCount = 0;
                    break;
                case 2:
                    res = SumTwo(_elems[0], _elems[1]);
                    break;
                case 3:
                    res = SumThree(_elems[0], _elems[1], _elems[2]);
                    break;
                default:
                    {
                        var cnt = (_elems.Length + 2) / 3;
                        var vars = ArrayPool<UIntVar>.Shared.Rent(cnt);
                        for (var i = 0; i < cnt; i++)
                            if (i * 3 + 2 < _elems.Length)
                                vars[i] = SumThree(_elems[i * 3], _elems[i * 3 + 1], _elems[i * 3 + 2]);
                            else if (i * 3 + 1 < _elems.Length)
                                vars[i] = SumTwo(_elems[i * 3], _elems[i * 3 + 1]);
                            else
                            {
                                vars[i] = UIntVar.ITE(_elems[i * 3], AddUIntConst(trueCount + 1), AddUIntConst(trueCount));
                                trueCount = 0;
                            }
                        res = Sum(vars.AsSpan().Slice(0, cnt));

                        ArrayPool<UIntVar>.Shared.Return(vars);
                    }
                    break;
            }

            if (arr is not null)
                ArrayPool<BoolExpr>.Shared.Return(arr);

            return res + trueCount;
        }

        private Dictionary<(BoolExpr, BoolExpr), UIntVar> UIntSumTwoCache = new();
        private Dictionary<(BoolExpr, BoolExpr, BoolExpr), UIntVar> UIntSumThreeCache = new();
        internal Dictionary<(UIntVar, UIntVar), UIntVar> UIntSumCache = new();
        internal Dictionary<LinExpr, UIntVar> UIntCache = new();
        private Dictionary<int, UIntVar> UIntConstCache = new();
        internal Dictionary<(LinExpr, int), BoolExpr> LinExprEqCache = new();
        internal Dictionary<(LinExpr, int), BoolExpr> LinExprLECache = new();

        /// <summary>
        /// Returns the count of the supplied expressions.
        /// </summary>
        public LinExpr Sum(params BoolExpr[] _elems) => Sum(_elems.AsSpan());

        /// <summary>
        /// Returns the count of the supplied expressions.
        /// </summary>
        public LinExpr Sum(ReadOnlySpan<BoolExpr> _elems)
        {
            var le = new LinExpr();
            foreach (var v in _elems)
                le.AddTerm(v);
            return le;
        }

        /// <summary>
        /// Returns the sum of the supplied UIntVars.
        /// </summary>
        public UIntVar Sum(params UIntVar[] _elems) => Sum(_elems.AsSpan());

        /// <summary>
        /// Returns the sum of the supplied UIntVars.
        /// </summary>
        public UIntVar Sum(IEnumerable<UIntVar> _elems) => Sum(_elems.ToArray());

        /// <summary>
        /// Returns the sum of the supplied UIntVars.
        /// </summary>
        public UIntVar Sum(ReadOnlySpan<UIntVar> _elems)
        {
            switch (_elems.Length)
            {
                case 0:
                    return AddUIntConst(0);
                case 1:
                    return _elems[0];
                case 2:
                    return _elems[0] + _elems[1];
                default:
                    var mid = _elems.Length / 2;
                    return Sum(_elems[..mid]) + Sum(_elems[mid..]);
            }
        }

        /// <summary>
        /// If-Then-Else to pick one of two values. If _if is TRUE, _then will be picked, _else otherwise.
        /// </summary>
        /// <param name="_if"></param>
        /// <param name="_then"></param>
        /// <param name="_else"></param>
        /// <returns></returns>
        public UIntVar ITE(BoolExpr _if, UIntVar _then, UIntVar _else) => UIntVar.ITE(_if, _then, _else);


        /// <summary>
        /// If-Then-Else to pick one of two values. If _if is TRUE, _then will be picked, _else otherwise.
        /// </summary>
        /// <param name="_if"></param>
        /// <param name="_then"></param>
        /// <param name="_else"></param>
        /// <returns></returns>
        public BoolExpr ITE(BoolExpr _if, BoolExpr _then, BoolExpr _else)
        {
            _if = _if.Flatten();
            _then = _then.Flatten();
            _else = _else.Flatten();

            if (_then.Equals(_else))
                return _then;
            if (ReferenceEquals(_if, True))
                return _then;
            if (ReferenceEquals(_if, False))
                return _else;
            if (ReferenceEquals(_then, True) && ReferenceEquals(_else, False))
                return _if;
            if (ReferenceEquals(_then, False) && ReferenceEquals(_else, True))
                return !_if;

            if (ITECache.TryGetValue((_if, _then, _else), out var res))
                return res;
            if (ITECache.TryGetValue((!_if, _else, _then), out res))
                return res;
            if (ITECache.TryGetValue((_if, !_then, !_else), out res))
                return !res;
            if (ITECache.TryGetValue((!_if, !_else, !_then), out res))
                return !res;

            return ITECache[(_if, _then, _else)] = ITEInternal(_if, _then, _else);
        }

        private Dictionary<(BoolExpr _i, BoolExpr _t, BoolExpr _e), BoolExpr> ITECache = new();
        internal Dictionary<OrExpr, BoolExpr> OrCache = new();

        private BoolExpr ITEInternal(BoolExpr _if, BoolExpr _then, BoolExpr _else)
        {
            if (ReferenceEquals(_else, False))
                return (_if & _then).Flatten();
            if (ReferenceEquals(_else, True))
                return (!_if | _then).Flatten();

            if (ReferenceEquals(_then, False))
                return (!_if & _else).Flatten();
            if (ReferenceEquals(_then, True))
                return (_if | _else).Flatten();

            var x = AddVar();
            AddConstr(OrExpr.Create(!_if, !_then, x));
            AddConstr(OrExpr.Create(!_if, _then, !x));
            AddConstr(OrExpr.Create(_if, !_else, x));
            AddConstr(OrExpr.Create(_if, _else, !x));

            //unitprop
            if (Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.ITE))
            {
                AddConstr(OrExpr.Create(!_then, !_else, x));
                AddConstr(OrExpr.Create(_then, _else, !x));
            }
            return x;
        }

        /// <summary>
        /// Returns the sum of the supplied LinExprs.
        /// </summary>
        /// <param name="_count"></param>
        /// <returns></returns>
        public LinExpr Sum(params LinExpr[] _elems)
        {
            var sum = new LinExpr();
            foreach (var e in _elems)
                sum.AddTerm(e);
            return sum;
        }

        public enum ExactlyOneOfMethod
        {
            Commander,
            SortBB,
            SortPairwise,
            BinaryCount,
            TwoFactor,
            Pairwise,
            PairwiseTree,
            OneHot,
            Sequential,
            SequentialUnary
        }

        public enum AtMostOneOfMethod
        {
            Pairwise,
            PairwiseTree,
            Commander,
            SortBB,
            SortPairwise,
            OneHot,
            Sequential,
            SequentialUnary,
            BinaryCount
        }

        public enum ExactlyKOfMethod
        {
            BinaryCount,
            SortBB,
            SortPairwise,
            Pairwise,
            Sequential,
            LinExpr
        }

        /// <summary>
        /// Expression is True iff at most one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        public BoolExpr AtMostOneOf(params BoolExpr[] _expr) => AtMostOneOf(_expr.AsSpan(), null);

        /// <summary>
        /// Expression is True iff at most one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        public BoolExpr AtMostOneOf(IEnumerable<BoolExpr> _expr, AtMostOneOfMethod? _method = null) => AtMostOneOf(_expr.ToArray(), _method);

        /// <summary>
        /// Expression is True iff at most one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        public BoolExpr AtMostOneOf(BoolExpr[] _expr, AtMostOneOfMethod? _method = null) => AtMostOneOf(_expr.AsSpan(), _method);

        /// <summary>
        /// Expression is True iff at most one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        public BoolExpr AtMostOneOf(ReadOnlySpan<BoolExpr> _expr, AtMostOneOfMethod? _method = null)
        {
            var trueCount = 0;
            var falseCount = 0;
            foreach (var be in _expr)
                if (ReferenceEquals(be, True))
                    trueCount++;
                else if (ReferenceEquals(be, False))
                    falseCount++;

            if (trueCount > 1)
                return False;

            var expr = _expr;

            if (trueCount + falseCount > 0)
            {
                var newExpr = new BoolExpr[_expr.Length - trueCount - falseCount].AsSpan();
                var i = 0;
                foreach (var be in _expr)
                    if (!ReferenceEquals(be, True) && !ReferenceEquals(be, False))
                        newExpr[i++] = be;
                expr = newExpr;
            }

            if (trueCount == 1)
                return !OrExpr.Create(expr).Flatten();

            Debug.Assert(trueCount == 0);

            switch (expr.Length)
            {
                case 0:
                case 1:
                    return True;
                case 2:
                    return !expr[0] | !expr[1];
            }

            switch (_method)
            {
                case null:
                    return AtMostOneOfPairwiseTree(expr);
                case AtMostOneOfMethod.SortBB:
                    return !SortBB(expr)[1];
                case AtMostOneOfMethod.SortPairwise:
                    return !SortPairwise(expr)[1];
                case AtMostOneOfMethod.Commander:
                    return AtMostOneOfCommander(expr);
                case AtMostOneOfMethod.Pairwise:
                    return AtMostOneOfPairwise(expr);
                case AtMostOneOfMethod.PairwiseTree:
                    return AtMostOneOfPairwiseTree(expr);
                case AtMostOneOfMethod.OneHot:
                    return AtMostOneOfOneHot(expr);
                case AtMostOneOfMethod.Sequential:
                    return AtMostOneOfSequential(expr);
                case AtMostOneOfMethod.SequentialUnary:
                    return AtMostOneOfSequentialUnary(expr);
                case AtMostOneOfMethod.BinaryCount:
                    return SumUInt(expr) < 2;
                default:
                    throw new ArgumentException(nameof(_method));
            }
        }

        private BoolExpr AtMostOneOfSequential(ReadOnlySpan<BoolExpr> _expr)
        {
            var v0 = False;
            var v1 = False;
            for (var i = 0; i < _expr.Length; i++)
            {
                var v0Old = v0;
                if (i < _expr.Length - 1)
                    v0 = (_expr[i] | v0).Flatten();
                v1 = ((v0Old & _expr[i]) | v1).Flatten();
            }
            return !v1;
        }

        private BoolExpr AtMostOneOfCommander(ReadOnlySpan<BoolExpr> _expr)
        {
            return ExactlyOneOfCommander(_expr).Flatten() | AndExpr.Create(_expr.ToArray().Select(e => !e).ToArray()).Flatten();
        }

        /// <summary>
        /// Expression is True iff exactly one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        /// <param name="_expr"></param>
        /// <returns></returns>
        public BoolExpr ExactlyOneOf(params BoolExpr[] _expr) => ExactlyOneOf(_expr.AsSpan(), null);

        /// <summary>
        /// Expression is True iff exactly one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        public BoolExpr ExactlyOneOf(IEnumerable<BoolExpr> _expr, ExactlyOneOfMethod? _method = null) => ExactlyOneOf(_expr.ToArray().AsSpan(), _method);

        /// <summary>
        /// Expression is True iff exactly one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        public BoolExpr ExactlyOneOf(BoolExpr[] _expr, ExactlyOneOfMethod? _method = null) => ExactlyOneOf(_expr.ToArray().AsSpan(), _method);

        /// <summary>
        /// Expression is True iff exactly one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        public BoolExpr ExactlyOneOf(ReadOnlySpan<BoolExpr> _expr, ExactlyOneOfMethod? _method = null)
        {
            var trueCount = 0;
            var falseCount = 0;
            foreach (var be in _expr)
                if (ReferenceEquals(be, True))
                    trueCount++;
                else if (ReferenceEquals(be, False))
                    falseCount++;

            if (trueCount > 1)
                return False;

            var expr = _expr;

            if (trueCount + falseCount > 0)
            {
                var newExpr = new BoolExpr[_expr.Length - trueCount - falseCount].AsSpan();
                var i = 0;
                foreach (var be in _expr)
                    if (!ReferenceEquals(be, True) && !ReferenceEquals(be, False))
                        newExpr[i++] = be;
                expr = newExpr;
            }

            if (trueCount == 1)
                return !OrExpr.Create(expr).Flatten();

            Debug.Assert(trueCount == 0);

            switch (expr.Length)
            {
                case 0:
                    return False;
                case 1:
                    return expr[0];
                case 2:
                    return expr[0] ^ expr[1];
            }

            switch (_method)
            {
                case null:
                    return ExactlyOneOfPairwiseTree(expr);
                case ExactlyOneOfMethod.SortBB:
                    return ExactlyKOf(expr.ToArray(), 1, ExactlyKOfMethod.SortBB);
                case ExactlyOneOfMethod.SortPairwise:
                    return ExactlyKOf(expr.ToArray(), 1, ExactlyKOfMethod.SortPairwise);
                case ExactlyOneOfMethod.BinaryCount:
                    return SumUInt(expr) == 1;
                case ExactlyOneOfMethod.SequentialUnary:
                    return ExactlyOneOfSequentialUnary(expr);
                case ExactlyOneOfMethod.Sequential:
                    return ExactlyOneOfSequential(expr);
                case ExactlyOneOfMethod.Commander:
                    return ExactlyOneOfCommander(expr);
                case ExactlyOneOfMethod.TwoFactor:
                    return ExactlyOneOfTwoFactor(expr);
                case ExactlyOneOfMethod.Pairwise:
                    return ExactlyOneOfPairwise(expr);
                case ExactlyOneOfMethod.PairwiseTree:
                    return ExactlyOneOfPairwiseTree(expr);
                case ExactlyOneOfMethod.OneHot:
                    return ExactlyOneOfOneHot(expr);
                default:
                    throw new ArgumentException();
            }
        }

        private BoolExpr ExactlyOneOfOneHot(ReadOnlySpan<BoolExpr> _expr)
        {
            var ors = new BoolExpr[_expr.Length];
            for (var i = 0; i < _expr.Length; i++)
            {
                var ands = new BoolExpr[_expr.Length];
                for (var j = 0; j < _expr.Length; j++)
                    ands[j] = (i == j) ? _expr[j] : !_expr[j];
                ors[i] = AndExpr.Create(ands).Flatten();
            }
            return OrExpr.Create(ors).Flatten();
        }

        private BoolExpr AtMostOneOfOneHot(ReadOnlySpan<BoolExpr> _expr)
        {
            var cse = new BoolExpr[(_expr.Length + 3) / 4];
            for (var i = 0; i < _expr.Length; i += 4)
                cse[i / 4] = !OrExpr.Create(_expr[i..Math.Min(_expr.Length, i + 4)]).Flatten();

            var ors = new List<BoolExpr>();
            for (var i = 0; i < _expr.Length; i++)
            {
                var ands = new List<BoolExpr>();
                for (var j = 0; j < _expr.Length; j += 4)
                    if (i != j && i != (j + 1) && i != (j + 2) && i != (j + 3))
                        ands.Add(cse[j / 4]);
                    else
                    {
                        if (i != j)
                            ands.Add(!_expr[j]);
                        if (i != j + 1 && j + 1 < _expr.Length)
                            ands.Add(!_expr[j + 1]);
                        if (i != j + 2 && j + 2 < _expr.Length)
                            ands.Add(!_expr[j + 2]);
                        if (i != j + 3 && j + 3 < _expr.Length)
                            ands.Add(!_expr[j + 3]);
                    }

                ors.Add(AndExpr.Create(CollectionsMarshal.AsSpan(ands)).Flatten());
            }
            return OrExpr.Create(CollectionsMarshal.AsSpan(ors)).Flatten();
        }

        private BoolExpr ExactlyOneOfPairwise(ReadOnlySpan<BoolExpr> _expr)
        {
            var pairs = new List<BoolExpr>(_expr.Length * (_expr.Length - 1) / 2 + 1);
            for (var i = 0; i < _expr.Length; i++)
                for (var j = i + 1; j < _expr.Length; j++)
                    pairs.Add(OrExpr.Create(!_expr[i], !_expr[j]).Flatten());

            pairs.Add(OrExpr.Create(_expr).Flatten());
            return AndExpr.Create(CollectionsMarshal.AsSpan(pairs)).Flatten();
        }

        private BoolExpr AtMostOneOfPairwiseTree(ReadOnlySpan<BoolExpr> _expr)
        {
            if (_expr.Length <= 4)
                return AtMostOneOfPairwise(_expr);

            var ia = (int)(1 * _expr.Length / 4);
            var ib = (int)(2 * _expr.Length / 4);
            var ic = (int)(3 * _expr.Length / 4);
            var a = _expr[..ia];
            var b = _expr[ia..ib];
            var c = _expr[ib..ic];
            var d = _expr[ic..];

            return AndExpr.Create(
                AtMostOneOfPairwise(new[] {
                    OrExpr.Create(a).Flatten(),
                    OrExpr.Create(b).Flatten(),
                    OrExpr.Create(c).Flatten(),
                    OrExpr.Create(d).Flatten() }),
                AtMostOneOfPairwiseTree(a),
                AtMostOneOfPairwiseTree(b),
                AtMostOneOfPairwiseTree(c),
                AtMostOneOfPairwiseTree(d)).Flatten();

            //var ok = new BoolExpr[1 + (_expr.Length + Fanout - 1) / Fanout];
            //var any = new BoolExpr[(_expr.Length + Fanout - 1) / Fanout];
            //for (var i = 0; i < any.Length; i++)
            //{
            //    ok[1 + i] = AtMostOneOfPairwise(_expr[(i * Fanout)..Math.Min(_expr.Length, (i + 1) * Fanout)]);
            //    any[i] = OrExpr.Create(_expr[(i * Fanout)..Math.Min(_expr.Length, (i + 1) * Fanout)]).Flatten();
            //}

            //ok[0] = AtMostOneOfPairwiseTree(any);
            //return AndExpr.Create(ok).Flatten();
        }

        private BoolExpr ExactlyOneOfPairwiseTree(ReadOnlySpan<BoolExpr> _expr)
        {
            if (_expr.Length <= 4)
                return ExactlyOneOfPairwise(_expr);

            var ia = (int)(1 * _expr.Length / 4);
            var ib = (int)(2 * _expr.Length / 4);
            var ic = (int)(3 * _expr.Length / 4);
            var a = _expr[..ia];
            var b = _expr[ia..ib];
            var c = _expr[ib..ic];
            var d = _expr[ic..];

            return AndExpr.Create(
                ExactlyOneOfPairwise(new[] {
                    OrExpr.Create(a).Flatten(),
                    OrExpr.Create(b).Flatten(),
                    OrExpr.Create(c).Flatten(),
                    OrExpr.Create(d).Flatten() }),
                AtMostOneOfPairwiseTree(a),
                AtMostOneOfPairwiseTree(b),
                AtMostOneOfPairwiseTree(c),
                AtMostOneOfPairwiseTree(d)).Flatten();

            //var ok = new BoolExpr[1 + (_expr.Length + Fanout - 1) / Fanout];
            //var any = new BoolExpr[(_expr.Length + Fanout - 1) / Fanout];
            //for (var i = 0; i < any.Length; i++)
            //{
            //    ok[1 + i] = AtMostOneOfPairwise(_expr[(i * Fanout)..Math.Min(_expr.Length, (i + 1) * Fanout)]);
            //    any[i] = OrExpr.Create(_expr[(i * Fanout)..Math.Min(_expr.Length, (i + 1) * Fanout)]).Flatten();
            //}

            //ok[0] = ExactlyOneOfPairwiseTree(any);
            //return AndExpr.Create(ok).Flatten();
        }

        private BoolExpr AtMostOneOfPairwise(ReadOnlySpan<BoolExpr> _expr)
        {
            var pairs = new List<BoolExpr>(_expr.Length * (_expr.Length - 1) / 2);
            for (var i = 0; i < _expr.Length; i++)
                for (var j = i + 1; j < _expr.Length; j++)
                    pairs.Add(OrExpr.Create(!_expr[i], !_expr[j]).Flatten());

            return AndExpr.Create(CollectionsMarshal.AsSpan(pairs)).Flatten();
        }


        //Formulation by Chen: A New SAT Encoding of the At-Most-One Constraint
        //- https://pdfs.semanticscholar.org/11ea/d39e2799fcb85a9064037080c0f2a1733d82.pdf
        private BoolExpr ExactlyOneOfTwoFactor(ReadOnlySpan<BoolExpr> _expr)
        {
            if (_expr.Length < 6)
                return ExactlyOneOf(_expr, null);

            var W = (int)Math.Ceiling(Math.Sqrt(_expr.Length));
            var H = (int)Math.Ceiling(_expr.Length / (double)W);

            var cols = new BoolExpr[H];
            for (var y = 0; y < H; y++)
            {
                var c = new List<BoolExpr>(W);
                for (var x = 0; x < W; x++)
                {
                    var i = W * y + x;
                    if (i < _expr.Length)
                        c.Add(_expr[i]);
                }
                cols[y] = OrExpr.Create(CollectionsMarshal.AsSpan(c)).Flatten();
            }

            var rows = new BoolExpr[W];
            for (var x = 0; x < W; x++)
            {
                var c = new List<BoolExpr>(H);
                for (var y = 0; y < H; y++)
                {
                    var i = W * y + x;
                    if (i < _expr.Length)
                        c.Add(_expr[i]);
                }
                rows[x] = OrExpr.Create(CollectionsMarshal.AsSpan(c)).Flatten();
            }

            return ExactlyOneOfTwoFactor(rows).Flatten() & ExactlyOneOfTwoFactor(cols).Flatten();
        }

        /// <summary>
        /// Expression is True iff exactly K one of the supplied expressions are True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        /// <param name="_expr"></param>
        /// <param name="_k"></param>
        /// <returns></returns>
        public BoolExpr ExactlyKOf(IEnumerable<BoolExpr> _expr, int _k, ExactlyKOfMethod? _method = null)
        {
            var expr = _expr.Where(e => !ReferenceEquals(e, False)).ToArray();

            var trueCount = expr.Count(e => ReferenceEquals(e, True));
            if (trueCount > 0)
            {
                _k -= trueCount;
                expr = expr.Where(e => !ReferenceEquals(e, True)).ToArray();
            }

            if (_k < 0 || _k > expr.Length)
                return False;
            else if (_k == 0)
                return !OrExpr.Create(expr).Flatten();
            else if (_k == expr.Length)
                return AndExpr.Create(expr).Flatten();

            Debug.Assert(_k >= 1 && _k < expr.Length);

            switch (_method)
            {
                case null:
                case ExactlyKOfMethod.LinExpr:
                    return Sum(expr) == _k;

                case ExactlyKOfMethod.BinaryCount:
                    return SumUInt(expr) == _k;

                case ExactlyKOfMethod.SortBB:
                    {
                        var uc = SortBB(expr);
                        return uc[_k - 1] & !uc[_k];
                    }

                case ExactlyKOfMethod.SortPairwise:
                    {
                        var uc = SortPairwise(expr);
                        return uc[_k - 1] & !uc[_k];
                    }

                case ExactlyKOfMethod.Sequential:
                    var v = Enumerable.Repeat(False, _k + 1).ToArray();
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

                        and.Add(OrExpr.Create(CollectionsMarshal.AsSpan(or)).Flatten());
                        return AndExpr.Create(CollectionsMarshal.AsSpan(and)).Flatten();
                    }
                    else
                    {
                        var or = new BoolExpr[expr.Length];
                        for (var i = 0; i < expr.Length; i++)
                            or[i] = (expr[i]
                                & AndExpr.Create(Enumerable.Range(0, i - 1).Select(j => !expr[j]).ToArray())
                                & ExactlyKOf(Enumerable.Range(i + 1, expr.Length - i - 1).Select(j => expr[j]).ToArray(), _k - 1, _method)
                                ).Flatten();
                        return OrExpr.Create(or).Flatten();
                    }

                default:
                    throw new ArgumentException("Invalid method", nameof(_method));
            }
        }

        internal BoolExpr AtMostTwoOfSequential(IEnumerable<BoolExpr> _expr)
        {
            var expr = _expr.Where(e => !ReferenceEquals(e, False)).ToArray();

            var trueCount = expr.Count(e => ReferenceEquals(e, True));
            if (trueCount > 0)
                expr = expr.Where(e => !ReferenceEquals(e, True)).ToArray();

            switch (trueCount)
            {
                case 2:
                    return !Or(expr).Flatten();
                case 1:
                    return AtMostOneOf(expr);
                case 0:
                    var v = Enumerable.Repeat(False, 3).ToArray();
                    foreach (var e in expr)
                    {
                        var vnext = new BoolExpr[3];
                        vnext[0] = (v[0] | e).Flatten();
                        vnext[1] = ((v[0] & e) | v[1]).Flatten();
                        vnext[2] = ((v[1] & e) | v[2]).Flatten();
                        v = vnext;
                    }
                    return !v[2];
                default:
                    return False;
            }
        }

        private BoolExpr ExactlyOneOfCommander(ReadOnlySpan<BoolExpr> _expr)
        {
            //Formulation by Klieber & Kwon: Efficient CNF Encoding for Selecting 1 from N Objects  
            //- https://www.cs.cmu.edu/~wklieber/papers/2007_efficient-cnf-encoding-for-selecting-1.pdf

            if (_expr.Length <= 5)
                return ExactlyOneOfPairwise(_expr);

            var groups = _expr.ToArray().Chunk(3).ToArray();
            var commanders = new BoolExpr[groups.Length];
            var valid = new List<BoolExpr>();

            for (var i = 0; i < commanders.Length; i++)
                if (groups[i].Length == 1)
                    commanders[i] = groups[i].Single();
                else
                {
                    commanders[i] = AddVar();

                    //1
                    for (var j = 0; j < groups[i].Length; j++)
                        for (var k = j + 1; k < groups[i].Length; k++)
                            valid.Add(OrExpr.Create(!groups[i][j], !groups[i][k]).Flatten());

                    AddConstr((!commanders[i]) | OrExpr.Create(groups[i])); //2
                    AddConstr(commanders[i] | (!OrExpr.Create(groups[i]))); //3
                }

            valid.Add(ExactlyOneOfCommander(commanders).Flatten());
            return AndExpr.Create(CollectionsMarshal.AsSpan(valid)).Flatten();
        }

        private BoolExpr ExactlyOneOfSequential(ReadOnlySpan<BoolExpr> _expr)
        {
            var v0 = False;
            var v1 = False;
            foreach (var e in _expr)
            {
                var v0Old = v0;
                v0 = (e | v0).Flatten();
                v1 = ((v0Old & e) | v1).Flatten();
            }
            return v0 & !v1;
        }

        private BoolExpr ExactlyOneOfSequentialUnary(ReadOnlySpan<BoolExpr> _expr)
        {
            var prev = False;
            var valid = new List<BoolExpr>(_expr.Length + 1);
            foreach (var be in _expr)
            {
                valid.Add((!be | !prev).Flatten());
                prev = (be | prev).Flatten();
            }
            valid.Add(prev);
            return And(valid).Flatten();
        }

        private BoolExpr AtMostOneOfSequentialUnary(ReadOnlySpan<BoolExpr> _expr)
        {
            var prev = False;
            var valid = new List<BoolExpr>(_expr.Length);
            foreach (var be in _expr)
            {
                prev = prev.Flatten();

                valid.Add((!be | !prev).Flatten());
                prev = be | prev;
            }
            return And(valid).Flatten();
        }

        /// <summary>
        /// Sorts the given expressions. True will be returned first, False last.
        /// </summary>
        public BoolExpr[] SortPairwise(ReadOnlySpan<BoolExpr> _elems)
        {
            switch (_elems.Length)
            {
                case 0:
                    return Array.Empty<BoolExpr>();
                case 1:
                    return new BoolExpr[] { _elems[0] };
                case 2:
                    return new BoolExpr[] { OrExpr.Create(_elems).Flatten(), AndExpr.Create(_elems).Flatten() };
                default:
                    //adapted from https://en.wikipedia.org/wiki/Pairwise_sorting_network

                    var R = _elems.ToArray();

                    void CompSwap(int _i, int _j)
                    {
                        var a = R[_i];
                        var b = R[_j];

                        R[_i] = OrExpr.Create(a, b).Flatten();
                        R[_j] = AndExpr.Create(a, b).Flatten();
                    }

                    var a = 1;
                    for (; a < _elems.Length; a *= 2)
                    {
                        var c = 0;
                        for (var b = a; b < _elems.Length;)
                        {
                            CompSwap(b - a, b);
                            b++;
                            c++;
                            if (c >= a)
                            {
                                c = 0;
                                b += a;
                            }
                        }
                    }

                    a /= 4;
                    for (var e = 1; a > 0; a /= 2, e = 2 * e + 1)
                        for (var d = e; d > 0; d /= 2)
                        {
                            var c = 0;
                            for (var b = (d + 1) * a; b < _elems.Length;)
                            {
                                CompSwap(b - d * a, b);
                                b++;
                                c++;
                                if (c >= a)
                                {
                                    c = 0;
                                    b += a;
                                }
                            }
                        }

                    return R;
            }
        }

        /// <summary>
        /// Sorts the given expressions. True will be returned first, False last.
        /// </summary>
        public BoolExpr[] SortBB(ReadOnlySpan<BoolExpr> _elems)
        {
            //Formulation by Bailleux & Boufkhad
            //- https://pdfs.semanticscholar.org/a948/1bf4ce2b5c20d2e282dd69dcb92bddcc36c9.pdf

            switch (_elems.Length)
            {
                case 0:
                    return Array.Empty<BoolExpr>();
                case 1:
                    return new BoolExpr[] { _elems[0] };
                case 2:
                    return new BoolExpr[] { OrExpr.Create(_elems).Flatten(), AndExpr.Create(_elems).Flatten() };
                default:
                    var R = new BoolExpr[_elems.Length + 2];
                    R[0] = True;
                    for (var i = 1; i < R.Length - 1; i++)
                        R[i] = AddVar();
                    R[^1] = False;

                    var A = new BoolExpr[] { True }.Concat(SortBB(_elems[..(_elems.Length / 2)])).Append(False).ToArray();
                    var B = new BoolExpr[] { True }.Concat(SortBB(_elems[(_elems.Length / 2)..])).Append(False).ToArray();
                    for (var a = 0; a < A.Length - 1; a++)
                        for (var b = 0; b < B.Length - 1; b++)
                        {
                            var r = a + b;
                            if (r < R.Length)
                            {
                                AddConstr(OrExpr.Create(!A[a], !B[b], R[r]).Flatten());
                                AddConstr(OrExpr.Create(A[a + 1], B[b + 1], !R[r + 1]).Flatten());
                            }
                        }

                    return R[1..^1];
            }
        }

        public void Dispose()
        {
            DIMACSOutput?.Dispose();
            DIMACSBuffer?.Dispose();

            Configuration.Solver.Dispose();
        }
    }
}
