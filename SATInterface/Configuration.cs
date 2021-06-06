using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SATInterface
{
    public enum OptimizationFocus
    {
        /// <summary>
        /// Binary search
        /// </summary>
        Balanced,

        /// <summary>
        /// Focus on finding incumbents
        /// </summary>
        Incumbent,

        /// <summary>
        /// Focus on proving bounds
        /// </summary>
        Bound
    }

    public enum InternalSolver
    {
        CryptoMiniSat,
        CaDiCaL,
        Kissat
    }

    public class Configuration
    {
        /// <summary>
        /// Strategy to solve minimization/maximization problems.
        /// Default: Balanced
        /// </summary>
        public OptimizationFocus OptimizationFocus = OptimizationFocus.Balanced;
        
        /// <summary>
        /// Verbosity of the solver logging. Set to 0 to disable logging.
        /// Default: 2
        /// </summary>
        public int Verbosity = 2;

        /// <summary>
        /// Bundled SAT solver to use.
        /// Default: CaDiCaL
        /// </summary>
        public InternalSolver Solver = InternalSolver.CaDiCaL;

        /// <summary>
        /// Number of threads the solver may use.
        /// Default: null
        /// </summary>
        public int? Threads;


        /// <summary>
        /// Set to true to allow saving the model to a stream/file
        /// </summary>
        public bool EnableDIMACSWriting;


        internal void Validate()
        {
            if ((Threads ?? 1) < 1)
                throw new ArgumentOutOfRangeException(nameof(Threads));

            if ((ConsoleSolverLines ?? 1) < 1)
                throw new ArgumentOutOfRangeException(nameof(ConsoleSolverLines));

            if (Verbosity < 0)
                throw new ArgumentOutOfRangeException(nameof(Verbosity));
        }

        /// <summary>
        /// Random seed used by the solver for tie-breaking.
        /// Default: null
        /// </summary>
        public int? RandomSeed;

        /// <summary>
        /// Initial phase of variables.
        /// Default: null
        /// </summary>
        public bool? InitialPhase;

        /// <summary>
        /// Number of lines the solver output will use, after
        /// which the solver log will scroll.
        /// Default: 30
        /// </summary>
        public int? ConsoleSolverLines = 30;





        public Configuration Clone()
            => new Configuration()
            {
                OptimizationFocus=OptimizationFocus,
                Verbosity=Verbosity,
                Solver=Solver,
                Threads=Threads,
                RandomSeed=RandomSeed,
                InitialPhase=InitialPhase,
                EnableDIMACSWriting=EnableDIMACSWriting,
            };

        //TODO: Time limit
        //public TimeSpan TimeLimit = TimeSpan.Zero;
    }
}
