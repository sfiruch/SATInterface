using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Numerics;

namespace SATInterface.Solver
{
    /// <summary>
    /// Managed-code facade of the native CaDiCaL solver
    /// </summary>
    public class CaDiCaL:Solver //<T> : Solver where T : struct, IBinaryInteger<T>
	{
        public IntPtr Handle;
        private int _declaredModelVarCount;
        private int _solverMaxDeclaredVar;
        // Each entry covers model vars [ModelStart .. next entry's ModelStart) mapped
        // to solver vars starting at SolverStart. Only non-identity batches are stored
        // (i.e. where factor created extension variables that shifted the numbering).
        private readonly List<(int ModelStart, int SolverStart)> _varBatches = [];

        // Held as a field so the delegate is never GC'd while CaDiCaL holds a pointer to it.
        // See https://github.com/arminbiere/cadical/issues/90
        private long _terminateTimeoutTicks = long.MaxValue;
        private readonly CaDiCaLNative.TerminateCallback _terminateCallback;

        public CaDiCaL()
        {
            _terminateCallback = _ => Environment.TickCount64 > _terminateTimeoutTicks ? 1 : 0;
        }

        private int SolverLit(int modelLit)
        {
            if (_varBatches.Count == 0) return modelLit;
            var absVar = Math.Abs(modelLit);
            for (var i = _varBatches.Count - 1; i >= 0; i--)
                if (absVar >= _varBatches[i].ModelStart)
                    return Math.Sign(modelLit) * (_varBatches[i].SolverStart + absVar - _varBatches[i].ModelStart);
            return modelLit;
        }

        private void EnsureDeclared(int modelVarCount)
        {
            if (modelVarCount <= _declaredModelVarCount) return;

            int newVars = modelVarCount - _declaredModelVarCount;
            // declare_more_variables returns the new maximum solver variable index
            // (last of the consecutively declared range). If factor has created
            // extension variables since the last declaration, the solver skips over
            // them, so newSolverMax > _solverMaxDeclaredVar + newVars.
            int newSolverMax = CaDiCaLNative.ccadical_declare_more_variables(Handle, newVars);
            int firstNewSolverVar = newSolverMax - newVars + 1;

            if (firstNewSolverVar != _solverMaxDeclaredVar + 1)
                _varBatches.Add((_declaredModelVarCount + 1, firstNewSolverVar));

            _solverMaxDeclaredVar = newSolverMax;
            _declaredModelVarCount = modelVarCount;
        }

        public override (State State, bool[]? Vars) Solve(int _variableCount, long _timeout = long.MaxValue, int[]? _assumptions = null)
        {
            var verbosity = Math.Max(0, Model.Configuration.Verbosity - 1);
            CaDiCaLNative.ccadical_set_option(Handle, "quiet", verbosity == 0 ? 1 : 0);
            CaDiCaLNative.ccadical_set_option(Handle, "report", verbosity > 0 ? 1 : 0);
            CaDiCaLNative.ccadical_set_option(Handle, "verbose", Math.Max(0, verbosity - 1));

            _terminateTimeoutTicks = _timeout;
            CaDiCaLNative.ccadical_set_terminate(Handle, IntPtr.Zero, _terminateCallback);

            EnsureDeclared(_variableCount);

            if (_assumptions != null)
                foreach (var a in _assumptions)
                    CaDiCaLNative.ccadical_assume(Handle, SolverLit(a));

            if (Model.Configuration.Verbosity >= 2)
            {
                //TODO: add banner to C API
                Console.WriteLine("c " + Marshal.PtrToStringAnsi(CaDiCaLNative.ccadical_signature()));
            }

            var satisfiable = CaDiCaLNative.ccadical_solve(Handle);

            if (Model.Configuration.Verbosity >= 3)
                CaDiCaLNative.ccadical_print_statistics(Handle);

            switch (satisfiable)
            {
                case 10:
                    //satisfiable
                    var res = new bool[_variableCount];
                    for (var i = 0; i < res.Length; i++)
                        res[i] = CaDiCaLNative.ccadical_val(Handle, SolverLit(i + 1)) > 0;
                    return (State.Satisfiable, res);

                case 20:
                    //unsat
                    return (State.Unsatisfiable, null);

                case 0:
                    //interrupted
                    return (State.Undecided, null);

                default:
                    throw new NotImplementedException();
            }
        }

        public override void AddClause(ReadOnlySpan<int> _clause)
        {
            var maxVar = 0;
            foreach (var i in _clause)
                maxVar = Math.Max(maxVar, Math.Abs(i));
            EnsureDeclared(maxVar);

            foreach (var i in _clause)
                CaDiCaLNative.ccadical_add(Handle, SolverLit(i));
            CaDiCaLNative.ccadical_add(Handle, 0);
        }

        protected override void DisposeUnmanaged()
        {
            if (Handle != IntPtr.Zero)
                CaDiCaLNative.ccadical_release(Handle);

            Handle = IntPtr.Zero;
        }

