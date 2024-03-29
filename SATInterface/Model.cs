﻿global using T = System.Numerics.BigInteger;
global using VarId = System.Int32;

using System.Runtime.CompilerServices;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

[assembly: InternalsVisibleTo("Tests")]

namespace SATInterface
{
    /// <summary>
    /// Builds an environment plus associated model, containing variables and
    /// constraints, as well as solver configuration and state.
    /// </summary>
    public class Model : IDisposable //<T> : IDisposable where T : struct, IBinaryInteger<T>
    {
        public static readonly BoolExpr True = new BoolVar(null!, int.MaxValue);
        public static readonly BoolExpr False = new BoolVar(null!, int.MaxValue - 1);

        private readonly List<bool> varsX = [];

        public State State { get; internal set; } = State.Undecided;

        internal bool InOptimization = false;
        internal bool AbortOptimization = false;
        internal bool UnsatWithAssumptions = false;

        internal class Counter
        {
            internal Counter(Model _m) : this(_m, 0, 0) { }
            internal Counter(Model _m, int _v, int _c) { Model = _m; Variables = _v; Clauses = _c; }
            private readonly Model Model;
            internal int Variables;
            internal int Clauses;

            int StartVariables;
            int StartClauses;

            readonly Dictionary<int, int> Histogram = [];

            internal void Start(int _value)
            {
                StartVariables = Model.VariableCount;
                StartClauses = Model.ClauseCount;

                if (!Histogram.TryGetValue(_value, out var old))
                    old = 0;
                Histogram[_value] = old + 1;
            }

            internal void Stop()
            {
                Variables += (Model.VariableCount - StartVariables);
                Clauses += (Model.ClauseCount - StartClauses);
            }

            public override string ToString()
            {
                var res = $"{Clauses,10:N0} ({(Clauses / (double)Model.ClauseCount),4:P0}) {Variables,10:N0} ({(Variables / (double)Model.VariableCount),4:P0})";
                if (Histogram.Any())
                    res += $"       Count:{Histogram.Sum(e => e.Value)} Min:{Histogram.Keys.Min()} Med:{Histogram.MaxBy(e => e.Value).Key} Max:{Histogram.Keys.Max()}";
                return res;
            }
        }

        private readonly Dictionary<string, Counter> Statistics = [];
        private string? ActiveStatKey = null;

        [Conditional("DEBUG")]
        internal void StartStatistics(string _key, int _value)
        {
            if (ActiveStatKey is not null)
                return;

            ActiveStatKey = _key;

            if (!Statistics.TryGetValue(_key, out var s))
                s = Statistics[_key] = new(this);

            s.Start(_value);
        }

        [Conditional("DEBUG")]
        internal void StopStatistics(string _key)
        {
            if (ActiveStatKey != _key)
                return;

            Statistics[_key].Stop();
            ActiveStatKey = null;
        }

        /// <summary>
        /// The active solver configuration for this model.
        /// </summary>
        public readonly Configuration Configuration;

        internal bool GetAssignment(int _id) => _id > 0 ? varsX[_id - 1] : !varsX[-_id - 1];
        internal BoolExpr GetVariable(int _id) => new BoolVar(this, _id);

        /// <summary>
        /// Number of variables in this model.
        /// </summary>
        public int VariableCount => varsX.Count;

        /// <summary>
        /// Number of clauses in this model.
        /// </summary>
        public int ClauseCount { get; internal set; }


        private readonly FileStream? DIMACSBuffer;
        private readonly StreamWriter? DIMACSOutput;

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

        [Conditional("DEBUG")]
        public void DebugEncodingStatistics()
        {
            Debug.Assert(ActiveStatKey is null);

            Console.WriteLine($"{"",20}         {"Clauses",10}        {"Variables",10}");
            Console.WriteLine($"---------------------------------------------------------");
            var otherVariables = VariableCount;
            var otherClauses = ClauseCount;
            foreach (var s in Statistics.OrderBy(s => s.Key).Where(s => s.Value.Clauses > 0 || s.Value.Variables > 0))
            {
                Console.WriteLine($"{s.Key,20}: {s.Value}");
                otherVariables -= s.Value.Variables;
                otherClauses -= s.Value.Clauses;
            }
            Console.WriteLine($"{"Others",20}: {new Counter(this, otherVariables, otherClauses)}");
            Console.WriteLine($"---------------------------------------------------------");
            Console.WriteLine($"{"",20}  {ClauseCount,10:N0}        {VariableCount,10:N0}");
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

                AddClauseToSolver(stackalloc[] { boolVar.Id });
            }
            else if (_c is OrExpr orExpr)
            {
                if (!ReferenceEquals(orExpr.Model, this))
                    throw new ArgumentException("Mixing variables from different models is not supported.");

                if (OrCache.TryGetValue(orExpr, out var res))
                    AddClauseToSolver(stackalloc[] { res });
                else
                    AddClauseToSolver(orExpr.Elements);
            }
            else
                throw new NotImplementedException(_c.GetType().ToString());
        }

