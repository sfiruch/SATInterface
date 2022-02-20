using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace SATInterface.Solver
{
    /// <summary>
    /// Managed-code facade of the native Kissat solver
    /// </summary>
    public class Kissat : ISolver
    {
        private Configuration? config;
        List<int> clauses = new List<int>();

        public Kissat()
        {
            if (!Environment.Is64BitProcess)
                throw new Exception("This library only supports x64 when using the bundled Kissat solver.");
        }

        public bool[]? Solve(int _variableCount, int[]? _assumptions = null)
        {
            if(config is null)
                throw new InvalidOperationException();

            var Handle = KissatNative.kissat_init();
            try
            {
                KissatNative.kissat_set_option(Handle, "quiet", config.Verbosity == 0 ? 1 : 0);
                //KissatNative.kissat_set_option(Handle, "report", Verbosity > 0 ? 1 : 0);
                KissatNative.kissat_set_option(Handle, "verbose", Math.Max(0, config.Verbosity - 2));

                if ((config.Threads ?? 1) != 1)
                    throw new NotImplementedException("Kissat only supports single-threaded operation.");

                if (config.RandomSeed.HasValue)
                    KissatNative.kissat_set_option(Handle, "seed", config.RandomSeed.Value);

                if (config.InitialPhase.HasValue)
                    KissatNative.kissat_set_option(Handle, "phase", config.InitialPhase.Value ? 1 : 0);

                if(config.TimeLimit!=TimeSpan.Zero)
                    //TODO: kissat uses signals to stop the process --> use terminate callback instead
                    throw new NotImplementedException("Kissat does not yet support time limits.");

                switch (config.ExpectedOutcome)
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

                if (config.Verbosity >= 1)
                    KissatNative.kissat_banner("c ", Marshal.PtrToStringAnsi(KissatNative.kissat_signature())!);

                var satisfiable = KissatNative.kissat_solve(Handle);

                if (config.Verbosity >= 1)
                    KissatNative.kissat_print_statistics(Handle);

                switch (satisfiable)
                {
                    case 10:
                        //satisfiable
                        var res = new bool[_variableCount];
                        for (var i = 0; i < _variableCount; i++)
                            res[i] = KissatNative.kissat_value(Handle, i + 1) > 0;
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
            finally
            {
                KissatNative.kissat_release(Handle);
            }
        }

        public void AddClause(Span<int> _clause)
        {
            foreach(var v in _clause)
                clauses.Add(v);
            clauses.Add(0);
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


                disposedValue = true;
            }
        }

        ~Kissat()
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
            config = _config;
        }
    }

    public static class KissatNative
    {
        [DllImport("kissat.dll")]
        public static extern IntPtr kissat_init();

        [DllImport("kissat.dll")]
        public static extern void kissat_release(IntPtr wrapper);

        [DllImport("kissat.dll")]
        //TODO: [SuppressGCTransition]
        public static extern void kissat_add(IntPtr wrapper, int lit);

        //[DllImport("kissat.dll")]
        ////TODO: [SuppressGCTransition]
        //public static extern void kissat_assume(IntPtr wrapper, int lit);

        [DllImport("kissat.dll")]
        public static extern int kissat_solve(IntPtr wrapper);

        [DllImport("kissat.dll")]
        public static extern void kissat_terminate(IntPtr wrapper);

        [DllImport("kissat.dll")]
        //TODO: [SuppressGCTransition]
        public static extern int kissat_value(IntPtr wrapper, int lit);

        [DllImport("kissat.dll")]
        public static extern void kissat_print_statistics(IntPtr wrapper);

        [DllImport("kissat.dll")]
        public static extern int kissat_set_option(IntPtr wrapper, [In, MarshalAs(UnmanagedType.LPStr)] string name, int val);

        [DllImport("kissat.dll")]
        public static extern int kissat_banner([In, MarshalAs(UnmanagedType.LPStr)] string line_prefix, [In, MarshalAs(UnmanagedType.LPStr)] string name_of_app);

        [DllImport("kissat.dll")]
        public static extern IntPtr kissat_signature();
    }
}
