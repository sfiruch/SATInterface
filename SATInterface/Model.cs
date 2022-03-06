using SATInterface.Solver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
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
        public static readonly BoolExpr True = new BoolVar("true", -1);
        public static readonly BoolExpr False = new BoolVar("false", -2);

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
                foreach (var e in andExpr.elements)
                    AddConstrInternal(e);
            }
            else if (_c is BoolVar boolVar)
            {
                if (!ReferenceEquals(boolVar.Model, this))
                    throw new ArgumentException("Mixing variables from different models is not supported.");
                AddClauseToSolver(stackalloc[] { boolVar.Id });
                ClauseCount++;
            }
            else if (_c is NotExpr notExpr)
            {
                if (!ReferenceEquals(notExpr.inner.Model, this))
                    throw new ArgumentException("Mixing variables from different models is not supported.");
                AddClauseToSolver(stackalloc[] { -notExpr.inner.Id });
                ClauseCount++;
            }
            else if (_c is OrExpr orExpr)
            {
                if (orExpr.EnumVars().Any(v => !ReferenceEquals(v.Model, this)))
                    throw new ArgumentException("Mixing variables from different models is not supported.");

                //if (orExpr.elements.Length >= 8)
                //{
                //    AddConstrInternal(
                //        Or(orExpr.elements.Take(orExpr.elements.Length/2)).Flatten()
                //        | Or(orExpr.elements.Skip(orExpr.elements.Length / 2)).Flatten());
                //    return;
                //}

                var sb = orExpr.elements.Select(e => e switch
                    {
                        BoolVar bv => bv.Id,
                        NotExpr ne => -ne.inner.Id,
                        _ => throw new NotImplementedException(e.GetType().ToString())
                    }).ToArray();

                AddClauseToSolver(sb);
                ClauseCount++;
            }
            else
                throw new NotImplementedException(_c.GetType().ToString());
        }

        private void AddClauseToSolver(Span<int> _x)
        {
            Configuration.Solver.AddClause(_x);
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
        public UIntVar AddUIntConst(int _c) => UIntVar.Const(this, _c);

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
        /// <param name="_name"></param>
        /// <returns></returns>
        public BoolExpr AddVar(string? _name = null) => new BoolVar(this, _name);

        /// <summary>
        /// Allocate a new one-dimensional array of boolean variables.
        /// </summary>
        /// <param name="_name"></param>
        /// <returns></returns>
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
        /// <param name="_name"></param>
        /// <returns></returns>
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
        /// <param name="_name"></param>
        /// <returns></returns>
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
        /// <param name="_name"></param>
        /// <returns></returns>
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
            => Maximize(-_obj, _solutionCallback, _minimization: true);

        /// <summary>
        /// Maximizes the supplied LinExpr by solving multiple models sequentially.
        /// </summary>
        /// <param name="_obj"></param>
        /// <param name="_solutionCallback">Invoked for every incumbent solution.</param>
        public void Maximize(LinExpr _obj, Action? _solutionCallback = null)
            => Maximize(_obj, _solutionCallback, _minimization: false);

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

        private (State State, bool[]? Vars) InvokeSolver(long _timeout, int[]? _assumptions)
        {
            try
            {
                if (Configuration.ConsoleSolverLines.HasValue && Configuration.Verbosity > 0)
                    Log.LimitOutputTo(Configuration.ConsoleSolverLines.Value);

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
        /// <param name="_modelVariables"></param>
        /// <param name="_solutionCallback">Invoked for every valid assignment</param>
        public void EnumerateSolutions(IEnumerable<UIntVar> _modelVariables, Action _solutionCallback)
            => EnumerateSolutions(_modelVariables.SelectMany(v => v.Bits), _solutionCallback);

        /// <summary>
        /// Enumerates all valid assignment, with differing assignments for _modelVariables
        /// </summary>
        /// <param name="_modelVariables"></param>
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
                        AddConstrInternal(somethingDifferent == Or(modelVariables.Select(v => v != v.X)));
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

        private void Maximize(LinExpr _obj, Action? _solutionCallback, bool _minimization)
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
                int objGELB = 0;
                BoolVar? objGE = null;
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

                    //prehaps we already added this GE var, and the current
                    //round is only a repetition with additional lazy constraints?
                    if (objGE is null || objGELB != cur)
                    {
                        objGELB = cur;
                        objGE = new BoolVar(this);
                        AddConstrInternal(objGE == (_obj >= cur));
                    }

                    (var subState, var assignment) = State==State.Unsatisfiable ? (State,null) : InvokeSolver(timeout, new[] { objGE.Id });
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
                    else if(subState == State.Unsatisfiable)
                    {
                        ub = cur - 1;
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

            var timeout = long.MaxValue;
            if (Configuration.TimeLimit != TimeSpan.Zero)
                timeout = Environment.TickCount64 + (long)Configuration.TimeLimit.TotalMilliseconds;

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
        public BoolExpr And(IEnumerable<BoolExpr> _elems) => AndExpr.Create(_elems);

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
        public BoolExpr Or(IEnumerable<BoolExpr> _elems) => OrExpr.Create(_elems);


        /// <summary>
        /// Returns an expression equivalent to the exclusive-or of the
        /// supplied expressions.
        /// </summary>
        /// <param name="_elems"></param>
        /// <returns></returns>
        public BoolExpr Xor(IEnumerable<BoolExpr> _elems)
        {
            var res = Model.False;
            foreach (var v in _elems)
                res ^= v;
            return res;
        }

        /// <summary>
        /// Returns an expression equivalent to a disjunction of the supplied
        /// expressions.
        /// </summary>
        /// <param name="_elems"></param>
        /// <returns></returns>
        public BoolExpr Or(params BoolExpr[] _elems) => OrExpr.Create(_elems);

        /// <summary>
        /// Returns the sum of the supplied expressions.
        /// </summary>
        /// <param name="_count"></param>
        /// <returns></returns>
        public LinExpr Sum(params BoolExpr[] _count) => Sum((IEnumerable<BoolExpr>)_count);

        /// <summary>
        /// Returns the sum of the supplied expressions as UIntVar.
        /// </summary>
        /// <param name="_count"></param>
        /// <returns></returns>
        public UIntVar SumUInt(params BoolExpr[] _count) => SumUInt((IEnumerable<BoolExpr>)_count);

        /// <summary>
        /// Returns the sum of the supplied expressions as UIntVar.
        /// </summary>
        /// <param name="_count"></param>
        /// <returns></returns>
        public UIntVar SumUInt(IEnumerable<BoolExpr> _count)
        {
            var simplified = _count.Where(b => !ReferenceEquals(b, False)).ToArray();
            var trueCount = simplified.Count(b => ReferenceEquals(b, True));
            simplified = simplified.Where(b => !ReferenceEquals(b, True)).ToArray();

            switch (simplified.Length)
            {
                case 0:
                    return UIntVar.Const(this, trueCount);
                case 1:
                    return UIntVar.ITE(simplified[0], UIntVar.Const(this, trueCount + 1), UIntVar.Const(this, trueCount));
                case 2:
                    {
                        var res = new UIntVar(this, 2, false);
                        res.bit[0] = Xor(simplified);
                        res.bit[1] = simplified[0] & simplified[1];
                        return res + trueCount;
                    }
                case 3:
                    {
                        var res = new UIntVar(this, 3, false);
                        res.bit[0] = Xor(simplified);
                        res.bit[1] = (simplified[0] & simplified[1]) | (simplified[0] & simplified[2]) | (simplified[1] & simplified[2]).Flatten();
                        return res + trueCount;
                    }
                default:
                    var groupsOfThree = new UIntVar[(simplified.Length + 2) / 3];
                    for (var i = 0; i < groupsOfThree.Length; i++)
                        groupsOfThree[i] = SumUInt(simplified.Skip(i * 3).Take(3));
                    return Sum(groupsOfThree) + trueCount;
            }
        }

        /// <summary>
        /// Returns the sum of the supplied expressions.
        /// </summary>
        /// <param name="_count"></param>
        /// <returns></returns>
        public LinExpr Sum(IEnumerable<BoolExpr> _elems)
        {
            var le = new LinExpr();
            foreach (var v in _elems)
                le.AddTerm(v);
            return le;
        }

        /// <summary>
        /// Returns the sum of the supplied UIntVars.
        /// </summary>
        /// <param name="_count"></param>
        /// <returns></returns>
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

            if (ReferenceEquals(_else, False))
                return (_if & _then).Flatten();
            if (ReferenceEquals(_else, True))
                return (!_if | _then).Flatten();

            if (ReferenceEquals(_then, False))
                return (!_if & _else).Flatten();
            if (ReferenceEquals(_then, True))
                return (_if | _else).Flatten();

            var x = AddVar();
            AddConstr(!(_if & _then) | x);
            AddConstr(!(_if & !_then) | !x);
            AddConstr(!(!_if & _else) | x);
            AddConstr(!(!_if & !_else) | !x);

            //arc-consistency
            AddConstr(!(_then & _else) | x);
            AddConstr(!(!_then & !_else) | !x);
            return x;
        }

        /// <summary>
        /// Returns the sum of the supplied LinExprs.
        /// </summary>
        /// <param name="_count"></param>
        /// <returns></returns>
        public LinExpr Sum(IEnumerable<LinExpr> _elems)
        {
            var sum = new LinExpr();
            foreach (var e in _elems)
                sum += e;
            return sum;
        }

        public enum ExactlyOneOfMethod
        {
            Commander,
            UnaryCount,
            BinaryCount,
            TwoFactor,
            Pairwise,
            PairwiseTree,
            OneHot,
            Sequential,
            Binary
        }

        public enum AtMostOneOfMethod
        {
            Pairwise,
            PairwiseTree,
            Commander,
            OneHot,
            Sequential,
            BinaryCount,
            Binary
        }

        public enum ExactlyKOfMethod
        {
            BinaryCount,
            UnaryCount,
            Pairwise,
            Sequential
        }

        /// <summary>
        /// Expression is True iff at most one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        /// <param name="_expr"></param>
        /// <returns></returns>
        public BoolExpr AtMostOneOf(params BoolExpr[] _expr) => AtMostOneOf(_expr.AsEnumerable());

        /// <summary>
        /// Expression is True iff at most one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        /// <param name="_expr"></param>
        /// <returns></returns>
        public BoolExpr AtMostOneOf(IEnumerable<BoolExpr> _expr, AtMostOneOfMethod? _method = null)
        {
            var expr = _expr.Where(e => !ReferenceEquals(e, False)).ToArray();

            var trueCount = expr.Count(e => ReferenceEquals(e, True));
            if (trueCount > 1)
                return False;

            if (trueCount == 1)
                return !Or(expr.Where(e => !ReferenceEquals(e, True))).Flatten();

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
                    if (expr.Length <= 8)
                        return AtMostOneOfPairwise(expr);
                    else
                        return AtMostOneOfCommander(expr);
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
                    return SumUInt(expr) < 2;
                case AtMostOneOfMethod.Binary:
                    return AtMostOneOfBinary(expr);
                default:
                    throw new ArgumentException();
            }
        }

        private BoolExpr AtMostOneOfBinary(IEnumerable<BoolExpr> _expr)
        {
            var expr = _expr.ToArray();
            if (expr.Length < 4)
                return AtMostOneOfPairwise(expr);

            var one = new BoolExpr[(expr.Length + 1) / 2];
            var more = new BoolExpr[(expr.Length + 1) / 2];
            for (var i = 0; i < one.Length; i++)
            {
                if (i * 2 + 1 == expr.Length)
                {
                    one[i] = expr[i * 2];
                    more[i] = false;
                }
                else
                {
                    one[i] = expr[i * 2] | expr[i * 2 + 1];
                    more[i] = expr[i * 2] & expr[i * 2 + 1];
                }
            }

            return AtMostOneOfBinary(one) & !Or(more).Flatten();
        }

        private BoolExpr AtMostOneOfSequential(IEnumerable<BoolExpr> _expr)
        {
            var v0 = False;
            var v1 = False;
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
                    commanders[i] = AddVar();

                    //1
                    for (var j = 0; j < groups[i].Length; j++)
                        for (var k = j + 1; k < groups[i].Length; k++)
                            valid.Add(Or(!groups[i][j], !groups[i][k]));

                    //AddConstr((!commanders[i]) | new OrExpr(groups[i])); //2
                    AddConstr(commanders[i] | (!Or(groups[i]))); //3
                }

            valid.Add(ExactlyOneOfCommander(commanders));
            return And(valid);
        }

        /// <summary>
        /// Expression is True iff exactly one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        /// <param name="_expr"></param>
        /// <returns></returns>
        public BoolExpr ExactlyOneOf(params BoolExpr[] _expr) => ExactlyOneOf(_expr.AsEnumerable());

        /// <summary>
        /// Expression is True iff exactly one of the supplied expressions is True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        /// <param name="_expr"></param>
        /// <returns></returns>
        public BoolExpr ExactlyOneOf(IEnumerable<BoolExpr> _expr, ExactlyOneOfMethod? _method = null)
        {
            var expr = _expr.Where(e => !ReferenceEquals(e, False)).ToArray();

            var trueCount = expr.Count(e => ReferenceEquals(e, True));
            if (trueCount > 1)
                return False;

            if (trueCount == 1)
                return !Or(expr.Where(e => !ReferenceEquals(e, True))).Flatten();

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
                    if (expr.Length <= 8)
                        return ExactlyOneOfPairwise(expr);
                    else
                        return ExactlyOneOfCommander(expr);
                case ExactlyOneOfMethod.UnaryCount:
                    return ExactlyKOf(expr, 1, ExactlyKOfMethod.UnaryCount);
                case ExactlyOneOfMethod.BinaryCount:
                    return ExactlyKOf(expr, 1, ExactlyKOfMethod.BinaryCount);
                case ExactlyOneOfMethod.Sequential:
                    return ExactlyKOf(expr, 1, ExactlyKOfMethod.Sequential);
                case ExactlyOneOfMethod.Commander:
                    return ExactlyOneOfCommander(expr);
                case ExactlyOneOfMethod.TwoFactor:
                    return ExactlyOneOfTwoFactor(expr);
                case ExactlyOneOfMethod.Pairwise:
                    return ExactlyOneOfPairwise(expr);
                case ExactlyOneOfMethod.Binary:
                    return ExactlyOneOfBinary(expr);
                case ExactlyOneOfMethod.PairwiseTree:
                    return ExactlyOneOfPairwiseTree(expr);
                case ExactlyOneOfMethod.OneHot:
                    return ExactlyOneOfOneHot(expr);
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
                ors.Add(And(ands).Flatten());
            }
            return Or(ors);
        }

        private BoolExpr AtMostOneOfOneHot(BoolExpr[] _expr)
        {
            var cse = new BoolExpr[(_expr.Length + 3) / 4];
            for (var i = 0; i < _expr.Length; i += 4)
                cse[i / 4] = And(_expr.Skip(i).Take(4).Select(v => !v)).Flatten();

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

                ors.Add(And(ands).Flatten());
            }
            return Or(ors);
        }

        private BoolExpr ExactlyOneOfPairwise(IEnumerable<BoolExpr> _expr)
        {
            return Or(_expr).Flatten() & AtMostOneOfPairwise(_expr);
        }

        private BoolExpr ExactlyOneOfBinary(IEnumerable<BoolExpr> _expr)
        {
            var expr = _expr.ToArray();
            if (expr.Length < 4)
                return ExactlyOneOfPairwise(expr);

            var one = new BoolExpr[(expr.Length + 1) / 2];
            var more = new BoolExpr[(expr.Length + 1) / 2];
            for (var i = 0; i < one.Length; i++)
            {
                if (i * 2 + 1 == expr.Length)
                {
                    one[i] = expr[i * 2];
                    more[i] = false;
                }
                else
                {
                    one[i] = expr[i * 2] | expr[i * 2 + 1];
                    more[i] = expr[i * 2] & expr[i * 2 + 1];
                }
            }

            return ExactlyOneOfBinary(one) & !Or(more).Flatten();
        }

        private BoolExpr AtMostOneOfPairwiseTree(IEnumerable<BoolExpr> _expr)
        {
            const int Fanout = 4;

            var expr = _expr.ToArray();
            if (expr.Length <= Fanout)
                return AtMostOneOfPairwise(expr);

            var ok = new BoolExpr[1 + (expr.Length + Fanout - 1) / Fanout];
            var any = new BoolExpr[(expr.Length + Fanout - 1) / Fanout];
            for (var i = 0; i < any.Length; i++)
            {
                ok[1 + i] = AtMostOneOfPairwise(expr.Skip(i * Fanout).Take(Fanout));
                any[i] = Or(expr.Skip(i * Fanout).Take(Fanout)).Flatten();
            }

            ok[0] = AtMostOneOfPairwiseTree(any);
            return And(ok).Flatten();
        }

        private BoolExpr ExactlyOneOfPairwiseTree(IEnumerable<BoolExpr> _expr)
        {
            const int Fanout = 4;

            var expr = _expr.ToArray();
            if (expr.Length <= Fanout)
                return ExactlyOneOfPairwise(expr);

            var ok = new BoolExpr[1 + (expr.Length + Fanout - 1) / Fanout];
            var any = new BoolExpr[(expr.Length + Fanout - 1) / Fanout];
            for (var i = 0; i < any.Length; i++)
            {
                ok[1 + i] = AtMostOneOfPairwise(expr.Skip(i * Fanout).Take(Fanout));
                any[i] = Or(expr.Skip(i * Fanout).Take(Fanout)).Flatten();
            }

            ok[0] = ExactlyOneOfPairwiseTree(any);
            return And(ok);
        }

        private BoolExpr AtMostOneOfPairwise(IEnumerable<BoolExpr> _expr)
        {
            var expr = _expr.ToArray();
            var pairs = new List<BoolExpr>(expr.Length * (expr.Length - 1) / 2);
            for (var i = 0; i < expr.Length; i++)
                for (var j = i + 1; j < expr.Length; j++)
                    pairs.Add(Or(!expr[i], !expr[j]));

            return And(pairs);
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

            var cols = new List<BoolExpr>(H);
            for (var y = 0; y < H; y++)
            {
                var c = new List<BoolExpr>(W);
                for (var x = 0; x < W; x++)
                {
                    var i = W * y + x;
                    if (i < expr.Length)
                        c.Add(expr[i]);
                }
                cols.Add(Or(c).Flatten());
            }

            var rows = new List<BoolExpr>(W);
            for (var x = 0; x < W; x++)
            {
                var c = new List<BoolExpr>(H);
                for (var y = 0; y < H; y++)
                {
                    var i = W * y + x;
                    if (i < expr.Length)
                        c.Add(expr[i]);
                }
                rows.Add(Or(c).Flatten());
            }

            return ExactlyOneOfTwoFactor(rows) &
                ExactlyOneOfTwoFactor(cols);
        }

        /// <summary>
        /// Expression is True iff exactly K one of the supplied expressions are True.
        /// 
        /// Consider using LinExpr-based constraints instead.
        /// </summary>
        /// <param name="_expr"></param>
        /// <param name="_k"></param>
        /// <returns></returns>
        public BoolExpr ExactlyKOf(IEnumerable<BoolExpr> _expr, int _k, ExactlyKOfMethod _method = ExactlyKOfMethod.Sequential)
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
                return !Or(expr).Flatten();
            else if (_k == expr.Length)
                return And(expr).Flatten();

            Debug.Assert(_k >= 1 && _k < expr.Length);

            switch (_method)
            {
                case ExactlyKOfMethod.BinaryCount:
                    return SumUInt(expr) == _k;

                case ExactlyKOfMethod.UnaryCount:
                    var uc = Sort(expr);
                    //return And(Enumerable.Range(0, uc.Length).Select(i => (i < _k) ? uc[i] : !uc[i]));
                    return uc[_k - 1] & !uc[_k];

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

                        return And(and) & Or(or);
                    }
                    else
                    {
                        var or = new List<BoolExpr>();
                        for (var i = 0; i < expr.Length; i++)
                            or.Add((expr[i]
                                & And(Enumerable.Range(0, i - 1).Select(j => !expr[j]))
                                & ExactlyKOf(Enumerable.Range(i + 1, expr.Length - i - 1).Select(j => expr[j]), _k - 1, _method)
                                ).Flatten());
                        return Or(or).Flatten();
                    }

                default:
                    throw new ArgumentException("Invalid method", nameof(_method));
            }
        }


        //Formulation by Klieber & Kwon: Efficient CNF Encoding for Selecting 1 from N Objects  
        //- https://www.cs.cmu.edu/~wklieber/papers/2007_efficient-cnf-encoding-for-selecting-1.pdf
        private BoolExpr ExactlyOneOfCommander(IEnumerable<BoolExpr> _expr)
        {
            if (_expr.Count() <= 5)
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
                    commanders[i] = AddVar();

                    //1
                    for (var j = 0; j < groups[i].Length; j++)
                        for (var k = j + 1; k < groups[i].Length; k++)
                            valid.Add(Or(!groups[i][j], !groups[i][k]).Flatten());

                    AddConstr((!commanders[i]) | Or(groups[i])); //2
                    AddConstr(commanders[i] | (!Or(groups[i]))); //3
                }

            valid.Add(ExactlyOneOfCommander(commanders));

            return And(valid);
        }

        /// <summary>
        /// Sorts the given expressions. True will be returned first, False last.
        /// </summary>
        /// <param name="_e"></param>
        /// <returns></returns>

        //Formulation by Bailleux & Boufkhad
        //- https://pdfs.semanticscholar.org/a948/1bf4ce2b5c20d2e282dd69dcb92bddcc36c9.pdf
        public BoolExpr[] Sort(IEnumerable<BoolExpr> _e)
        {
            var len = _e.Count();
            switch (len)
            {
                case 0:
                    return new BoolExpr[0];
                case 1:
                    return new BoolExpr[] { _e.Single() };
                case 2:
                    return new BoolExpr[] { Or(_e).Flatten(), And(_e).Flatten() };
                default:
                    var R = new BoolExpr[len + 2];
                    R[0] = True;
                    for (var i = 1; i < R.Length - 1; i++)
                        R[i] = AddVar();
                    R[R.Length - 1] = False;

                    var A = new BoolExpr[] { True }.Concat(Sort(_e.Take(len / 2))).Concat(new BoolExpr[] { False }).ToArray();
                    var B = new BoolExpr[] { True }.Concat(Sort(_e.Skip(len / 2))).Concat(new BoolExpr[] { False }).ToArray();
                    for (var a = 0; a < A.Length - 1; a++)
                        for (var b = 0; b < B.Length - 1; b++)
                        {
                            var r = a + b;
                            if (r >= 0 && r < R.Length)
                            {
                                var C1 = Or(!A[a], !B[b], R[r]).Flatten();
                                var C2 = Or(A[a + 1], B[b + 1], !R[r + 1]).Flatten();
                                AddConstr(C1 & C2);
                            }
                        }

                    return R.Skip(1).Take(R.Length - 2).ToArray();
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