        internal void AddClauseToSolver(ReadOnlySpan<int> _x)
        {
            Configuration.Solver.AddClause(_x);
            ClauseCount++;
            DIMACSOutput?.WriteLine(string.Join(' ', _x.ToArray().Append(0)));
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
        public UIntVar AddUIntConst(T _c)
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
        public UIntVar AddUIntVar(BoolExpr[] _bits, in T? _ub = null) => new(this, _ub ?? T.One << _bits.Length, _bits, _ub.HasValue);

        /// <summary>
        /// Creates a new unsigned integer variable with the specified upper bound.
        /// </summary>
        /// <param name="_ub">Upper bound of this variable or UIntVar.Unbounded when >2^30</param>
        public UIntVar AddUIntVar(T _ub) => new(this, _ub, true);

        /// <summary>
        /// Allocate a new boolean variable. This variable takes the value True or False in a SAT model.
        /// </summary>
        public BoolExpr AddVar() => new BoolVar(this, AllocateVar());

        internal VarId AllocateVar()
        {
            if (!InOptimization && State == State.Satisfiable)
                State = State.Undecided;

            varsX.Add(false);
            return VariableCount;
        }

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

                if (_assumptions is not null)
                    Debug.Assert(_assumptions.All(a => a != 0 && Math.Abs(a) <= varsX.Count));

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
                    (var state, var assignment) = InvokeSolver(timeout, [.. assumptions]);

                    if (state == State.Undecided)
                        AbortOptimization = true;

                    if (assignment is null)
                        break;

                    for (var i = 0; i < assignment.Length; i++)
                        varsX[i] = assignment[i];

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

                        var somethingDifferent = new BoolVar(this, AllocateVar());
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
                        varsX[i] = lastAssignment[i];

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
        /// <param name="_solutionCallback">Invoked for every valid assignment. May call Abort().</param>
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
                        varsX[i] = assignment[i];

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

                //shift the objective such that binary optimization can work on bits
                var objOffset = _obj.Offset;
                foreach (var e in _obj.Weights)
                    checked
                    {
                        if (e.Value < T.Zero)
                            objOffset += e.Value;
                    }
                var obj = _obj - objOffset;
                Debug.Assert(obj.LB == 0);

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
                        varsX[i] = bestAssignment[i];

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

                //guide search towards better solutions
                if (Configuration.SetVariablePhaseFromObjective)
                    foreach (var t in _obj.Terms)
                        if (t.Var is BoolVar bv)
                            bv.SetPhase(t.Weight > 0 ? !_minimization : _minimization);

                var lb = obj.X;
                var ub = T.Min(_obj.UB - objOffset, obj.UB);
                var objGELB = T.Zero;
                var assumptions = new List<int>();
                while (lb != ub && !AbortOptimization)
                {
                    //determine common leading bits of lb and ub
                    var diffBit = checked((int)ub.GetBitLength() - 1);
                    for (; (ub >> diffBit) == (lb >> diffBit); diffBit--) ;
                    Debug.Assert(diffBit >= -1);

                    var cur = Configuration.OptimizationFocus switch
                    {
                        OptimizationFocus.Bisection => (lb + T.One + ub) >> 1,
                        OptimizationFocus.Incumbent => lb + T.One,
                        OptimizationFocus.Bound => ub,
                        OptimizationFocus.Binary => (ub >> diffBit) << diffBit,
                        _ => throw new NotImplementedException()
                    };

                    //is the current round only a repetition with additional lazy constraints,
                    //and we already added the GE constraint?
                    if (assumptions.Count == 0 || objGELB != cur)
                    {
                        Debug.Assert(cur > 0);

                        objGELB = cur;
                        var v = (BoolVar)(obj >= cur).Flatten();
                        Debug.Assert(!ReferenceEquals(v, Model.True) && !ReferenceEquals(v, Model.False));
                        assumptions.Add(v.Id);
                    }

                    if (Configuration.Verbosity > 0)
                    {
                        if (_minimization)
                            Console.WriteLine($"Minimizing objective, range {-(ub + objOffset)} - {-(lb + objOffset)}, testing {-(cur + objOffset)}");
                        else
                            Console.WriteLine($"Maximizing objective, range {(lb + objOffset)} - {(ub + objOffset)}, testing {(cur + objOffset)}");
                    }

                    (var subState, var assignment) = State == State.Unsatisfiable ? (State, null) : InvokeSolver(timeout, [.. assumptions]);

                    if (subState == State.Satisfiable)
                    {
                        Debug.Assert(assignment is not null);
                        State = State.Satisfiable;
                        for (var i = 0; i < assignment.Length; i++)
                            varsX[i] = assignment[i];

                        Debug.Assert(obj.X >= lb);
                        Debug.Assert(obj.X <= ub);
                        Debug.Assert(obj.X >= cur);

                        //callback might add lazy constraints
                        var mClauses = ClauseCount;
                        _solutionCallback?.Invoke();

                        if (State == State.Satisfiable && ClauseCount == mClauses)
                        {
                            //no new lazy constraints
                            lb = obj.X;
                            bestAssignment = assignment;
                        }

                        if (AbortOptimization)
                            break;
                    }
                    else if (subState == State.Unsatisfiable)
                    {
                        ub = cur - T.One;
                        assumptions.Clear();
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
                    varsX[i] = bestAssignment[i];
            }
            finally
            {
                if (Configuration.SetVariablePhaseFromObjective)
                    foreach (var (Var, _) in _obj.Terms)
                        if (Var is BoolVar bv)
                            bv.SetPhase(null);

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
                        BoolExpr be => be.Flatten() switch
                        {
                            BoolVar ibv => ibv.Id,
                            _ => throw new NotImplementedException()
                        }
                    };
            }

            (State, var assignment) = InvokeSolver(timeout, assumptions);
            if (State == State.Satisfiable)
            {
                Debug.Assert(assignment is not null);
                Debug.Assert(assignment.Length == varsX.Count);

                for (var i = 0; i < assignment.Length; i++)
                    varsX[i] = assignment[i];
            }

            UnsatWithAssumptions = assumptions is not null;
        }

