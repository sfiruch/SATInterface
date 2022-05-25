using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SATInterface
{
    /// <summary>
    /// A BoolVar is either True or False in a SAT model.
    /// </summary>
    internal class BoolVar : BoolExpr
    {
        internal readonly int Id;
        internal bool Value;
        internal readonly Model? Model;
        private BoolExpr? negated;

        internal BoolExpr Negated
        {
            get
            {
                if (negated is null)
                    negated = new NotExpr(this);
                return negated;
            }
        }

        public override BoolExpr Flatten() => this;

        internal BoolVar(Model _model)
        {
            Model = _model;
            Id = _model.VariableCount + 1;
            Model.RegisterVariable(this);
        }

        /// <summary>
        /// This is only used for the global True and False constants which should be
        /// short-circuited away anyway before hitting the solver.
        /// </summary>
        internal BoolVar(int _id)
        {
            Model = null!;
            Id = _id;
        }

        public override string ToString() => $"b{Id}";

        public override bool X
        {
            get
            {
                if (ReferenceEquals(this, Model.True))
                    return true;
                if (ReferenceEquals(this, Model.False))
                    return false;

                if (Model!.State == State.Unsatisfiable)
                    throw new InvalidOperationException("Model is UNSAT");

                return Value;
            }
        }

        public override int VarCount => 1;

        internal override IEnumerable<BoolVar> EnumVars()
        {
            yield return this;
        }
        internal override Model? GetModel() => Model;

        public override int GetHashCode() => Id;

        public override bool Equals(object? obj) => (obj is BoolVar bv) && bv.Id == Id;
    }
}
