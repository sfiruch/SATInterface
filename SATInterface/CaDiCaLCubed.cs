using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SATInterface
{
    /// <summary>
    /// Managed-code facade of the native CaDiCaL solver
    /// </summary>
    public class CaDiCaLCubed : ISolver
    {
        private int Verbosity;
        int varCount = 0;
        private int Threads = Environment.ProcessorCount;
        private int CubeDepth = 10;
        List<int[]> clauses = new List<int[]>();

        public CaDiCaLCubed()
        {
            if (!Environment.Is64BitProcess)
                throw new Exception("This library only supports x64 when using the bundled CaDiCaL solver.");
        }

        public bool[]? Solve(int[]? _assumptions = null)
        {
            var cubes = new List<int[]>();
            bool[]? sol = null;

            var conX = Console.CursorLeft;
            var conY = Console.CursorTop;

            using var tlHandles = new ThreadLocal<IntPtr>(true);

            bool[]? CreateCubes(List<int> assumptions)
            {
                if (!(sol is null))
                    return sol;

                if (assumptions.Count >= CubeDepth)
                {
                    cubes.Add(assumptions.ToArray());
                    Console.Write('.');
                    return sol;
                }

                if (!tlHandles.IsValueCreated)
                {
                    var newHandle = CaDiCaLNative.ccadical_init();
                    CaDiCaLNative.ccadical_set_option(newHandle, "quiet", 1);
                    CaDiCaLNative.ccadical_set_option(newHandle, "report", 0);
                    CaDiCaLNative.ccadical_set_option(newHandle, "verbose", 0);
                    foreach (var c in clauses)
                    {
                        foreach (var i in c)
                            CaDiCaLNative.ccadical_add(newHandle, i);
                        CaDiCaLNative.ccadical_add(newHandle, 0);
                    }
                    if (_assumptions != null)
                        foreach (var a in _assumptions)
                        {
                            CaDiCaLNative.ccadical_add(newHandle, a);
                            CaDiCaLNative.ccadical_add(newHandle, 0);
                        }

                    CaDiCaLNative.ccadical_limit(newHandle, "preprocessing", 10);
                    CaDiCaLNative.ccadical_limit(newHandle, "conflicts", 10000);
                    switch (CaDiCaLNative.ccadical_solve(newHandle))
                    {
                        case 10:
                            //satisfiable
                            var res = new bool[varCount];
                            for (var i = 0; i < varCount; i++)
                                res[i] = CaDiCaLNative.ccadical_val(newHandle, i + 1) > 0;
                            return sol = res;

                        case 20:
                            //unsat
                            return sol;

                        case 0:
                            //interrupted
                            break;

                        default:
                            throw new Exception();
                    }

                    tlHandles.Value = newHandle;
                }

                foreach (var a in assumptions)
                    CaDiCaLNative.ccadical_assume(tlHandles.Value, a);

                var lit = CaDiCaLNative.ccadical_lookahead(tlHandles.Value);
                if (lit == 0)
                {
                    cubes.Insert(0, assumptions.ToArray());
                    Console.Write('.');
                    return sol;
                }

                bool[]? s1 = null;
                bool[]? s2 = null;
                Parallel.Invoke(
                    () =>
                    {
                        var assumptionsA = new List<int>(assumptions);
                        assumptionsA.Add(lit);
                        s1 = CreateCubes(assumptionsA);
                    },
                    () =>
                    {
                        var assumptionsB = new List<int>(assumptions);
                        assumptionsB.Add(-lit);
                        s2 = CreateCubes(assumptionsB);
                    }
                    );

                return sol ??= s1 ?? s2;

            }

            bool[]? solution = null;

            var s = CreateCubes(new List<int>());
            if (s != null)
                return s;

            foreach(var Handle in tlHandles.Values)
                CaDiCaLNative.ccadical_release(Handle);


            char[] a = new string('.', cubes.Count).ToCharArray();
            Parallel.ForEach(Partitioner.Create(Enumerable.Range(0, cubes.Count), EnumerablePartitionerOptions.NoBuffering), new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, () =>
            {
                var Handle = CaDiCaLNative.ccadical_init();
                CaDiCaLNative.ccadical_set_option(Handle, "quiet", 1);
                CaDiCaLNative.ccadical_set_option(Handle, "report", 0);
                CaDiCaLNative.ccadical_set_option(Handle, "verbose", 0);
                foreach (var c in clauses)
                {
                    foreach (var i in c)
                        CaDiCaLNative.ccadical_add(Handle, i);
                    CaDiCaLNative.ccadical_add(Handle, 0);
                }
                if (_assumptions != null)
                    foreach (var a in _assumptions)
                    {
                        CaDiCaLNative.ccadical_add(Handle, a);
                        CaDiCaLNative.ccadical_add(Handle, 0);
                    }
                return Handle;
            }, (cubeI, pls, Handle) =>
            {
                lock (a)
                {
                    a[cubeI] = '*';
                    Console.SetCursorPosition(conX, conY);
                    Console.WriteLine(new string(a));
                }

                var cube = cubes[cubeI];
                foreach (var i in cube)
                    CaDiCaLNative.ccadical_assume(Handle, i);

                var satisfiable = CaDiCaLNative.ccadical_solve(Handle);
                lock (a)
                {
                    a[cubeI] = '#';
                    Console.SetCursorPosition(conX, conY);
                    Console.WriteLine(new string(a));
                }

                switch (satisfiable)
                {
                    case 10:
                        //satisfiable
                        var res = new bool[varCount];
                        for (var i = 0; i < varCount; i++)
                            res[i] = CaDiCaLNative.ccadical_val(Handle, i + 1) > 0;
                        pls.Stop();

                        solution = res;
                        break;

                    case 20:
                        //unsat, try other cubes
                        break;

                    case 0: //interrupted
                    default:
                        throw new Exception();
                }

                return Handle;
            }, Handle => CaDiCaLNative.ccadical_release(Handle));

            return solution;
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

        ~CaDiCaLCubed()
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
            if (_config.Threads.HasValue)
                Threads = _config.Threads.Value;
        }
    }
}
