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
    public class YalSAT : ISolver
    {
        private Configuration? config;
        List<int> clauses = new List<int>();

        public YalSAT()
        {
            if (!Environment.Is64BitProcess)
                throw new Exception("This library only supports x64 when using the bundled YalSAT solver.");
        }

        public bool[]? Solve(int _variableCount, int[]? _assumptions = null)
        {
            if(config is null)
                throw new InvalidOperationException();

            var Handle = YalSATNative.yals_new();
            try
            {
                YalSATNative.yals_setopt(Handle, "verbose", Math.Max(0, config.Verbosity-1));

                if ((config.Threads ?? 1) != 1)
                    throw new NotImplementedException("YalSAT only supports single-threaded operation.");

                if (config.RandomSeed.HasValue)
                    YalSATNative.yals_srand(Handle, (ulong)config.RandomSeed.Value);

                if (config.InitialPhase.HasValue)
                    throw new NotImplementedException("YalSAT does not yet support initial phase.");

                if (config.TimeLimit!=TimeSpan.Zero)
                    //TODO: yalsat uses signals to stop the process --> use terminate callback instead
                    throw new NotImplementedException("YalSAT does not yet support time limits.");

                switch (config.Target)
                {
                    case Target.FindAssignment:
                        break;
                    case Target.ProveUnsat:
                        break;
                    case Target.RandomSampledAssignment:
                        YalSATNative.yals_setopt(Handle, "pick", 0);
                        YalSATNative.yals_setopt(Handle, "pol", 0);
                        YalSATNative.yals_setopt(Handle, "cacheduni", 1);
                        YalSATNative.yals_setopt(Handle, "toggleuniform", 1);
                        break;
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

                if (config.Verbosity >= 1)
                    YalSATNative.yals_stats(Handle);

                switch (satisfiable)
                {
                    case 10:
                        //satisfiable
                        var res = new bool[_variableCount];
                        for (var i = 0; i < _variableCount; i++)
                            res[i] = YalSATNative.yals_deref(Handle, i + 1) > 0;
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
                YalSATNative.yals_del(Handle);
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

        ~YalSAT()
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

    public static class YalSATNative
    {
        [DllImport("YalSAT.dll")]
        public static extern IntPtr yals_new();
        [DllImport("YalSAT.dll")]
        public static extern void yals_del(IntPtr wrapper);
        [DllImport("YalSAT.dll")]
        public static extern void yals_srand(IntPtr wrapper, ulong seed);
        [DllImport("YalSAT.dll")]
        public static extern int yals_setopt(IntPtr wrapper, [In, MarshalAs(UnmanagedType.LPStr)] string name, int val);
        [DllImport("YalSAT.dll")]
        public static extern void yals_setphase(IntPtr wrapper, int lit);
        [DllImport("YalSAT.dll")]
        public static extern void yals_add(IntPtr wrapper, int lit);
        [DllImport("YalSAT.dll")]
        public static extern int yals_sat(IntPtr wrapper);
        [DllImport("YalSAT.dll")]
        public static extern void yals_stats(IntPtr wrapper);
        [DllImport("YalSAT.dll")]
        public static extern int yals_deref(IntPtr wrapper, int lit);
    }
}
