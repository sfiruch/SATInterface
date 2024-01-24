using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Numerics;
using System.Diagnostics.CodeAnalysis;

namespace SATInterface.Solver
{
    /// <summary>
    /// Managed-code facade of the native Kissat solver
    /// </summary>
    public class Kissat:Solver //<T> : Solver where T : struct, IBinaryInteger<T>
	{
        private readonly List<int> clauses = new();

        public Kissat()
        {
            if (!Environment.Is64BitProcess)
                throw new Exception("This library only supports x64 when using the bundled Kissat solver.");
        }

        public override (State State, bool[]? Vars) Solve(int _variableCount, long _timeout=long.MaxValue, int[]? _assumptions = null)
        {
            var Handle = KissatNative.kissat_init();
            CancellationTokenRegistration? ctr=null;
            try
            {
                if (_timeout != long.MaxValue)
                {
                    var timeout = (int)Math.Min(int.MaxValue, _timeout - Environment.TickCount64);
                    if (timeout <= 0)
                        return (State.Undecided, null);

                    ctr = new CancellationTokenSource(timeout).Token.Register(() => KissatNative.kissat_terminate(Handle));
                }

                KissatNative.kissat_set_option(Handle, "quiet", Model.Configuration.Verbosity == 0 ? 1 : 0);
                //KissatNative.kissat_set_option(Handle, "report", Verbosity > 0 ? 1 : 0);
                KissatNative.kissat_set_option(Handle, "verbose", Math.Max(0, Model.Configuration.Verbosity - 2));

                if ((Model.Configuration.Threads ?? 1) != 1)
                    throw new NotImplementedException("Kissat only supports single-threaded operation.");

                if (Model.Configuration.RandomSeed.HasValue)
                    KissatNative.kissat_set_option(Handle, "seed", Model.Configuration.RandomSeed.Value);

                if (Model.Configuration.InitialPhase.HasValue)
                    KissatNative.kissat_set_option(Handle, "phase", Model.Configuration.InitialPhase.Value ? 1 : 0);

                switch (Model.Configuration.ExpectedOutcome)
                {
                    case ExpectedOutcome.Sat:
                        KissatNative.kissat_set_option(Handle, "target", 2);
                        KissatNative.kissat_set_option(Handle, "restartint", 50);
                        break;
                    case ExpectedOutcome.Unsat:
                        KissatNative.kissat_set_option(Handle, "stable", 0);
                        break;
                    //case ExpectedOutcome.RandomSampledAssignment:
                    //    KissatNative.kissat_set_option(Handle, "target", 2);
                    //    KissatNative.kissat_set_option(Handle, "restartint", 50);
                    //    KissatNative.kissat_set_option(Handle, "restartreusetrail", 0);
                    //    KissatNative.kissat_set_option(Handle, "walkeffort", 500000);
                    //    KissatNative.kissat_set_option(Handle, "walkinitially", 1);
                    //    KissatNative.kissat_set_option(Handle, "reluctant", 0);
                    //    break;
                }

                foreach (var v in clauses)
                    KissatNative.kissat_add(Handle, v);

                if (_assumptions != null)
                {
                    foreach (var a in _assumptions)
                    {
                        KissatNative.kissat_add(Handle, a);
                        KissatNative.kissat_add(Handle, 0);
                    }
                }

                if (Model.Configuration.Verbosity >= 1)
                    KissatNative.kissat_banner("c ", Marshal.PtrToStringAnsi(KissatNative.kissat_signature())!);

                var satisfiable = KissatNative.kissat_solve(Handle);

                if (Model.Configuration.Verbosity >= 1)
                    KissatNative.kissat_print_statistics(Handle);

                switch (satisfiable)
                {
                    case 10:
                        //satisfiable
                        var res = new bool[_variableCount];
                        for (var i = 0; i < _variableCount; i++)
                            res[i] = KissatNative.kissat_value(Handle, i + 1) > 0;
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
                ctr?.Unregister();
                ctr?.Dispose();
                KissatNative.kissat_release(Handle);
            }
        }

        public override void AddClause(ReadOnlySpan<int> _clause)
        {
            foreach(var v in _clause)
                clauses.Add(v);
            clauses.Add(0);
        }

        internal override void ApplyConfiguration()
        {
        }
    }

	[SuppressMessage("Style", "IDE1006:Naming Styles")]
	[SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible")]
	public static partial class KissatNative
    {
        [LibraryImport("kissat.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial IntPtr kissat_init();

        [LibraryImport("kissat.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void kissat_release(IntPtr wrapper);

        [LibraryImport("kissat.dll")]
        [SuppressGCTransition]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void kissat_add(IntPtr wrapper, int lit);

        //[DllImport("kissat.dll", CallingConvention = CallingConvention.Cdecl)]
        //[SuppressGCTransition]
        //public static extern void kissat_assume(IntPtr wrapper, int lit);

        [LibraryImport("kissat.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial int kissat_solve(IntPtr wrapper);

        [LibraryImport("kissat.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void kissat_terminate(IntPtr wrapper);

        [LibraryImport("kissat.dll")]
        [SuppressGCTransition]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial int kissat_value(IntPtr wrapper, int lit);

        [LibraryImport("kissat.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void kissat_print_statistics(IntPtr wrapper);

        [LibraryImport("kissat.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial int kissat_set_option(IntPtr wrapper, [MarshalAs(UnmanagedType.LPStr)] string name, int val);

        [LibraryImport("kissat.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial int kissat_banner([MarshalAs(UnmanagedType.LPStr)] string line_prefix, [MarshalAs(UnmanagedType.LPStr)] string name_of_app);

        [LibraryImport("kissat.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial IntPtr kissat_signature();

        [LibraryImport("kissat.dll")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		public static partial void kissat_set_terminate(IntPtr wrapper, IntPtr state, [MarshalAs(UnmanagedType.FunctionPtr)] TerminateCallback? terminate);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int TerminateCallback(IntPtr State);
    }
}
