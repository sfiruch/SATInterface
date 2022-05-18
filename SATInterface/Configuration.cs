using SATInterface.Solver;
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

    public enum ExpectedOutcome
    {
        /// <summary>
        /// Unkown result. Will rely on default solver configuration.
        /// </summary>
        Unknown,

        /// <summary>
        /// Likely SAT. Prioritize finding assignment.
        /// </summary>
        Sat,

        /// <summary>
        /// Likely UNSAT. Prioritize finding proof.
        /// </summary>
        Unsat,

        //doesn't appear to work as intended
        ///// <summary>
        ///// Find randomly sampled assignment
        ///// </summary>
        //RandomSampledAssignment
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
        /// Default: new SATInterface.Solver.CaDiCaL()
        /// </summary>
        public Solver.Solver Solver = new CaDiCaL();

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
        /// Default: 35
        /// </summary>
        public int? ConsoleSolverLines = 35;

        /// <summary>
        /// Solver time limit
        /// </summary>
        public TimeSpan TimeLimit = TimeSpan.Zero;

        /// <summary>
        /// Expected outcome.
        /// Default: ExpectedOutcome.Unknown
        /// </summary>
        public ExpectedOutcome ExpectedOutcome = ExpectedOutcome.Unknown;

        /// <summary>
        /// When LinExpr contains more variables than this threshold, use
        /// binary arithmetic for comparisons. Improved sequence counters
        /// will be used below this threshold.
        /// Default: 3
        /// </summary>
        public int LinExprBinaryComparisonThreshold = 3;
    }
}
