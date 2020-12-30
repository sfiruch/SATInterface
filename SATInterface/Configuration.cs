using System;
using System.Collections.Generic;
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
        //CaDiCaLCubed,
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
        /// Eliminating duplicate subexpressions makes setting up models
        /// more resource intensive, but solving potentially faster.
        /// Default: false
        /// </summary>
        public bool CommonSubexpressionElimination = false;

        //TODO: Time limit
        //public TimeSpan TimeLimit = TimeSpan.Zero;
    }
}
