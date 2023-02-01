using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace SATInterface.Solver
{
    /// <summary>
    /// Managed-code facade of the native CryptoMiniSat solver
    /// </summary>
    public class CryptoMiniSat : Solver
    {
        private IntPtr Handle;

        public CryptoMiniSat()
        {
            if (!Environment.Is64BitProcess)
                throw new Exception("This library only supports x64 when using the bundled CryptoMiniSat solver.");

            Handle = CryptoMiniSatNative.cmsat_new();
        }

        public override (State State, bool[]? Vars) Solve(int _variableCount, long _timeout = long.MaxValue, int[]? _assumptions = null)
        {
            CryptoMiniSatNative.cmsat_set_verbosity(Handle, (uint)Math.Max(0, Model.Configuration.Verbosity - 1));

            if (_timeout != long.MaxValue)
                //CryptoMiniSatNative.cmsat_set_max_time(Handle, (_timeout - Environment.TickCount64) / 1000d);
                throw new Exception("CryptoMiniSat does not support wall-clock time limits");


            CryptoMiniSatNative.c_lbool result;
            if (_assumptions == null || _assumptions.Length == 0)
                result = CryptoMiniSatNative.cmsat_solve(Handle);
            else
                result = CryptoMiniSatNative.cmsat_solve_with_assumptions(
                        Handle,
                        _assumptions.Select(v => v < 0 ? (-v - v - 2 + 1) : (v + v - 2)).ToArray(),
                        (IntPtr)_assumptions.Length
                    );

            if (result == CryptoMiniSatNative.c_lbool.L_UNDEF)
                return (State.Undecided, null);

            if (Model.Configuration.Verbosity >= 2)
                CryptoMiniSatNative.cmsat_print_stats(Handle);

            if (result == CryptoMiniSatNative.c_lbool.L_TRUE)
            {
                var model = CryptoMiniSatNative.cmsat_get_model(Handle);

                Debug.Assert((int)model.num_vals <= _variableCount);
                var bytes = new byte[_variableCount];
                if ((int)model.num_vals != 0)
                    Marshal.Copy(model.vals, bytes, 0, (int)model.num_vals);
                return (State.Satisfiable, bytes.Select(v => v == (byte)CryptoMiniSatNative.c_lbool.L_TRUE).ToArray());
            }
            else
                return (State.Unsatisfiable, null);
        }

        public override void AddClause(ReadOnlySpan<int> _clause)
        {
            var maxVar = 0;
            foreach (var v in _clause)
                if (v > maxVar)
                    maxVar = v;
                else if (-v > maxVar)
                    maxVar = -v;

            var curVars = CryptoMiniSatNative.cmsat_nvars(Handle);
            if (curVars < maxVar)
                CryptoMiniSatNative.cmsat_new_vars(Handle, (IntPtr)(maxVar - curVars));

            CryptoMiniSatNative.cmsat_add_clause(Handle,
                _clause.ToArray().Select(v => v < 0 ? (-v - v - 2 + 1) : (v + v - 2)).ToArray(),
                (IntPtr)_clause.Length);
        }

        protected override void DisposeUnmanaged()
        {
            CryptoMiniSatNative.cmsat_free(Handle);
            Handle = IntPtr.Zero;
        }

        internal override void ApplyConfiguration()
        {
            if (Model.Configuration.Threads.HasValue)
                CryptoMiniSatNative.cmsat_set_num_threads(Handle, (uint)Model.Configuration.Threads.Value);

            if (Model.Configuration.RandomSeed.HasValue)
                throw new NotImplementedException("CryptoMiniSat does not allow the configuration of RandomSeed.");

            if (Model.Configuration.InitialPhase.HasValue)
                throw new NotImplementedException("CryptoMiniSat does not allow the configuration of InitialPhase.");
        }
    }

    public static class CryptoMiniSatNative
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    {
        //https://github.com/msoos/cryptominisat/blob/master/src/cryptominisat_c.h.in

        //typedef struct slice_Lit { const c_Lit* vals; size_t num_vals; }
        //typedef struct slice_lbool { const c_lbool* vals; size_t num_vals; }

        public enum c_lbool : byte
        {
            L_TRUE = 0,
            L_FALSE = 1,
            L_UNDEF = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct slice_lbool
        {
            public IntPtr vals;
            public IntPtr num_vals;
        }

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern bool cmsat_add_clause(IntPtr self, [In, MarshalAs(UnmanagedType.LPArray)] Int32[] lits, IntPtr num_lits);

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern bool cmsat_add_xor_clause(IntPtr self, [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] vars, IntPtr num_vars, bool rhs);

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cmsat_free(IntPtr s);

        //[DllImport("cryptominisat5.exe")]
        //public static extern slice_Lit cmsat_get_conflict(SATSolver self);

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern slice_lbool cmsat_get_model(IntPtr self);

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cmsat_new();

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cmsat_new_vars(IntPtr self, IntPtr n);

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern UInt32 cmsat_nvars(IntPtr self);

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cmsat_set_num_threads(IntPtr self, UInt32 n);

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cmsat_print_stats(IntPtr self);

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cmsat_set_verbosity(IntPtr self, UInt32 n);

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern c_lbool cmsat_solve(IntPtr self);

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern c_lbool cmsat_solve_with_assumptions(IntPtr self, [In, MarshalAs(UnmanagedType.LPArray)] Int32[] assumptions, IntPtr num_assumptions);

        [DllImport("cryptominisat5win.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void cmsat_set_max_time(IntPtr self, double max_time);
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
