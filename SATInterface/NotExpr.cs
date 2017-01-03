using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class NotExpr:BoolExpr
    {
        public BoolExpr inner;

        internal NotExpr(BoolExpr _inner)
        {
            inner = _inner;
        }

        internal override IEnumerable<BoolVar> EnumVars()
        {
            foreach (var v in inner.EnumVars())
                yield return v;
        }

        public override string ToString() => "!" + inner;

        internal override BoolExpr Simplify()
        {
            if (!ReferenceEquals(Simplified, null))
                return Simplified;

            if (inner is NotExpr)
                Simplified = ((NotExpr)inner).inner.Simplify();
            else if (inner is AndExpr)
                Simplified = new OrExpr(((AndExpr)inner).elements.Select(e => !e).ToArray()).Simplify();
            else if (inner is OrExpr)
                Simplified = new AndExpr(((OrExpr)inner).elements.Select(e => !e).ToArray()).Simplify();
            else if (ReferenceEquals(inner, TRUE))
                Simplified = FALSE;
            else if (ReferenceEquals(inner, FALSE))
                Simplified = TRUE;
            else if (inner is BoolVar)
                Simplified = this;
            else
                throw new NotImplementedException();

            return Simplified;
        }

        public override bool X
        {
            get
            {
                return !inner.X;
            }
        }

        public override bool Equals(object _obj)
        {
            var other = _obj as NotExpr;
            if (ReferenceEquals(other, null))
                return false;

            return inner.Equals(other.inner);
        }

        public override int GetHashCode() => inner.GetHashCode() ^ (1 << 28);
    }
}
