using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class BoolVar:BoolExpr
    {
        internal readonly int Id;
        internal bool Value;
        internal readonly Model Model;
        private readonly string Name;
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

        public BoolVar(Model _model):this(_model,"b"+(_model.VarCount+1))
        {
        }

        public override BoolExpr Flatten() => this;

        public BoolVar(Model _model,string _name)
        {
            Model = _model;
            Name = _name;
            Id = ++_model.VarCount;
            Model.RegisterVariable(this);
        }

        internal BoolVar(string _name)
        {
            Name = _name;
            Model = null!;
        }

        public override string ToString() => Name;

        public override bool X
        {
            get
            {
                if (ReferenceEquals(this, True))
                    return true;
                if (ReferenceEquals(this, False))
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