        /// <summary>
        /// Writes the model as DIMACS file
        /// </summary>
        public void Write(string _path)
        {
            using var fo = File.CreateText(_path);
            Write(fo);
        }

        /// <summary>
        /// Serializes the model in DIMACS format into a stream
        /// </summary>
        /// <param name="_out"></param>
        public void Write(TextWriter _out)
        {
            if (DIMACSBuffer is null)
                throw new InvalidOperationException("Configuration.EnableDIMACSWriting must be set");

            _out.WriteLine("c Created by SATInterface");
            _out.WriteLine($"p cnf {VariableCount} {ClauseCount}");

            DIMACSOutput!.Flush();
            DIMACSBuffer.Position = 0;
            Span<char> buf = stackalloc char[4096];
            using var fin = new StreamReader(DIMACSBuffer, Encoding.UTF8, false, -1, true);
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
        public BoolExpr And(List<BoolExpr> _elems) => AndExpr.Create(CollectionsMarshal.AsSpan(_elems));

        /// <summary>
        /// Returns an expression equivalent to a conjunction of the supplied
        /// expressions.
        /// </summary>
        /// <param name="_elems"></param>
        /// <returns></returns>
        public BoolExpr And(ReadOnlySpan<BoolExpr> _elems) => AndExpr.Create(_elems);

        /// <summary>
        /// Returns an expression equivalent to a conjunction of the supplied
        /// expressions.
        /// </summary>
        /// <param name="_elems"></param>
        /// <returns></returns>
        [Pure]
        public BoolExpr And(params BoolExpr[] _elems) => AndExpr.Create(_elems);

        /// <summary>
        /// Returns an expression equivalent to a disjunction of the supplied
        /// expressions.
        /// </summary>
        /// <param name="_elems"></param>
        /// <returns></returns>
        [Pure]
        public BoolExpr Or(IEnumerable<BoolExpr> _elems) => OrExpr.Create(_elems.ToArray());

        /// <summary>
        /// Returns an expression equivalent to a disjunction of the supplied
        /// expressions.
        /// </summary>
        /// <param name="_elems"></param>
        /// <returns></returns>
        [Pure]
        public BoolExpr Or(List<BoolExpr> _elems) => OrExpr.Create(CollectionsMarshal.AsSpan(_elems));

        /// <summary>
        /// Returns an expression equivalent to a disjunction of the supplied
        /// expressions.
        /// </summary>
        /// <param name="_elems"></param>
        /// <returns></returns>
        [Pure]
        public BoolExpr Or(ReadOnlySpan<BoolExpr> _elems) => OrExpr.Create(_elems);

        /// <summary>
		/// Returns an expression equivalent to the exclusive-or of the
		/// supplied expressions.
		/// </summary>
		[Pure]
        public BoolExpr Xor(BoolExpr _a, BoolExpr _b) => !(_a == _b);

        /// <summary>
        /// Returns an expression equivalent to the exclusive-or of the
        /// supplied expressions.
        /// </summary>
        [Pure]
        public static BoolExpr Xor(params BoolExpr[] _elems)
        {
            static void AddXorConstr(Model _m, BoolExpr[] _elems)
            {
                Debug.Assert(_elems.Length <= 16);

                for (var v = 0; v < (1 << _elems.Length); v++)
                    if ((BitOperations.PopCount((uint)v) & 1) == 0)
                        _m.AddConstr(_m.Or(_elems.Select((be, i) => (((v >> i) & 1) == 1) ? !be : be)));
            }

            if (_elems.Contains(False))
                _elems = _elems.Where(e => !ReferenceEquals(e, False)).ToArray();

            var trueCount = _elems.Count(e => ReferenceEquals(e, True));
            if (trueCount > 0)
                _elems = _elems.Where(e => !ReferenceEquals(e, True)).ToArray();

            switch (_elems.Length)
            {
                case 0:
                    return (trueCount & 1) == 1 ? True : False;
                case 1:
                    return (trueCount & 1) == 1 ? !_elems[0] : _elems[0];
                case 2:
                    return (trueCount & 1) == 1 ? _elems[0] == _elems[1] : _elems[0] != _elems[1];
                case 3:
                case 4:
                case 5:
                case 6:
                    {
                        var m = _elems[0].GetModel()!;
                        var r = m.AddVar();
                        AddXorConstr(m, [.. _elems, !r]);
                        return (trueCount & 1) == 1 ? !r : r;
                    }
                default:
                    return Xor(_elems.Chunk(4).Select(ch => Xor(ch)).ToArray());
            }
        }

        /// <summary>
        /// Returns an expression equivalent to a disjunction of the supplied
        /// expressions.
        /// </summary>
        [Pure]
        public BoolExpr Or(params BoolExpr[] _elems) => OrExpr.Create(_elems);

        /// <summary>
        /// Returns the sum of the supplied expressions.
        /// </summary>
        [Pure]
        public LinExpr Sum(IEnumerable<BoolExpr> _elems) => Sum(_elems.ToArray());

        /// <summary>
        /// Returns the sum of the supplied expressions.
        /// </summary>
        public LinExpr Sum(List<BoolExpr> _elems) => Sum(CollectionsMarshal.AsSpan(_elems));

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
        public UIntVar SumUInt(List<BoolExpr> _elems) => SumUInt(CollectionsMarshal.AsSpan(_elems));

        /// <summary>
        /// Returns the sum of the supplied expressions as UIntVar.
        /// </summary>
        public UIntVar SumUInt(params BoolExpr[] _elems) => SumUInt(_elems.AsSpan());

        /// <summary>
        /// Returns the sum of the supplied expressions as UIntVar.
        /// </summary>
        public UIntVar SumUInt(ReadOnlySpan<BoolExpr> _elems)
        {
            BoolExpr[]? arr = null;
            try
            {
                StartStatistics("UInt BitSum", _elems.Length);

                UIntVar SumTwo(BoolVar _a, BoolVar _b)
                {
                    if (_a.Id > _b.Id)
                        (_a, _b) = (_b, _a);

                    if (UIntSumTwoCache.TryGetValue((_a, _b), out var res))
                        return res;

                    var carry = (_a & _b).Flatten();
                    var sum = (_a ^ _b).Flatten();

                    //unitprop
                    if (Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.FullArith))
                    {
                        AddConstr(OrExpr.Create(carry, sum, !_a));
                        AddConstr(OrExpr.Create(carry, sum, !_b));
                        AddConstr(OrExpr.Create(!carry, !sum));
                    }
                    else if (Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.PartialArith))
                        AddConstr(OrExpr.Create(!carry, !sum));

                    return UIntSumTwoCache[(_a, _b)] = new UIntVar(this, T.CreateChecked(2), [sum, carry], false);
                }

                UIntVar SumThree(BoolVar _a, BoolVar _b, BoolVar _c)
                {
                    if (_a.Id > _b.Id)
                        (_a, _b) = (_b, _a);
                    if (_a.Id > _c.Id)
                        (_a, _c) = (_c, _a);
                    if (_b.Id > _c.Id)
                        (_b, _c) = (_c, _b);

                    if (UIntSumThreeCache.TryGetValue((_a, _b, _c), out var res))
                        return res;

                    var sum = Xor(_a, _b, _c).Flatten();
                    var carry = OrExpr.Create(_a & _b, _a & _c, _b & _c).Flatten();

                    //unitprop
                    if (Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.FullArith))
                    {
                        AddConstr(OrExpr.Create(!sum, _a, _b, _c));
                        AddConstr(OrExpr.Create(!carry, !sum, _a));
                        AddConstr(OrExpr.Create(!carry, !sum, _b));
                        AddConstr(OrExpr.Create(!carry, !sum, _c));
                        AddConstr(OrExpr.Create(carry, sum, !_a));
                        AddConstr(OrExpr.Create(carry, sum, !_b));
                        AddConstr(OrExpr.Create(carry, sum, !_c));
                    }

                    return UIntSumThreeCache[(_a, _b, _c)] = new UIntVar(this, T.CreateChecked(3), [sum, carry], false);
                }

