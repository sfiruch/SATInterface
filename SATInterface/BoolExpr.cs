using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SATInterface
{
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
    public class BoolExpr
#pragma warning restore CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
#pragma warning restore CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        public static readonly BoolExpr TRUE = new BoolVar("true");
        public static readonly BoolExpr FALSE = new BoolVar("false");

        internal BoolExpr()
        {
        }

        public static bool operator true(BoolExpr _be) => ReferenceEquals(TRUE,_be);
        public static bool operator false(BoolExpr _be) => ReferenceEquals(FALSE, _be);

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
            if (this is BoolVar)
                return this;
            else if (ReferenceEquals(this, TRUE))
                return TRUE;
            else if (ReferenceEquals(this, FALSE))
                return FALSE;
            else
            {
                var res = new BoolVar(_model);
                _model.AddConstr(res == this);
                return res;
            }
        }

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

            return NotExpr.Create(_v);
        }

        public static BoolExpr operator |(BoolExpr lhs, BoolExpr rhs) => OrExpr.Create(lhs, rhs);
        public static BoolExpr operator &(BoolExpr lhs, BoolExpr rhs) => AndExpr.Create(lhs, rhs);
        public static BoolExpr operator ==(BoolExpr lhsS, BoolExpr rhsS)
        {
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

        public static BoolExpr operator ^(BoolExpr lhsS, BoolExpr rhsS)
        {
            if (ReferenceEquals(lhsS, rhsS))
                return FALSE;

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
