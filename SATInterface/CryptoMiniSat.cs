using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using c_Lit = System.Int32;
using SATSolver = System.IntPtr;
using size_t = System.IntPtr;
using System.Runtime.InteropServices;

namespace SATInterface
{
    static class CryptoMiniSat
    {
        //https://github.com/msoos/cryptominisat/blob/master/src/cryptominisat_c.h.in

        //typedef struct slice_Lit { const c_Lit* vals; size_t num_vals; }
        //typedef struct slice_lbool { const c_lbool* vals; size_t num_vals; }

        private enum c_lbool : byte
        {
            L_TRUE = 0,
            L_FALSE = 1,
            L_UNDEF = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct slice_lbool
        {
            public IntPtr vals;
            public IntPtr num_vals;
        }


        [DllImport("cryptominisat5win.dll")]
        public static extern SATSolver cmsat_new();

        [DllImport("cryptominisat5win.dll")]
        public static extern void cmsat_free(SATSolver s);

        [DllImport("cryptominisat5win.dll")]
        public static extern UInt32 cmsat_nvars(SATSolver self);

        [DllImport("cryptominisat5win.dll")]
        public static extern bool cmsat_add_clause(SATSolver self, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] c_Lit[] lits, IntPtr num_lits);

        [DllImport("cryptominisat5win.dll")]
        public static extern bool cmsat_add_xor_clause(SATSolver self, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] UInt32[] vars, IntPtr num_vars, bool rhs);

        [DllImport("cryptominisat5win.dll")]
        public static extern void cmsat_new_vars(SATSolver self, IntPtr n);

        [DllImport("cryptominisat5win.dll")]
        static extern c_lbool cmsat_solve(SATSolver self);

        [DllImport("cryptominisat5win.dll")]
        static extern c_lbool cmsat_solve_with_assumptions(SATSolver self, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] c_Lit[] assumptions, IntPtr num_assumptions);

        [DllImport("cryptominisat5win.dll")]
        static extern slice_lbool cmsat_get_model(SATSolver self);

        //[DllImport("cryptominisat5.exe")]
        //public static extern slice_Lit cmsat_get_conflict(SATSolver self);

        [DllImport("cryptominisat5win.dll")]
        public static extern void cmsat_set_num_threads(SATSolver self, UInt32 n);
    }
}
