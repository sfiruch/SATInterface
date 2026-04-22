using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SATInterface.Solver
{
    /// <summary>
    /// This NULL solver can be used to save memory when no
    /// solver is needed.
    /// </summary>
    public class NullSolver : Solver //where T : struct, IBinaryInteger<T>
	{
        public override void AddClause(ReadOnlySpan<int> _clause)
        {
        }

        public override (State State, bool[]? Vars) Solve(int _variableCount, long _timeout=long.MaxValue, int[]? _assumptions = null, in CancellationToken? _ct = null)
            => throw new NotImplementedException();

        internal override void ApplyConfiguration()
        {
        }
    }
}
