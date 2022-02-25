using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATInterface.Solver
{
    //TODO: refactor dimacsOutput to solver instead, support multiple solvers

    /// <summary>
    /// This NULL solver can be used to save memory when no
    /// solver is needed.
    /// </summary>
    public class NullSolver : ISolver
    {
        public void AddClause(Span<int> _clause)
        {
        }

        public void Dispose()
        {
        }

        public bool[]? Solve(int _variableCount, int[]? _assumptions = null)
        {
            throw new NotImplementedException();
        }

        void ISolver.ApplyConfiguration(Configuration _config)
        {
        }
    }
}
