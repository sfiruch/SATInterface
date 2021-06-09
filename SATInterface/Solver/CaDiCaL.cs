using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace SATInterface.Solver
{
    /// <summary>
    /// Managed-code facade of the native CaDiCaL solver
    /// </summary>
    public class CaDiCaL : ISolver
    {
        private IntPtr Handle;
        private int Verbosity;

        public CaDiCaL()
        {
            if (!Environment.Is64BitProcess)
                throw new Exception("This library only supports x64 when using the bundled CaDiCaL solver.");

            Handle = CaDiCaLNative.ccadical_init();
        }

        public bool[]? Solve(int _variableCount, int[]? _assumptions = null)
        {
            if (_assumptions != null)
                foreach (var a in _assumptions)
                    CaDiCaLNative.ccadical_assume(Handle, a);

            if (Verbosity >= 1)
            {
                //TODO: add banner to C API
                Console.WriteLine("c " + Marshal.PtrToStringAnsi(CaDiCaLNative.ccadical_signature()));
            }

            var satisfiable = CaDiCaLNative.ccadical_solve(Handle);

            if (Verbosity >= 1)
                CaDiCaLNative.ccadical_print_statistics(Handle);

            switch (satisfiable)
            {
                case 10:
                    //satisfiable
                    var res = new bool[_variableCount];
                    for (var i = 0; i < res.Length; i++)
                        res[i] = CaDiCaLNative.ccadical_val(Handle, i + 1) > 0;
                    return res;

                case 20:
                    //unsat
                    return null;

                case 0:
                //interrupted
                default:
                    throw new Exception();
            }
        }

        public void AddClause(Span<int> _clause)
        {
            foreach (var i in _clause)
                CaDiCaLNative.ccadical_add(Handle, i);
            CaDiCaLNative.ccadical_add(Handle, 0);
        }

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

                CaDiCaLNative.ccadical_release(Handle);
                Handle = IntPtr.Zero;

                disposedValue = true;
            }
        }

        ~CaDiCaL()
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
            CaDiCaLNative.ccadical_set_option(Handle, "quiet", Verbosity == 0 ? 1 : 0);
            CaDiCaLNative.ccadical_set_option(Handle, "report", Verbosity > 0 ? 1 : 0);
            CaDiCaLNative.ccadical_set_option(Handle, "verbose", Math.Max(0, Verbosity - 1));

            if ((_config.Threads ?? 1) != 1)
                throw new NotImplementedException("CaDiCaL only supports single-threaded operation.");

            if (_config.RandomSeed.HasValue)
                CaDiCaLNative.ccadical_set_option(Handle, "seed", _config.RandomSeed.Value);

            if (_config.InitialPhase.HasValue)
                CaDiCaLNative.ccadical_set_option(Handle, "phase", _config.InitialPhase.Value ? 1 : 0);
        }
    }

    public static class CaDiCaLNative
    {
        [DllImport("CaDiCaL.dll")]
        public static extern IntPtr ccadical_init();

        [DllImport("CaDiCaL.dll")]
        public static extern void ccadical_release(IntPtr wrapper);

        [DllImport("CaDiCaL.dll")]
        //TODO: [SuppressGCTransition]
        public static extern void ccadical_add(IntPtr wrapper, int lit);

        [DllImport("CaDiCaL.dll")]
        public static extern void ccadical_assume(IntPtr wrapper, int lit);

        [DllImport("CaDiCaL.dll")]
        public static extern int ccadical_solve(IntPtr wrapper);

        [DllImport("CaDiCaL.dll")]
        public static extern int ccadical_simplify(IntPtr wrapper);

        [DllImport("CaDiCaL.dll")]
        public static extern int ccadical_lookahead(IntPtr wrapper);

        [DllImport("CaDiCaL.dll")]
        //TODO: [SuppressGCTransition]
        public static extern int ccadical_val(IntPtr wrapper, int lit);

        [DllImport("CaDiCaL.dll")]
        public static extern int ccadical_print_statistics(IntPtr wrapper);

        [DllImport("CaDiCaL.dll")]
        public static extern void ccadical_set_option(IntPtr wrapper, [In, MarshalAs(UnmanagedType.LPStr)] string name, int val);

        [DllImport("CaDiCaL.dll")]
        public static extern void ccadical_limit(IntPtr wrapper, [In, MarshalAs(UnmanagedType.LPStr)] string name, int val);

        [DllImport("CaDiCaL.dll")]
        public static extern IntPtr ccadical_signature();
    }
}
