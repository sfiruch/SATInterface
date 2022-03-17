using System;
using System.Collections.Generic;
using System.Text;

namespace SATInterface.Solver
{
    public abstract class Solver : IDisposable
    {
        internal Model Model = null!;

        public abstract void AddClause(Span<int> _clause);

        public abstract (State State,bool[]? Vars) Solve(int _variableCount, long _timeout = long.MaxValue, int[]? _assumptions = null);

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
