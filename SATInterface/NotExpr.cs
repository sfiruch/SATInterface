using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class NotExpr:BoolExpr
    {
        public BoolVar inner;

        internal NotExpr(BoolVar _inner)
        {
            inner = _inner;
        }

        public static BoolExpr Create(BoolExpr _inner)
        {
            if (_inner is NotExpr)
                return ((NotExpr)_inner).inner;
            else if (ReferenceEquals(_inner, TRUE))
                return FALSE;
            else if (ReferenceEquals(_inner, FALSE))
                return TRUE;
            else if (_inner is BoolVar)
                return ((BoolVar)_inner).Negated;
            else if (_inner is AndExpr)
                return OrExpr.Create(((AndExpr)_inner).elements.Select(e => !e).ToArray());
            else if (_inner is OrExpr)
                return AndExpr.Create(((OrExpr)_inner).elements.Select(e => !e).ToArray());
            else
                throw new NotImplementedException();
        }

        internal override IEnumerable<BoolVar> EnumVars()
        {
            yield return inner;
        }

        public override string ToString() => "!" + inner;

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

        public override int GetHashCode() => ~inner.GetHashCode();
    }
}
