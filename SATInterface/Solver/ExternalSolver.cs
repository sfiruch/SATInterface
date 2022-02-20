using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Diagnostics.CodeAnalysis;

namespace SATInterface.Solver
{
    /// <summary>
    /// Supports execution of an external solver in a separate process
    /// </summary>
    public class ExternalSolver : ISolver
    {
        private string SolverExecutable;
        private string? SolverArguments;
        private string? FilenameInput;
        private string? FilenameOutput;
        private string NewLine;
        private int Verbosity;

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

        public bool[]? Solve(int _variableCount, int[]? _assumptions = null)
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
            if (FilenameOutput is not null)
                p!.WaitForExit();

            var log = new List<string>();
            try
            {
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
                    if (tk.Length > 1 && tk[0] == "c" && FilenameOutput is null)
                    {
                        if (Verbosity > 0)
                            Console.WriteLine(line);
                    }
                    if (tk.Length == 2 && tk[0] == "s")
                    {
                        if (Verbosity > 0 && FilenameOutput is null)
                            Console.WriteLine(line);
                        if (tk[1] == "SATISFIABLE")
                        {
                            isSat = true;
                        }
                        else if (tk[1] == "UNSATISFIABLE")
                            return null;
                        else
                            throw new Exception(tk[2]);
                    }
                    else if (tk.Length >= 2 && tk[0] == "v")
                    {
                        foreach (var n in tk.Skip(1).Select(s => int.Parse(s)))
                            if (n > 0)
                                res[n-1] = true;
                    }
                }

                if (!isSat)
                    throw new Exception("Solver did not report SAT or UNSAT");

                return res;
            }
            finally
            {
                satWriterThread?.Interrupt();
                p!.WaitForExit();

                if (FilenameInput is null)
                    p!.StandardInput.Dispose();
                if (FilenameOutput is null)
                    p!.StandardOutput.Dispose();
                p!.Dispose();

                if (FilenameInput is not null)
                    File.Delete(FilenameInput);
                if (FilenameOutput is not null)
                    File.Delete(FilenameOutput);
            }
        }

        public void AddClause(Span<int> _clause)
        {
            clauses.Add(_clause.ToArray());
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

        ~ExternalSolver()
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
            Verbosity = _config.Verbosity;
        }
    }
}
