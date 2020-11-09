using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace SATInterface
{
    /// <summary>
    /// Managed-code facade of the native Kissat solver
    /// </summary>
    public class Kissat : ISolver
    {
        private Configuration config = new Configuration();
        private int Verbosity;
        int varCount = 0;
        List<int[]> clauses = new List<int[]>();

        public Kissat()
        {
            if (!Environment.Is64BitProcess)
                throw new Exception("This library only supports x64 when using the bundled Kissat solver.");
        }

        public bool[]? Solve(int[]? _assumptions = null)
        {
            var Handle = KissatNative.kissat_init();
            try
            {
                Verbosity = Math.Max(0, config.Verbosity - 1);
                KissatNative.kissat_set_option(Handle, "quiet", Verbosity == 0 ? 1 : 0);
                //KissatNative.kissat_set_option(Handle, "report", Verbosity > 0 ? 1 : 0);
                KissatNative.kissat_set_option(Handle, "verbose", Math.Max(0, Verbosity - 1));

                if ((config.Threads ?? 1) != 1)
                    throw new NotImplementedException("Kissat only supports single-threaded operation.");

                if (config.RandomSeed.HasValue)
                    KissatNative.kissat_set_option(Handle, "seed", config.RandomSeed.Value);

                if (config.InitialPhase.HasValue)
                    KissatNative.kissat_set_option(Handle, "phase", config.InitialPhase.Value ? 1 : 0);

                foreach (var c in clauses)
                {
                    foreach (var i in c)
                        KissatNative.kissat_add(Handle, i);
                    KissatNative.kissat_add(Handle, 0);
                }

                if (_assumptions != null)
                {
                    foreach (var a in _assumptions)
                    {
                        KissatNative.kissat_add(Handle, a);
                        KissatNative.kissat_add(Handle, 0);
                    }
                }

                if (Verbosity >= 1)
                {
                    //TODO: add banner to C API
                    //Console.WriteLine("c " + Marshal.PtrToStringAnsi(KissatNative.kissat_signature()));
                    Console.WriteLine("c Kissat 1.0.3");
                }

                var satisfiable = KissatNative.kissat_solve(Handle);

                if (Verbosity >= 1)
                    KissatNative.kissat_print_statistics(Handle);

                switch (satisfiable)
                {
                    case 10:
                        //satisfiable
                        var res = new bool[varCount];
                        for (var i = 0; i < varCount; i++)
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

        public void AddVars(int _number) => varCount += _number;

        public bool AddClause(Span<int> _clause)
        {
            clauses.Add(_clause.ToArray());
            return true;
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
        public static extern void kissat_add(IntPtr wrapper, int lit);

        //[DllImport("kissat.dll")]
        //public static extern void kissat_assume(IntPtr wrapper, int lit);

        [DllImport("kissat.dll")]
        public static extern int kissat_solve(IntPtr wrapper);

        [DllImport("kissat.dll")]
        public static extern void kissat_terminate(IntPtr wrapper);

        [DllImport("kissat.dll")]
        public static extern int kissat_value(IntPtr wrapper, int lit);

        [DllImport("kissat.dll")]
        public static extern void kissat_print_statistics(IntPtr wrapper);

        [DllImport("kissat.dll")]
        public static extern int kissat_set_option(IntPtr wrapper, [In, MarshalAs(UnmanagedType.LPStr)] string name, int val);

        //[DllImport("kissat.dll")]
        //public static extern IntPtr kissat_signature();
    }
}
