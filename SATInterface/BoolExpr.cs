using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class BoolExpr
    {
        public static readonly BoolExpr TRUE = new BoolVar(null, "true");
        public static readonly BoolExpr FALSE = new BoolVar(null, "false");

        internal BoolExpr Simplified = null;

        internal BoolExpr()
        {
        }

        public static implicit operator BoolExpr(bool _v) => _v ? TRUE:FALSE;

        public virtual bool X
        {
            get
            {
                if (ReferenceEquals(this, TRUE))
                    return true;
                if (ReferenceEquals(this, FALSE))
                    return false;

                throw new Exception();
            }
        }


        public BoolExpr Flatten(Model _model)
        {
            var simp = Simplify();
            if (simp is BoolVar)
                return simp;
            else if (ReferenceEquals(simp, TRUE))
                return TRUE;
            else if (ReferenceEquals(simp, FALSE))
                return FALSE;
            else
            {
                var res = new BoolVar(_model);
                _model.AddConstr(res == Simplify());
                return res;
            }
        }


        internal virtual BoolExpr Simplify() => this;

        internal virtual IEnumerable<BoolVar> EnumVars()
        {
            yield break;
        }

        public override string ToString()
        {
            if (ReferenceEquals(this, TRUE))
                return "true";
            if (ReferenceEquals(this, FALSE))
                return "false";

            throw new Exception();
        }

        public override bool Equals(object obj) => ReferenceEquals(this, obj);

        public override int GetHashCode() => GetHashCode();

        public static BoolExpr operator !(BoolExpr _v)
        {
            if (ReferenceEquals(_v, FALSE))
                return TRUE;
            if (ReferenceEquals(_v, TRUE))
                return FALSE;

            if (_v is NotExpr)
                return ((NotExpr)_v).inner;

            if (_v is BoolVar)
                return ((BoolVar)_v).Negated;

            return new NotExpr(_v);
        }

        public static BoolExpr operator |(BoolExpr lhs, BoolExpr rhs) => new OrExpr(lhs, rhs);

        public static BoolExpr operator |(OrExpr lhs, BoolExpr rhs) => new OrExpr(lhs.elements.Concat(new BoolExpr[] { rhs }));
        public static BoolExpr operator |(BoolExpr lhs, OrExpr rhs) => new OrExpr(rhs.elements.Concat(new BoolExpr[] { lhs }));


        public static BoolExpr operator &(AndExpr lhs, BoolExpr rhs) => new AndExpr(lhs.elements.Concat(new BoolExpr[] { rhs }));
        public static BoolExpr operator &(BoolExpr lhs, AndExpr rhs) => new AndExpr(rhs.elements.Concat(new BoolExpr[] { lhs }));

        public static BoolExpr operator &(BoolExpr lhs, BoolExpr rhs) => new AndExpr(lhs, rhs);
        public static BoolExpr operator ==(BoolExpr lhs, BoolExpr rhs)
        {
            var lhsS = lhs.Simplify();
            var rhsS = rhs.Simplify();
            if (ReferenceEquals(lhsS, rhsS))
                return TRUE;

            if (ReferenceEquals(lhsS, TRUE))
                return rhsS;
            if (ReferenceEquals(lhsS, FALSE))
                return !rhsS;

            if (ReferenceEquals(rhsS, TRUE))
                return lhsS;
            if (ReferenceEquals(rhsS, FALSE))
                return !lhsS;

            //return (lhsS & rhsS) | (!lhsS & !rhsS);
            return (lhsS | !rhsS) & (rhsS | !lhsS);
        }

        public static BoolExpr operator !=(BoolExpr lhs, BoolExpr rhs) => Xor(lhs,rhs);

        public static BoolExpr operator >(BoolExpr lhs, BoolExpr rhs) => lhs & !rhs;
        public static BoolExpr operator <(BoolExpr lhs, BoolExpr rhs) => !lhs & rhs;
        public static BoolExpr operator >=(BoolExpr lhs, BoolExpr rhs) => lhs | !rhs;
        public static BoolExpr operator <=(BoolExpr lhs, BoolExpr rhs) => !lhs | rhs;

        public static BoolExpr operator ^(BoolExpr _lhs, BoolExpr _rhs)
        {
            var lhsS = _lhs.Simplify();
            var rhsS = _rhs.Simplify();

            if (ReferenceEquals(lhsS, rhsS))
                return false;

            if (ReferenceEquals(lhsS, FALSE))
                return rhsS;
            if (ReferenceEquals(lhsS, TRUE))
                return !rhsS;

            if (ReferenceEquals(rhsS, FALSE))
                return lhsS;
            if (ReferenceEquals(rhsS, TRUE))
                return !lhsS;

            return (lhsS | rhsS) & (!lhsS | !rhsS);
        }

        public static BoolExpr Xor(BoolExpr _lhs, BoolExpr _rhs) => _lhs ^ _rhs;

        public static BoolExpr Max(BoolExpr _lhs, BoolExpr _rhs) => _lhs | _rhs;
        public static BoolExpr Min(BoolExpr _lhs, BoolExpr _rhs) => _lhs & _rhs;


        public static BoolExpr ITE(BoolExpr _if, BoolExpr _then, BoolExpr _else) => ((!_if | _then) & (_if | _else) & (_then | _else)) | (_then & _else);

    }
}
