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

namespace SATInterface.Solver
{
    /// <summary>
    /// Supports execution of an external solver in a separate process
    /// </summary>
    public class ExternalSolver : Solver
    {
        private string SolverExecutable;
        private string? SolverArguments;
        private string? FilenameInput;
        private string? FilenameOutput;
        private string NewLine;

        private List<int[]> clauses = new List<int[]>();

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

        public override (State State, bool[]? Vars) Solve(int _variableCount, long _timeout = long.MaxValue, int[]? _assumptions = null)
        {
            if (FilenameInput is not null)
            {
                using var fin = File.CreateText(FilenameInput);
                Write(fin, _variableCount, _assumptions);
            }

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

            var t = Task.Run<(State, bool[]?)>(() =>
            {
                if (FilenameOutput is not null)
                    p!.WaitForExit();

                var log = new List<string>();
                var isSat = false;
                var res = new bool[_variableCount];

                using StreamReader output = FilenameOutput is null ? p!.StandardOutput : File.OpenText(FilenameOutput);
                for (var line = output.ReadLine(); line != null; line = output.ReadLine())
                {
                    var tk = line.Split(' ').Where(e => e != "").ToArray();
                    if (tk.Length == 0)
                    {
                        //skip empty lines
                    }
                    else if (tk.Length > 1 && tk[0] == "c" && FilenameOutput is null)
                    {
                        if (Model.Configuration.Verbosity > 0)
                            Console.WriteLine(line);
                    }
                    else if (tk.Length == 2 && tk[0] == "s")
                    {
                        if (Model.Configuration.Verbosity > 0 && FilenameOutput is null)
                            Console.WriteLine(line);
                        if (tk[1] == "SATISFIABLE")
                        {
                            isSat = true;
                        }
                        else if (tk[1] == "UNSATISFIABLE")
                            return (State.Unsatisfiable, null);
                        else
                            throw new Exception($"Unexpected status {tk[2]}");
                    }
                    else if (tk.Length >= 2 && tk[0] == "v")
                    {
                        foreach (var n in tk.Skip(1).Select(s => int.Parse(s)))
                            if (n > 0)
                                res[n - 1] = true;
                    }
                }

                if (!isSat)
                    return (State.Undecided, null);

                return (State.Satisfiable, res);
            });

            try
            {
                if (_timeout == long.MaxValue)
                    t.Wait();
                else
                {
                    var timeout = (int)Math.Min(int.MaxValue, _timeout - Environment.TickCount64);
                    if (timeout <= 0 || !t.Wait(timeout))
                        return (State.Undecided, null);
                }

                return t.Result;
            }
            finally
            {
                p!.Kill(true);
                p.WaitForExit();

                if (FilenameInput is null)
                    p.StandardInput.Dispose();
                if (FilenameOutput is null)
                    p.StandardOutput.Dispose();

                p.Dispose();

                try
                {
                    //let task close the file
                    t.Wait();
                }
                catch
                {
                }

                if (FilenameInput is not null)
                    File.Delete(FilenameInput);
                if (FilenameOutput is not null)
                    File.Delete(FilenameOutput);
            }
        }

        public override void AddClause(Span<int> _clause)
        {
            clauses.Add(_clause.ToArray());
        }

        internal override void ApplyConfiguration()
        {
        }
    }
}
