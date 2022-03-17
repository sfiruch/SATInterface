using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace SATInterface.Solver
{
    /// <summary>
    /// Managed-code facade of the native YalSAT solver
    /// </summary>
    public class YalSAT : Solver
    {
        List<int> clauses = new List<int>();

        public YalSAT()
        {
            if (!Environment.Is64BitProcess)
                throw new Exception("This library only supports x64 when using the bundled YalSAT solver.");
        }

        public override (State State, bool[]? Vars) Solve(int _variableCount, long _timeout=long.MaxValue, int[]? _assumptions = null)
        {
            var callback = (YalSATNative.TerminateCallback)(s => Environment.TickCount64 > _timeout ? 1 : 0);
            var Handle = YalSATNative.yals_new();
            try
            {
                YalSATNative.yals_seterm(Handle, callback, IntPtr.Zero);

                YalSATNative.yals_setopt(Handle, "verbose", Math.Max(0, Model.Configuration.Verbosity-1));

                if ((Model.Configuration.Threads ?? 1) != 1)
                    //TODO: YalSATNative.yals_setopt(Handle, "threads", Model.Configuration.Threads.Value);
                    throw new NotImplementedException("YalSAT only supports single-threaded operation.");

                if (Model.Configuration.RandomSeed.HasValue)
                    YalSATNative.yals_srand(Handle, (ulong)Model.Configuration.RandomSeed.Value);

                if (Model.Configuration.InitialPhase.HasValue)
                    throw new NotImplementedException("YalSAT does not yet support initial phase.");

                switch (Model.Configuration.ExpectedOutcome)
                {
                    case ExpectedOutcome.Sat:
                        break;
                    case ExpectedOutcome.Unsat:
                        break;
                    //case ExpectedOutcome.RandomSampledAssignment:
                    //    YalSATNative.yals_setopt(Handle, "pick", 0);
                    //    YalSATNative.yals_setopt(Handle, "pol", 0);
                    //    YalSATNative.yals_setopt(Handle, "cacheduni", 1);
                    //    YalSATNative.yals_setopt(Handle, "toggleuniform", 1);
                    //    break;
                }

                foreach (var v in clauses)
                    YalSATNative.yals_add(Handle, v);

                if (_assumptions != null)
                {
                    foreach (var a in _assumptions)
                    {
                        YalSATNative.yals_add(Handle, a);
                        YalSATNative.yals_add(Handle, 0);
                    }
                }

                //if (Verbosity >= 1)
                //    YalSATNative.kissat_banner("c ", Marshal.PtrToStringAnsi(YalSATNative.kissat_signature()));

                var satisfiable = YalSATNative.yals_sat(Handle);

                if (Model.Configuration.Verbosity >= 1)
                    YalSATNative.yals_stats(Handle);

                switch (satisfiable)
                {
                    case 10:
                        //satisfiable
                        var res = new bool[_variableCount];
                        for (var i = 0; i < _variableCount; i++)
                            res[i] = YalSATNative.yals_deref(Handle, i + 1) > 0;
                        return (State.Satisfiable, res);

                    case 20:
                        //unsat
                        return (State.Unsatisfiable, null);

                    case 0:
                        //interrupted
                        return (State.Undecided, null);

                    default:
                        throw new Exception();
                }
            }
            finally
            {
                YalSATNative.yals_del(Handle);
                GC.KeepAlive(callback);
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

    public static class YalSATNative
    {
        [DllImport("YalSAT.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr yals_new();

        [DllImport("YalSAT.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void yals_del(IntPtr wrapper);

        [DllImport("YalSAT.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void yals_srand(IntPtr wrapper, ulong seed);

        [DllImport("YalSAT.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int yals_setopt(IntPtr wrapper, [In, MarshalAs(UnmanagedType.LPStr)] string name, int val);

        [DllImport("YalSAT.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void yals_setphase(IntPtr wrapper, int lit);

        [DllImport("YalSAT.dll", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void yals_add(IntPtr wrapper, int lit);

        [DllImport("YalSAT.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int yals_sat(IntPtr wrapper);

        [DllImport("YalSAT.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void yals_stats(IntPtr wrapper);

        [DllImport("YalSAT.dll", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int yals_deref(IntPtr wrapper, int lit);

        [DllImport("YalSAT.dll", CallingConvention = CallingConvention.Cdecl)] 
        public static extern void yals_seterm(IntPtr wrapper, [MarshalAs(UnmanagedType.FunctionPtr)] TerminateCallback? terminate, IntPtr state);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int TerminateCallback(IntPtr State);
    }
}
