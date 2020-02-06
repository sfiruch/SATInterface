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
        internal readonly Model Model;
        private readonly string? Name;
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

        internal BoolVar(Model _model, string? _name = null)
        {
            Model = _model;
            Name = _name;
            Id = ++_model.VarCount;
            Model.RegisterVariable(this);
        }

        /// <summary>
        /// This is only used for the global True and False constants which should be
        /// short-circuited away anyway before hitting the solver.
        /// </summary>
        /// <param name="_name"></param>
        internal BoolVar(string _name)
        {
            Name = _name;
            Model = null!;
        }

        public override string ToString() => Name ?? $"b{Id}";

        public override bool X
        {
            get
            {
                if (ReferenceEquals(this, Model.True))
                    return true;
                if (ReferenceEquals(this, Model.False))
                    return false;

                if (!Model.IsSatisfiable)
                    throw new InvalidOperationException("Model is not SAT");

                return Value;
            }
        }

        internal override IEnumerable<BoolVar> EnumVars()
        {
            yield return this;
        }

        public override int GetHashCode() => Id;

        public override bool Equals(object obj) => (obj is BoolVar bv) && bv.Id == Id;
    }
}
