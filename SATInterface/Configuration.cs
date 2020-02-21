using System;
using System.Collections.Generic;
using System.Text;

namespace SATInterface
{
    public enum OptimizationStrategy
    {
        BinarySearch,
        Increasing,
        Decreasing
    }

    public enum InternalSolver
    {
        CryptoMiniSat,
        CaDiCaL
    }

    public class Configuration
    {
        /// <summary>
        /// Strategy to solve minimization/maximization problems.
        /// </summary>
        public OptimizationStrategy OptimizationStrategy = OptimizationStrategy.BinarySearch;
        
        /// <summary>
        /// Verbosity of the solver logging. Set to 0 to disable logging.
        /// </summary>
        public int Verbosity = 1;

        /// <summary>
        /// Bundled SAT solver to use.
        /// </summary>
        public InternalSolver Solver = InternalSolver.CaDiCaL;

        /// <summary>
        /// Number of threads the solver may use.
        /// </summary>
        public int? Threads;

        /// <summary>
        /// Random seed used by the solver for tie-breaking.
        /// </summary>
        public int? RandomSeed;

        /// <summary>
        /// Initial phase of variables.
        /// </summary>
        public bool? InitialPhase;

        /// <summary>
        /// Eliminating duplicate subexpressions makes setting up models
        /// more resource intensive, but solving potentially faster.
        /// </summary>
        public bool CommonSubexpressionElimination = false;

        //TODO: Time limit
    }
}
