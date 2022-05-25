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
            if (_inner is NotExpr ne)
                return ne.inner;
            else if (ReferenceEquals(_inner, Model.True))
                return Model.False;
            else if (ReferenceEquals(_inner, Model.False))
                return Model.True;
            else if (_inner is BoolVar bv)
                return bv.Negated;
            else if (_inner is AndExpr ae)
            {
                var be = new BoolExpr[ae.Elements.Length];
                for (var i = 0; i < be.Length; i++)
                    be[i] = !ae.Elements[i];
                return OrExpr.Create(be);
            }
            else if (_inner is OrExpr oe)
            {
                var be = new BoolExpr[oe.Elements.Length];
                for (var i = 0; i < be.Length; i++)
                    be[i] = !oe.Elements[i];
                return AndExpr.Create(be);
            }
            else
                throw new NotImplementedException();
        }

        public override BoolExpr Flatten() => this;

        internal override IEnumerable<BoolVar> EnumVars()
        {
            yield return inner;
        }

        internal override Model? GetModel() => inner.GetModel();

        public override string ToString() => "!" + inner;

        public override bool X => !inner.X;

        public override int VarCount => 1;

        public override bool Equals(object? _obj) => (_obj is NotExpr ne) && inner.Equals(ne.inner);

        public override int GetHashCode() => ~inner.GetHashCode();
    }
}
