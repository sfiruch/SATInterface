using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Numerics;

namespace SATInterface.Solver
{
    /// <summary>
    /// Supports execution of an external solver in a separate process
    /// </summary>
    public class ExternalSolver : Solver //where T : struct, IBinaryInteger<T>
	{
        private readonly string SolverExecutable;
        private readonly string? SolverArguments;
        private readonly string? FilenameInput;
        private readonly string? FilenameOutput;
        private readonly string NewLine;

        private readonly List<int[]> clauses = new();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_executable"></param>
        /// <param name="_arguments"></param>
        /// <param name="_inputFilename"></param>
        /// <param name="_outputFilename"></param>
        /// <param name="_newLine"></param>
        public ExternalSolver(string _executable, string? _arguments = null, string? _inputFilename = null, string? _outputFilename = null, string? _newLine = null)
        {
            SolverExecutable = _executable;
            SolverArguments = _arguments;
            FilenameInput = _inputFilename;
            FilenameOutput = _outputFilename;
            NewLine = _newLine ?? Environment.NewLine;
        }

        private void Write(TextWriter _out, int _variableCount, int[]? _assumptions = null)
        {
            _out.WriteLine("c Created by SATInterface");
            _out.WriteLine($"p cnf {_variableCount} {clauses.Count + (_assumptions?.Length ?? 0)}");
            foreach (var c in clauses)
                _out.WriteLine(string.Join(' ', c.ToArray().Append(0)));

            if (_assumptions is not null)
                foreach (var c in _assumptions)
                    _out.WriteLine($"{c} 0");
        }

        public override IEnumerable<bool[]> RandomSample(int _variableCount, long _timeout = long.MaxValue, int[]? _assumptions = null)
            => InternalSolve(_variableCount, _timeout, _assumptions).Where(s => s.State == State.Satisfiable).Select(s => s.Vars!);

        public override (State State, bool[]? Vars) Solve(int _variableCount, long _timeout = long.MaxValue, int[]? _assumptions = null)
        {
            var solutions = InternalSolve(_variableCount, _timeout, _assumptions).ToArray();
            if (solutions.Length == 0)
                return (State.Undecided, null);

            return solutions.Single();
        }

        protected IEnumerable<(State State, bool[]? Vars)> InternalSolve(int _variableCount, long _timeout = long.MaxValue, int[]? _assumptions = null)
        {
            if (FilenameInput is not null)
            {
                using var fin = File.CreateText(FilenameInput);
                Write(fin, _variableCount, _assumptions);
            }

            if (FilenameOutput is not null)
                File.Delete(FilenameOutput);

            var p = Process.Start(new ProcessStartInfo()
            {
                FileName = SolverExecutable,
                Arguments = SolverArguments ?? "",
                RedirectStandardInput = FilenameInput == null,
                RedirectStandardOutput = FilenameOutput == null,
                UseShellExecute = false
            });

            Thread? satWriterThread = null;
            if (FilenameInput is null)
            {
                satWriterThread = new Thread(new ParameterizedThreadStart(delegate
                {
                    p!.StandardInput.AutoFlush = false;
                    p!.StandardInput.NewLine = NewLine ?? "\n";
                    Write(p!.StandardInput, _variableCount, _assumptions);
                    p!.StandardInput.Close();
                }))
                {
                    IsBackground = true,
                    Name = "SAT Writer Thread"
                };
                satWriterThread.Start();
            }

            var timeout = (int)Math.Min(int.MaxValue, _timeout - Environment.TickCount64);
            if (timeout <= 0)
                yield break;

            var UNSAT = Array.Empty<bool>();

            using var cts = timeout == int.MaxValue ? new CancellationTokenSource() : new CancellationTokenSource(timeout);
            using var bc = new BlockingCollection<bool[]>(1);
            var t = Task.Run(() =>
            {
                if (FilenameOutput is not null)
                    p!.WaitForExit();

                var log = new List<string>();
                bool[]? assignments = null;

                using StreamReader output = FilenameOutput is null ? p!.StandardOutput : File.OpenText(FilenameOutput);
                for (var line = output.ReadLine(); line != null; line = output.ReadLine())
                {
                    if (line.StartsWith("v "))
                    {
                        Debug.Assert(assignments is not null);
                        foreach (var n in line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(s => int.Parse(s)))
                        {
                            if (n == 0)
                            {
                                bc.Add(assignments, cts.Token);
                                assignments = null;
                                break;
                            }
                            if (n > 0)
                                assignments[n - 1] = true;
                        }
                    }
                    else if (line.StartsWith("s SATISFIABLE"))
                    {
                        if (Model.Configuration.Verbosity > 0)
                            Console.WriteLine(line);

                        assignments = new bool[_variableCount];
                    }
                    else if (line.StartsWith("s UNSATISFIABLE"))
                    {
                        if (Model.Configuration.Verbosity > 0)
                            Console.WriteLine(line);

                        bc.Add(UNSAT, cts.Token);
                        break;
                    }
                    else if (Model.Configuration.Verbosity > 0)
                        Console.WriteLine(line);
                }

                bc.CompleteAdding();
            });

            try
            {
                for (; ; )
                {
                    State s;
                    bool[]? assignment = null;

                    try
                    {
                        assignment = bc.Take(cts.Token);

                        if (ReferenceEquals(assignment, UNSAT))
                        {
                            assignment = null;
                            s = State.Unsatisfiable;
                        }
                        else
                            s = State.Satisfiable;
                    }
                    catch (OperationCanceledException)
                    {
                        s = State.Undecided;
                    }
                    catch (InvalidOperationException)
                    {
                        //enumeration complete
                        yield break;
                    }

                    yield return (s, assignment);
                    if (s == State.Undecided)
                        yield break;
                }
            }
            finally
            {
                cts.Cancel();

                if (FilenameInput is null)
                    p?.StandardInput.Dispose();
                if (FilenameOutput is null)
                    p?.StandardOutput.Dispose();

                p?.Dispose();

                try
                {
                    //let task close the file
                    t.Wait();
                }
                catch
#pragma warning disable ERP022 // Unobserved exception in generic exception handler
                {
                }
#pragma warning restore ERP022 // Unobserved exception in generic exception handler

                if (FilenameInput is not null)
                    File.Delete(FilenameInput);
                if (FilenameOutput is not null)
                    File.Delete(FilenameOutput);
            }
        }

        public override void AddClause(ReadOnlySpan<int> _clause)
        {
            clauses.Add(_clause.ToArray());
        }

        internal override void ApplyConfiguration()
        {
        }
    }
}
