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

        public CaDiCaL()
        {
        }

        public override (State State, bool[]? Vars) Solve(int _variableCount, long _timeout = long.MaxValue, int[]? _assumptions = null)
        {
            var verbosity = Math.Max(0, Model.Configuration.Verbosity - 1);
            CaDiCaLNative.ccadical_set_option(Handle, "quiet", verbosity == 0 ? 1 : 0);
            CaDiCaLNative.ccadical_set_option(Handle, "report", verbosity > 0 ? 1 : 0);
            CaDiCaLNative.ccadical_set_option(Handle, "verbose", Math.Max(0, verbosity - 1));

            var callback = (CaDiCaLNative.TerminateCallback)(s =>
            {
                return Environment.TickCount64 > _timeout ? 1 : 0;
            });
            try
            {
                CaDiCaLNative.ccadical_set_terminate(Handle, IntPtr.Zero, callback);

                if (_assumptions != null)
                    foreach (var a in _assumptions)
                        CaDiCaLNative.ccadical_assume(Handle, a);

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
                            res[i] = CaDiCaLNative.ccadical_val(Handle, i + 1) > 0;
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
            finally
            {
                CaDiCaLNative.ccadical_set_terminate(Handle, IntPtr.Zero, null);
                GC.KeepAlive(callback);
            }
        }

        public override void AddClause(ReadOnlySpan<int> _clause)
        {
            foreach (var i in _clause)
                CaDiCaLNative.ccadical_add(Handle, i);
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
            if (_phase == true)
                CaDiCaLNative.ccadical_phase(Handle, _variable);
            else if(_phase == false)
				CaDiCaLNative.ccadical_phase(Handle, -_variable);
            else
                CaDiCaLNative.ccadical_unphase(Handle, _variable);
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
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial IntPtr ccadical_init();

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void ccadical_release(IntPtr wrapper);

        [LibraryImport("CaDiCaL.dll")]
        [SuppressGCTransition]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void ccadical_add(IntPtr wrapper, int lit);

        [LibraryImport("CaDiCaL.dll")]
        [SuppressGCTransition]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void ccadical_assume(IntPtr wrapper, int lit);

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial int ccadical_solve(IntPtr wrapper);

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial int ccadical_simplify(IntPtr wrapper);

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial int ccadical_lookahead(IntPtr wrapper);

        [LibraryImport("CaDiCaL.dll")]
        [SuppressGCTransition]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial int ccadical_val(IntPtr wrapper, int lit);

		[LibraryImport("CaDiCaL.dll")]
		[SuppressGCTransition]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void ccadical_phase(IntPtr wrapper, int lit);

		[LibraryImport("CaDiCaL.dll")]
		[SuppressGCTransition]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void ccadical_unphase(IntPtr wrapper, int lit);

		[LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial int ccadical_print_statistics(IntPtr wrapper);

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void ccadical_set_option(IntPtr wrapper, [MarshalAs(UnmanagedType.LPStr)] string name, int val);

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void ccadical_limit(IntPtr wrapper, [MarshalAs(UnmanagedType.LPStr)] string name, int val);

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial IntPtr ccadical_signature();

        [LibraryImport("CaDiCaL.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void ccadical_set_terminate(IntPtr wrapper, IntPtr state, [MarshalAs(UnmanagedType.FunctionPtr)] TerminateCallback? terminate);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int TerminateCallback(IntPtr State);
    }
}
