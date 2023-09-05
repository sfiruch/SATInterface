using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace SATInterface.Solver
{
    public abstract class Solver:IDisposable //<T> : IDisposable where T:struct,IBinaryInteger<T>
    {
        internal Model Model = null!;

        /// <summary>
        /// Adds the clause to the model.
        /// </summary>
        public abstract void AddClause(ReadOnlySpan<int> _clause);

        /// <summary>
        /// Solves the model.
        /// </summary>
        /// <param name="_variableCount">Assignments for all variables from 1 to _variableCount will be returned.</param>
        /// <param name="_timeout">Solution process should be aborted when Environment.TickCount64 >= _timeout.</param>
        /// <param name="_assumptions">The supplied variables must be true in a valid assignment.</param>
        /// <returns></returns>
        public abstract (State State,bool[]? Vars) Solve(int _variableCount, long _timeout = long.MaxValue, int[]? _assumptions = null);

        /// <summary>
        /// Randomly sample a valid assignment.
        /// </summary>
        /// <param name="_variableCount">Assignments for all variables from 1 to _variableCount will be returned.</param>
        /// <param name="_timeout">Solution process should be aborted when Environment.TickCount64 >= _timeout.</param>
        /// <param name="_assumptions">The supplied variables must be true in a valid assignment.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual IEnumerable<bool[]> RandomSample(int _variableCount, long _timeout = long.MaxValue, int[]? _assumptions = null)
            => throw new NotImplementedException();

        internal abstract void ApplyConfiguration();

        protected virtual void DisposeManaged()
        {
        }

        protected virtual void DisposeUnmanaged()
        {
        }

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    DisposeManaged();

                DisposeUnmanaged();
                disposedValue = true;
            }
        }

        ~Solver()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
