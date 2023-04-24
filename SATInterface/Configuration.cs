﻿using SATInterface.Solver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SATInterface
{
    public enum OptimizationFocus
    {
        /// <summary>
        /// Bisection
        /// </summary>
        Bisection,

        /// <summary>
        /// Binary search
        /// </summary>
        Binary,

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

    [Flags]
    public enum ArcConstistencyClauses
    {
        /// <summary>
        /// Do not add clauses for arc-consistency
        /// </summary>
        None = 0,

        /// <summary>
        /// Add arc-consistency for ITE
        /// </summary>
        ITE = 1,

        /// <summary>
        /// Add partial arc-consistency for UInt arithmetic
        /// </summary>
        PartialArith = 2,

        /// <summary>
        /// Add full arc-consistency for UInt arithmetic
        /// </summary>
        FullArith = 6,

        /// <summary>
        /// Add arc-consistency for UInt arithmetic and ITE
        /// </summary>
        All = 7
    }

    public class Configuration
    {
        /// <summary>
        /// Strategy to solve minimization/maximization problems.
        /// 
        /// Default: Incumbent
        /// </summary>
        public OptimizationFocus OptimizationFocus = OptimizationFocus.Incumbent;

        /// <summary>
        /// Verbosity of the solver logging. Set to 0 to disable logging.
        /// 
        /// Default: 2
        /// </summary>
        public int Verbosity = 2;

        /// <summary>
        /// Bundled SAT solver to use.
        /// 
        /// Default: new SATInterface.Solver.CaDiCaL()
        /// </summary>
        public Solver.Solver Solver = new CaDiCaL();

        /// <summary>
        /// Number of threads the solver may use.
        /// 
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
        /// 
        /// Default: null
        /// </summary>
        public int? RandomSeed;

        /// <summary>
        /// Initial phase of variables.
        /// 
        /// Default: null
        /// </summary>
        public bool? InitialPhase;

        /// <summary>
        /// Number of lines the solver output will use, after
        /// which the solver log will scroll.
        /// 
        /// Default: 35
        /// </summary>
        public int? ConsoleSolverLines = 35;

        /// <summary>
        /// Solver time limit
        /// </summary>
        public TimeSpan TimeLimit = TimeSpan.Zero;

        /// <summary>
        /// Expected outcome.
        /// 
        /// Default: ExpectedOutcome.Unknown
        /// </summary>
        public ExpectedOutcome ExpectedOutcome = ExpectedOutcome.Unknown;

        /// <summary>
        /// Break large clauses into multiple smaller clauses. This is
        /// a work around for O(n^2)-algorithms in certain solvers.
        /// 
        /// Default: 64
        /// </summary>
        public int MaxClauseSize = 64;

        /// <summary>
        /// Controls which redundant clauses are added for arc-consistency.
        /// 
        /// Default: ArcConstistencyClauses.ITE
        /// </summary>
        public ArcConstistencyClauses AddArcConstistencyClauses = ArcConstistencyClauses.ITE;

        /// <summary>
        /// Valid assignments for (in)equalities are enumerated, if there are
        /// fewer assignments than this limit.
        /// 
        /// Default: 4
        /// </summary>
        public int EnumerateLinExprComparisonsLimit = 4;
    }
}
