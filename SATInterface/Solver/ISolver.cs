using System;
using System.Collections.Generic;
using System.Text;

namespace SATInterface.Solver
{
    public interface ISolver:IDisposable
    {
        public void AddClause(Span<int> _clause);

        public bool[]? Solve(int _variableCount, int[]? _assumptions = null);

        internal void ApplyConfiguration(Configuration _config);
    }
}