		internal override void SetPhase(int _variable, bool? _phase)
        {
            EnsureDeclared(_variable);
            var solverVar = SolverLit(_variable);

            if (_phase == true)
                CaDiCaLNative.ccadical_phase(Handle, solverVar);
            else if(_phase == false)
				CaDiCaLNative.ccadical_phase(Handle, -solverVar);
            else
                CaDiCaLNative.ccadical_unphase(Handle, solverVar);
		}

		internal override void ApplyConfiguration()
        {
            if (Handle == IntPtr.Zero)
            {
                if (!Environment.Is64BitProcess)
                    throw new Exception("This library only supports x64 when using the bundled CaDiCaL solver.");
                Handle = CaDiCaLNative.ccadical_init();
            }

            if ((Model.Configuration.Threads ?? 1) != 1)
                throw new NotImplementedException("CaDiCaL only supports single-threaded operation.");

            if (Model.Configuration.RandomSeed.HasValue)
                CaDiCaLNative.ccadical_set_option(Handle, "seed", Model.Configuration.RandomSeed.Value);

            if (Model.Configuration.InitialPhase.HasValue)
                CaDiCaLNative.ccadical_set_option(Handle, "phase", Model.Configuration.InitialPhase.Value ? 1 : 0);

            var verbosity = Math.Max(0, Model.Configuration.Verbosity - 1);
            CaDiCaLNative.ccadical_set_option(Handle, "quiet", verbosity == 0 ? 1 : 0);
            CaDiCaLNative.ccadical_set_option(Handle, "report", verbosity > 0 ? 1 : 0);
            CaDiCaLNative.ccadical_set_option(Handle, "verbose", Math.Max(0, verbosity - 1));

            switch (Model.Configuration.ExpectedOutcome)
            {
                case ExpectedOutcome.Sat:
                    //copied from config.cpp
                    CaDiCaLNative.ccadical_set_option(Handle, "elimreleff", 10);
                    CaDiCaLNative.ccadical_set_option(Handle, "stabilizeonly", 1);
                    CaDiCaLNative.ccadical_set_option(Handle, "subsumereleff", 60);
                    break;
                case ExpectedOutcome.Unsat:
                    //copied from config.cpp
                    CaDiCaLNative.ccadical_set_option(Handle, "stabilize", 0);
                    CaDiCaLNative.ccadical_set_option(Handle, "walk", 0);
                    break;
                    //case ExpectedOutcome.RandomSampledAssignment:
                    //    CaDiCaLNative.ccadical_set_option(Handle, "reluctant", 0);
                    //    CaDiCaLNative.ccadical_set_option(Handle, "reluctantmax", 0);
                    //    CaDiCaLNative.ccadical_set_option(Handle, "restartint", 50);
                    //    CaDiCaLNative.ccadical_set_option(Handle, "restartreusetrail", 0);
                    //    CaDiCaLNative.ccadical_set_option(Handle, "stabilizeonly", 1);

                    //    CaDiCaLNative.ccadical_set_option(Handle, "walkreleff", 100000); //TODO: ?
                    //    break;
            }
        }
    }

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible")]
    public static partial class CaDiCaLNative
    {
        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial IntPtr ccadical_init();

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial void ccadical_release(IntPtr wrapper);

        [LibraryImport("CaDiCaL.dll")]
        [SuppressGCTransition]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial void ccadical_add(IntPtr wrapper, int lit);

        [LibraryImport("CaDiCaL.dll")]
        [SuppressGCTransition]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial void ccadical_assume(IntPtr wrapper, int lit);

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial int ccadical_solve(IntPtr wrapper);

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial int ccadical_simplify(IntPtr wrapper);

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial int ccadical_declare_more_variables(IntPtr wrapper, int number_of_vars);

        [LibraryImport("CaDiCaL.dll")]
        [SuppressGCTransition]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial int ccadical_val(IntPtr wrapper, int lit);

		[LibraryImport("CaDiCaL.dll")]
		[SuppressGCTransition]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial void ccadical_phase(IntPtr wrapper, int lit);

		[LibraryImport("CaDiCaL.dll")]
		[SuppressGCTransition]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial void ccadical_unphase(IntPtr wrapper, int lit);

		[LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial void ccadical_print_statistics(IntPtr wrapper);

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial void ccadical_set_option(IntPtr wrapper, [MarshalAs(UnmanagedType.LPStr)] string name, int val);

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial void ccadical_limit(IntPtr wrapper, [MarshalAs(UnmanagedType.LPStr)] string name, int val);

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial IntPtr ccadical_signature();

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
		public static partial void ccadical_set_terminate(IntPtr wrapper, IntPtr state, [MarshalAs(UnmanagedType.FunctionPtr)] TerminateCallback? terminate);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int TerminateCallback(IntPtr State);
    }
}
