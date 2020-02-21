using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
    internal class NotExpr : BoolExpr
    {
        public BoolVar inner;

        internal NotExpr(BoolVar _inner)
        {
            inner = _inner;
        }

        internal static BoolExpr Create(BoolExpr _inner)
        {
            if (_inner is NotExpr)
                return ((NotExpr)_inner).inner;
            else if (ReferenceEquals(_inner, Model.True))
                return Model.False;
            else if (ReferenceEquals(_inner, Model.False))
                return Model.True;
            else if (_inner is BoolVar)
                return ((BoolVar)_inner).Negated;
            else if (_inner is AndExpr)
                return OrExpr.Create(((AndExpr)_inner).elements.Select(e => !e).ToArray());
            else if (_inner is OrExpr)
                return AndExpr.Create(((OrExpr)_inner).elements.Select(e => !e).ToArray());
            else
                throw new NotImplementedException();
        }

        public override BoolExpr Flatten() => this;

        internal override IEnumerable<BoolVar> EnumVars()
        {
            yield return inner;
        }

        public override string ToString() => "!" + inner;

        public override bool X => !inner.X;

        public override int VarCount => 1;

        public override bool Equals(object _obj) => (_obj is NotExpr ne) && inner.Equals(ne.inner);

        public override int GetHashCode() => ~inner.GetHashCode();
    }
}
