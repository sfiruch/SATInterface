using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace SATInterface
{
    /// <summary>
    /// Managed-code facade of the native CryptoMiniSat solver
    /// </summary>
    public class CryptoMiniSat : ISolver
    {
        private IntPtr Handle;
        private int Verbosity;

        public CryptoMiniSat()
        {
            if (!Environment.Is64BitProcess)
                throw new Exception("This library only supports x64 when using the bundled CryptoMiniSat solver.");

            Handle = CryptoMiniSatNative.cmsat_new();
        }

        public bool[]? Solve(int[]? _assumptions = null)
        {
            bool satisfiable;
            if (_assumptions == null || _assumptions.Length == 0)
                satisfiable = CryptoMiniSatNative.cmsat_solve(Handle) == CryptoMiniSatNative.c_lbool.L_TRUE;
            else
                satisfiable = CryptoMiniSatNative.cmsat_solve_with_assumptions(Handle,
                    _assumptions.Select(v => v < 0 ? (-v - v - 2 + 1) : (v + v - 2)).ToArray(),
                    (IntPtr)_assumptions.Length) == CryptoMiniSatNative.c_lbool.L_TRUE;

            if (Verbosity >= 1)
                CryptoMiniSatNative.cmsat_print_stats(Handle);

            if (satisfiable)
            {
                var model = CryptoMiniSatNative.cmsat_get_model(Handle);
                var bytes = new byte[(int)model.num_vals];
                if ((int)model.num_vals != 0)
                    Marshal.Copy(model.vals, bytes, 0, (int)model.num_vals);
                return bytes.Select(v => v == (byte)CryptoMiniSatNative.c_lbool.L_TRUE).ToArray();
            }
            else
                return null;
        }

        public void AddVars(int _number) => CryptoMiniSatNative.cmsat_new_vars(Handle, (IntPtr)_number);

        public bool AddClause(int[] _clause) => CryptoMiniSatNative.cmsat_add_clause(Handle,
                _clause.Select(v => v < 0 ? (-v - v - 2 + 1) : (v + v - 2)).ToArray(),
                (IntPtr)_clause.Length);

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //dispose managed state
                }

                CryptoMiniSatNative.cmsat_free(Handle);
                Handle = IntPtr.Zero;

                disposedValue = true;
            }
        }

        ~CryptoMiniSat()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        void ISolver.ApplyConfiguration(Configuration _config)
        {
            Verbosity = Math.Max(0, _config.Verbosity - 1);
            CryptoMiniSatNative.cmsat_set_verbosity(Handle, (uint)Verbosity);

            if (_config.Threads.HasValue)
                CryptoMiniSatNative.cmsat_set_num_threads(Handle, (uint)_config.Threads.Value);

            if (_config.RandomSeed.HasValue)
                throw new NotImplementedException("CryptoMiniSat does not allow the configuration of RandomSeed.");

            if (_config.InitialPhase.HasValue)
                throw new NotImplementedException("CryptoMiniSat does not allow the configuration of InitialPhase.");
        }
    }

    public static class CryptoMiniSatNative
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

        [DllImport("cryptominisat5win.dll")]
        public static extern bool cmsat_add_clause(IntPtr self, [In, MarshalAs(UnmanagedType.LPArray)] Int32[] lits, IntPtr num_lits);

        [DllImport("cryptominisat5win.dll")]
        public static extern bool cmsat_add_xor_clause(IntPtr self, [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] vars, IntPtr num_vars, bool rhs);

        [DllImport("cryptominisat5win.dll")]
        public static extern void cmsat_free(IntPtr s);

        //[DllImport("cryptominisat5.exe")]
        //public static extern slice_Lit cmsat_get_conflict(SATSolver self);

        [DllImport("cryptominisat5win.dll")]
        public static extern slice_lbool cmsat_get_model(IntPtr self);

        [DllImport("cryptominisat5win.dll")]
        public static extern IntPtr cmsat_new();

        [DllImport("cryptominisat5win.dll")]
        public static extern void cmsat_new_vars(IntPtr self, IntPtr n);

        [DllImport("cryptominisat5win.dll")]
        public static extern UInt32 cmsat_nvars(IntPtr self);

        [DllImport("cryptominisat5win.dll")]
        public static extern void cmsat_set_num_threads(IntPtr self, UInt32 n);

        [DllImport("cryptominisat5win.dll")]
        public static extern void cmsat_print_stats(IntPtr self);

        [DllImport("cryptominisat5win.dll")]
        public static extern void cmsat_set_verbosity(IntPtr self, UInt32 n);

        [DllImport("cryptominisat5win.dll")]
        public static extern c_lbool cmsat_solve(IntPtr self);

        [DllImport("cryptominisat5win.dll")]
        public static extern c_lbool cmsat_solve_with_assumptions(IntPtr self, [In, MarshalAs(UnmanagedType.LPArray)] Int32[] assumptions, IntPtr num_assumptions);

        [DllImport("cryptominisat5win.dll")]
        public static extern void cmsat_set_max_time(IntPtr self, double max_time);

    }
}