                var beCount = 0;
                var trueCount = T.Zero;
                foreach (var e in _elems)
                    if (ReferenceEquals(e, True))
                        trueCount++;
                    else if (!ReferenceEquals(e, False))
                        beCount++;

                if (beCount != _elems.Length)
                {
                    arr = ArrayPool<BoolExpr>.Shared.Rent(beCount);
                    var i = 0;
                    foreach (var e in _elems)
                        if (!ReferenceEquals(e, True) && !ReferenceEquals(e, False))
                            arr[i++] = e;
                    _elems = arr.AsSpan()[..beCount];
                }

                UIntVar res;
                if (_elems.Length == 0)
                    res = AddUIntConst(T.Zero);
                else
                    res = Sum(_elems.ToArray().Chunk(Configuration.SumBitsInChunksOf7 ? 7 : 3).Select(chunk =>
                    {
                        switch (chunk.Length)
                        {
                            case 1:
                                return UIntVar.ITE(chunk[0], AddUIntConst(T.One), AddUIntConst(0));
                            case 2:
                                return SumTwo((BoolVar)chunk[0].Flatten(), (BoolVar)chunk[1].Flatten());
                            case 3:
                                return SumThree((BoolVar)chunk[0].Flatten(), (BoolVar)chunk[1].Flatten(), (BoolVar)chunk[2].Flatten());
                            default:
                                chunk = SortTotalizer(chunk);
                                return new UIntVar(this, chunk.Length,
                                [
                                    Xor(chunk),
                                    Xor(chunk[1], chunk[3], chunk.Length>5 ? chunk[5] : False),
                                    chunk.Length>3 ? chunk[3] : False
                                ], false);
                        }
                    }));

