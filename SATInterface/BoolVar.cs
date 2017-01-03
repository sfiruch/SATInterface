using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATInterface
{
    public class BoolVar:BoolExpr
    {
        static int NameCounter = 0;

        internal int Id;
        internal bool Value;
        internal Model Model;
        private readonly string Name;
        private BoolExpr negated;

        internal BoolExpr Negated
        {
            get
            {
                if (ReferenceEquals(negated, null))
                    negated = new NotExpr(this);
                return negated;
            }
        }

        public BoolVar(Model _model):this(_model,"b"+NameCounter++)
        {
        }

        public BoolVar(Model _model,string _name)
        {
            Model = _model;
            Name = _name;
        }

        public override string ToString() => Name;

        public override bool X
        {
            get
            {
                if (ReferenceEquals(this, TRUE))
                    return true;
                if (ReferenceEquals(this, FALSE))
                    return false;

                return Value;
            }
        }

        internal override IEnumerable<BoolVar> EnumVars()
        {
            yield return this;
        }

        internal void AssignModelId(Model _model)
        {
            if (Model != _model)
                throw new Exception("Already in another model");

            if (Id == 0)
                Id = Model.VarIdCounter++;
        }

        public override int GetHashCode() => Id;
        public override bool Equals(object obj) => ReferenceEquals(this, obj);
    }
}