                return res + AddUIntConst(trueCount);
            }
            finally
            {
                if (arr is not null)
                    ArrayPool<BoolExpr>.Shared.Return(arr);

                StopStatistics("UInt BitSum");
            }
        }


        class IgnoreLinExprOffsetComparer : IEqualityComparer<LinExpr>
        {
            public bool Equals(LinExpr? _x, LinExpr? _y)
            {
                if (_x is null && _y is null)
                    return true;
                if (_x is null)
                    return false;
                if (_y is null)
                    return false;

                if (_x.Weights.Count != _y.Weights.Count)
                    return false;

                return _x.Weights.OrderBy(e => e.Key).SequenceEqual(_y.Weights.OrderBy(e => e.Key));
            }

            public int GetHashCode([DisallowNull] LinExpr _x)
            {
                var hc = new HashCode();
                foreach (var e in _x.Weights.OrderBy(e => e.Key))
                {
                    hc.Add(e.Key);
                    hc.Add(e.Value);
                }
                return hc.ToHashCode();
            }
        }

        class IgnoreLinExprOffsetTupleComparer : IEqualityComparer<(LinExpr, T)>
        {
            public bool Equals((LinExpr, T) _x, (LinExpr, T) _y)
            {
                if (_x.Item2 != _y.Item2)
                    return false;

                if (_x.Item1.Weights.Count != _y.Item1.Weights.Count)
                    return false;

                return _x.Item1.Weights.OrderBy(e => e.Key).SequenceEqual(_y.Item1.Weights.OrderBy(e => e.Key));
            }

            public int GetHashCode([DisallowNull] (LinExpr, T) _x)
            {
                var hc = new HashCode();
                hc.Add(_x.Item2);
                foreach (var e in _x.Item1.Weights.OrderBy(e => e.Key))
                {
                    hc.Add(e.Key);
                    hc.Add(e.Value);
                }
                return hc.ToHashCode();
            }
        }

        class IntArrayComparer : IEqualityComparer<int[]>
        {
            public bool Equals(int[]? _x, int[]? _y)
            {
                if (_x is null && _y is null)
                    return true;
                if (_x is null)
                    return false;
                if (_y is null)
                    return false;

                return MemoryExtensions.SequenceEqual(_x.AsSpan(), _y.AsSpan());
            }

            public int GetHashCode([DisallowNull] int[] _x)
            {
                var hc = new HashCode();
                foreach (var x in _x)
                    hc.Add(x);
                return hc.ToHashCode();
            }
        }

        private readonly Dictionary<(BoolExpr, BoolExpr), UIntVar> UIntSumTwoCache = [];
        private readonly Dictionary<(BoolExpr, BoolExpr, BoolExpr), UIntVar> UIntSumThreeCache = [];
        internal readonly Dictionary<(UIntVar, UIntVar), UIntVar> UIntSumCache = [];
        internal readonly Dictionary<LinExpr, UIntVar> UIntCache = new(new IgnoreLinExprOffsetComparer());
        private readonly Dictionary<T, UIntVar> UIntConstCache = [];
        internal readonly Dictionary<(LinExpr, T), BoolExpr> LinExprEqCache = new(new IgnoreLinExprOffsetTupleComparer());
        internal readonly Dictionary<(LinExpr, T), BoolExpr> LinExprLECache = new(new IgnoreLinExprOffsetTupleComparer());
        private readonly Dictionary<int[], BoolExpr[]> SortCache = new(new IntArrayComparer());

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
            try
            {
                StartStatistics("UInt Sum", _elems.Length);

                switch (_elems.Length)
                {
                    case 0:
                        return AddUIntConst(T.Zero);
                    case 1:
                        return _elems[0];
                    case 2:
                        return _elems[0] + _elems[1];
                    default:
                        var mid = _elems.Length / 2;
                        return Sum(_elems[..mid]) + Sum(_elems[mid..]);
                }
            }
            finally
            {
                StopStatistics("UInt Sum");
            }
        }

        /// <summary>
        /// If-Then-Else to pick one of two values. If _if is TRUE, _then will be picked, _else otherwise.
        /// </summary>
        /// <param name="_if"></param>
        /// <param name="_then"></param>
        /// <param name="_else"></param>
        /// <returns></returns>
        [Pure]
        public UIntVar ITE(BoolExpr _if, UIntVar _then, UIntVar _else) => UIntVar.ITE(_if, _then, _else);


        /// <summary>
        /// If-Then-Else to pick one of two values. If _if is TRUE, _then will be picked, _else otherwise.
        /// </summary>
        /// <param name="_if"></param>
        /// <param name="_then"></param>
        /// <param name="_else"></param>
        /// <returns></returns>
        [Pure]
        public BoolExpr ITE(BoolExpr _if, BoolExpr _then, BoolExpr _else)
        {
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

            _if = _if.Flatten();
            _then = _then.Flatten();
            _else = _else.Flatten();

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

        private readonly Dictionary<(BoolExpr _i, BoolExpr _t, BoolExpr _e), BoolExpr> ITECache = [];
        internal readonly Dictionary<OrExpr, int> OrCache = [];

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
            /// <summary>
            /// Klieber, W. and Kwon, G., 2007, July. Efficient CNF encoding for selecting 1 from n objects. In Proc. International Workshop on Constraints in Formal Verification (p. 14).
            /// </summary>
            Commander,

            /// <summary>
            /// Bailleux, O. and Boufkhad, Y., 2003, September. Efficient CNF encoding of boolean cardinality constraints. In International conference on principles and practice of constraint programming (pp. 108-122). Berlin, Heidelberg: Springer Berlin Heidelberg.
            /// </summary>
            SortTotalizer,

            /// <summary>
            /// Parberry, I., 1992. The pairwise sorting network. Parallel Processing Letters, 2(02n03), pp.205-211.
            /// </summary>
            SortPairwise,
            BinaryCount,

            /// <summary>
            /// Chen, J., 2010. A new SAT encoding of the at-most-one constraint. Proc. constraint modelling and reformulation, p.8.
            /// </summary>
            TwoFactor,
            Pairwise,
            PairwiseTree,
            OneHot,

            /// <summary>
            /// Sinz, C., 2005, October. Towards an optimal CNF encoding of boolean cardinality constraints. In International conference on principles and practice of constraint programming (pp. 827-831). Berlin, Heidelberg: Springer Berlin Heidelberg.
            /// </summary>
            Sequential
        }

        public enum AtMostOneOfMethod
        {
            Pairwise,
            PairwiseTree,

            /// <summary>
            /// Klieber, W. and Kwon, G., 2007, July. Efficient CNF encoding for selecting 1 from n objects. In Proc. International Workshop on Constraints in Formal Verification (p. 14).
            /// </summary>
            Commander,

            /// <summary>
            /// Bailleux, O. and Boufkhad, Y., 2003, September. Efficient CNF encoding of boolean cardinality constraints. In International conference on principles and practice of constraint programming (pp. 108-122). Berlin, Heidelberg: Springer Berlin Heidelberg.
            /// </summary>
            SortTotalizer,

            /// <summary>
            /// Parberry, I., 1992. The pairwise sorting network. Parallel Processing Letters, 2(02n03), pp.205-211.
            /// </summary>
            SortPairwise,
            OneHot,

            /// <summary>
            /// Sinz, C., 2005, October. Towards an optimal CNF encoding of boolean cardinality constraints. In International conference on principles and practice of constraint programming (pp. 827-831). Berlin, Heidelberg: Springer Berlin Heidelberg.
            /// </summary>
            Sequential,
            BinaryCount,

            /// <summary>
            /// Slide 5 in https://www.cs.upc.edu/~erodri/webpage/cps/theory/sat/encodings/slides.pdf
            /// </summary>
            Heule
        }

        public enum KOfMethod
        {
            BinaryCount,

            /// <summary>
            /// Bailleux, O. and Boufkhad, Y., 2003, September. Efficient CNF encoding of boolean cardinality constraints. In International conference on principles and practice of constraint programming (pp. 108-122). Berlin, Heidelberg: Springer Berlin Heidelberg.
            /// </summary>
            SortTotalizer,

            /// <summary>
            /// Parberry, I., 1992. The pairwise sorting network. Parallel Processing Letters, 2(02n03), pp.205-211.
            /// </summary>
            SortPairwise,
            Sequential,
            LinExpr
        }

        /// <summary>
        /// Expression is True iff at most one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        [Pure]
        public BoolExpr AtMostOneOf(params BoolExpr[] _expr) => AtMostOneOf(_expr.AsSpan(), null);

        /// <summary>
        /// Expression is True iff at most one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        [Pure]
        public BoolExpr AtMostOneOf(IEnumerable<BoolExpr> _expr, AtMostOneOfMethod? _method = null) => AtMostOneOf(_expr.ToArray(), _method);

        /// <summary>
        /// Expression is True iff at most one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        [Pure]
        public BoolExpr AtMostOneOf(List<BoolExpr> _expr, AtMostOneOfMethod? _method = null) => AtMostOneOf(CollectionsMarshal.AsSpan(_expr), _method);

        /// <summary>
        /// Expression is True iff at most one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        [Pure]
        public BoolExpr AtMostOneOf(BoolExpr[] _expr, AtMostOneOfMethod? _method = null) => AtMostOneOf(_expr.AsSpan(), _method);

        /// <summary>
        /// Expression is True iff at most one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        [Pure]
        public BoolExpr AtMostOneOf(ReadOnlySpan<BoolExpr> _expr, AtMostOneOfMethod? _method = null)
        {
            try
            {
                StartStatistics($"AMO {_method}", _expr.Length);
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
                        if (expr.Length <= Configuration.TotalizerLimit)
                            return !SortTotalizer(expr)[1];
                        else
                            return AtMostOneOfPairwiseTree(expr);
                    case AtMostOneOfMethod.SortTotalizer:
                        return !SortTotalizer(expr)[1];
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
                    case AtMostOneOfMethod.BinaryCount:
                        return SumUInt(expr) < T.CreateChecked(2);
                    case AtMostOneOfMethod.Heule:
                        return AtMostOneOfHeule(expr);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(_method), $"Invalid method specified");
                }
            }
            finally
            {
                StopStatistics($"AMO {_method}");
            }
        }

        private BoolExpr AtMostOneOfSequential(ReadOnlySpan<BoolExpr> _expr)
        {
            var v0 = False;
            var v1 = False;
            for (var i = 0; i < _expr.Length; i++)
            {
                var v0Old = v0.Flatten();
                v0 = _expr[i] | v0Old;
                v1 = ((v0Old & _expr[i]) | v1).Flatten();
            }
            return !v1;
        }

        private BoolExpr AtMostOneOfHeule(ReadOnlySpan<BoolExpr> _expr)
        {
            //inspired by https://www.cs.upc.edu/~erodri/webpage/cps/theory/sat/encodings/slides.pdf, pg5
            if (_expr.Length <= 3)
                return AtMostOneOfPairwise(_expr);

            var a = _expr[..(_expr.Length / 2)];
            var b = _expr[(_expr.Length / 2)..];
            return (AtMostOneOfHeule(a) & !Or(b).Flatten()) | (!Or(a).Flatten() & AtMostOneOfHeule(b)).Flatten();
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
        [Pure]
        public BoolExpr ExactlyOneOf(IEnumerable<BoolExpr> _expr, ExactlyOneOfMethod? _method = null) => ExactlyOneOf(_expr.ToArray().AsSpan(), _method);

        /// <summary>
        /// Expression is True iff exactly one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        [Pure]
        public BoolExpr ExactlyOneOf(List<BoolExpr> _expr, ExactlyOneOfMethod? _method = null) => ExactlyOneOf(CollectionsMarshal.AsSpan(_expr), _method);

        /// <summary>
        /// Expression is True iff exactly one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        [Pure]
        public BoolExpr ExactlyOneOf(BoolExpr[] _expr, ExactlyOneOfMethod? _method = null) => ExactlyOneOf(_expr.ToArray().AsSpan(), _method);

        /// <summary>
        /// Expression is True iff exactly one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        [Pure]
        public BoolExpr ExactlyOneOf(ReadOnlySpan<BoolExpr> _expr, ExactlyOneOfMethod? _method = null)
        {
            try
            {
                StartStatistics($"EOO {_method}", _expr.Length);

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
                    case 3:
                        var a = expr[0].Flatten();
                        var b = expr[1].Flatten();
                        var c = expr[2].Flatten();

                        return Or(
                            And(a, !b, !c).Flatten(),
                            And(!a, b, !c).Flatten(),
                            And(!a, !b, c).Flatten());
                }

                switch (_method)
                {
                    case null:
                        if (expr.Length <= Configuration.TotalizerLimit)
                        {
                            var uc = SortTotalizer(expr);
                            return uc[0] & !uc[1];
                        }
                        else
                            return ExactlyOneOfCommander(expr);
                    case ExactlyOneOfMethod.SortTotalizer:
                        return ExactlyKOf(expr.ToArray(), 1, KOfMethod.SortTotalizer);
                    case ExactlyOneOfMethod.SortPairwise:
                        return ExactlyKOf(expr.ToArray(), 1, KOfMethod.SortPairwise);
                    case ExactlyOneOfMethod.BinaryCount:
                        return SumUInt(expr) == T.One;
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
                        throw new ArgumentOutOfRangeException(nameof(_method), $"Invalid method specified");
                }
            }
            finally
            {
                StopStatistics($"EOO {_method}");
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
                AtMostOneOfPairwise([
                    OrExpr.Create(a).Flatten(),
                    OrExpr.Create(b).Flatten(),
                    OrExpr.Create(c).Flatten(),
                    OrExpr.Create(d).Flatten() ]),
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
                ExactlyOneOfPairwise([
                    OrExpr.Create(a).Flatten(),
                    OrExpr.Create(b).Flatten(),
                    OrExpr.Create(c).Flatten(),
                    OrExpr.Create(d).Flatten() ]),
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


        private BoolExpr ExactlyOneOfTwoFactor(ReadOnlySpan<BoolExpr> _expr)
        {
            //Formulation by Chen: A New SAT Encoding of the At-Most-One Constraint
            //- https://pdfs.semanticscholar.org/11ea/d39e2799fcb85a9064037080c0f2a1733d82.pdf

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
        /// <param name="_method">Specific SAT encoding of the constraint</param>
        /// <returns></returns>
        [Pure]
        public BoolExpr ExactlyKOf(IEnumerable<BoolExpr> _expr, int _k, KOfMethod? _method = null)
        {
            try
            {
                StartStatistics($"EKO {_method}", _expr.Count());

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
                    case KOfMethod.LinExpr:
                        return Sum(expr) == T.CreateChecked(_k);

                    case KOfMethod.BinaryCount:
                        return SumUInt(expr) == T.CreateChecked(_k);

                    case KOfMethod.SortTotalizer:
                        {
                            var uc = SortTotalizer(expr);
                            return uc[_k - 1] & !uc[_k];
                        }

                    case KOfMethod.SortPairwise:
                        {
                            var uc = SortPairwise(expr);
                            return uc[_k - 1] & !uc[_k];
                        }

                    case KOfMethod.Sequential:
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

                    default:
                        throw new ArgumentOutOfRangeException(nameof(_method), "Invalid method");
                }
            }
            finally
            {
                StopStatistics($"EKO {_method}");
            }
        }


        /// <summary>
        /// Expression is True iff at most K of the supplied expressions are True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        /// <param name="_expr"></param>
        /// <param name="_k"></param>
        /// <param name="_method">Specific SAT encoding of the constraint</param>
        /// <returns></returns>
        [Pure]
        internal BoolExpr AtMostKOf(IEnumerable<BoolExpr> _expr, int _k, KOfMethod? _method = null)
        {
            try
            {
                StartStatistics($"AMK {_method}", _expr.Count());

                var expr = _expr.Where(e => !ReferenceEquals(e, False)).ToArray();

                var trueCount = expr.Count(e => ReferenceEquals(e, True));
                if (trueCount > 0)
                {
                    _k -= trueCount;
                    expr = expr.Where(e => !ReferenceEquals(e, True)).ToArray();
                }

                if (_k < 0)
                    return False;
                else if (_k == 0)
                    return !Or(expr).Flatten();
                else if (_k == expr.Length - 1)
                    return Or(expr.Select(e => !e)).Flatten();
                else if (_k >= expr.Length)
                    return True;

                Debug.Assert(_k >= 1 && _k < expr.Length);

                switch (_method)
                {
                    case null:
                    case KOfMethod.LinExpr:
                        return Sum(expr) <= T.CreateChecked(_k);

                    case KOfMethod.BinaryCount:
                        return SumUInt(expr) <= T.CreateChecked(_k);

                    case KOfMethod.SortTotalizer:
                        return !SortTotalizer(expr)[_k];

                    case KOfMethod.SortPairwise:
                        return !SortPairwise(expr)[_k];

                    case KOfMethod.Sequential:
                        var v = Enumerable.Repeat(False, _k + 1).ToArray();
                        foreach (var e in expr)
                        {
                            var vnext = new BoolExpr[_k + 1];
                            vnext[0] = (v[0] | e).Flatten();
                            for (var i = 1; i < _k + 1; i++)
                                vnext[i] = ((v[i - 1] & e) | v[i]).Flatten();
                            v = vnext;
                        }
                        return !v[_k];

                    default:
                        throw new ArgumentOutOfRangeException(nameof(_method), "Invalid method");
                }
            }
            finally
            {
                StopStatistics($"AMK {_method}");
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
                    //1
                    for (var j = 0; j < groups[i].Length; j++)
                        for (var k = j + 1; k < groups[i].Length; k++)
                            valid.Add(OrExpr.Create(!groups[i][j], !groups[i][k]).Flatten());

                    //2 3
                    commanders[i] = OrExpr.Create(groups[i]).Flatten();
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
                var v0Old = v0.Flatten();
                v0 = e | v0Old;
                v1 = ((v0Old & e) | v1).Flatten();
            }
            return v0.Flatten() & !v1;
        }

        /// <summary>
        /// Sorts the given expressions. True will be returned first, False last.
        /// </summary>
        public BoolExpr[] SortPairwise(ReadOnlySpan<BoolExpr> _elems)
        {
            switch (_elems.Length)
            {
                case 0:
                    return [];
                case 1:
                    return [_elems[0]];
                case 2:
                    return [OrExpr.Create(_elems).Flatten(), AndExpr.Create(_elems).Flatten()];
                default:
                    var R = new BoolExpr[_elems.Length];
                    var cacheKey = new int[_elems.Length];
                    for (var i = 0; i < _elems.Length; i++)
                    {
                        R[i] = _elems[i].Flatten();
                        cacheKey[i] = ((BoolVar)R[i]).Id;
                    }
                    Array.Sort(cacheKey);
                    if (SortCache.TryGetValue(cacheKey, out var res))
                        return res;

                    void CompSwap(int _i, int _j)
                    {
                        var a = R[_i];
                        var b = R[_j];

                        R[_i] = OrExpr.Create(a, b).Flatten();
                        R[_j] = AndExpr.Create(a, b).Flatten();
                    }

                    //adapted from https://en.wikipedia.org/wiki/Pairwise_sorting_network
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

                    if (Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.SortingNetworks))
                        for (var i = 1; i < R.Length; i++)
                            AddConstr(!R[i] | R[i - 1]);

                    return SortCache[cacheKey] = R;
            }
        }

        /// <summary>
        /// Sorts the given expressions. True will be returned first, False last.
        /// </summary>
        public BoolExpr[] SortTotalizer(ReadOnlySpan<BoolExpr> _elems)
        {
            //Formulation by Bailleux & Boufkhad
            //- https://pdfs.semanticscholar.org/a948/1bf4ce2b5c20d2e282dd69dcb92bddcc36c9.pdf

            try
            {
                StartStatistics("Sort Total", _elems.Length);
                switch (_elems.Length)
                {
                    case 0:
                        return [];
                    case 1:
                        return [_elems[0]];
                    case 2:
                        return [OrExpr.Create(_elems).Flatten(), AndExpr.Create(_elems).Flatten()];
                    default:
                        var elems = _elems.ToArray();
                        var cacheKey = new int[_elems.Length];
                        for (var i = 0; i < _elems.Length; i++)
                        {
                            elems[i] = _elems[i].Flatten();
                            cacheKey[i] = ((BoolVar)elems[i]).Id;
                        }
                        Array.Sort(cacheKey);
                        if (SortCache.TryGetValue(cacheKey, out var res))
                            return res;


                        var R = new BoolExpr[elems.Length + 2];
                        R[0] = True;
                        for (var i = 1; i < R.Length - 1; i++)
                            R[i] = AddVar();
                        R[^1] = False;

                        var A = new BoolExpr[] { True }.Concat(SortTotalizer(elems.AsSpan()[..(elems.Length / 2)])).Append(False).ToArray();
                        var B = new BoolExpr[] { True }.Concat(SortTotalizer(elems.AsSpan()[(elems.Length / 2)..])).Append(False).ToArray();
                        for (var a = 0; a < A.Length - 1; a++)
                            for (var b = 0; b < B.Length - 1; b++)
                            {
                                var r = a + b;
                                if (r < R.Length)
                                {
                                    AddConstr(OrExpr.Create(!A[a], !B[b], R[r]));
                                    AddConstr(OrExpr.Create(A[a + 1], B[b + 1], !R[r + 1]));
                                }
                            }

                        if (Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.SortingNetworks))
                            for (var i = 2; i < R.Length - 1; i++)
                                AddConstr(!R[i] | R[i - 1]);

                        return SortCache[cacheKey] = R[1..^1];
                }
            }
            finally
            {
                StopStatistics("Sort Total");
            }
        }

        public void Dispose()
        {
            DIMACSOutput?.Dispose();
            DIMACSBuffer?.Dispose();

            Configuration.Solver.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
