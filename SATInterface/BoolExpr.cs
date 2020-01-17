using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class BoolExpr
    {
        public static readonly BoolExpr True = new BoolVar("true");
        public static readonly BoolExpr False = new BoolVar("false");
        
        //TODO cache flattened expressions
        //private BoolVar? flattened;

        internal BoolExpr()
        {
        }

        public static bool operator true(BoolExpr _be) => ReferenceEquals(True, _be);
        public static bool operator false(BoolExpr _be) => ReferenceEquals(False, _be);

        public static implicit operator BoolExpr(bool _v) => _v ? True : False;

        public virtual bool X
        {
            get
            {
                if (ReferenceEquals(this, True))
                    return true;
                if (ReferenceEquals(this, False))
                    return false;

                throw new InvalidOperationException("This expression could not be pre-solved to true or false.");
            }
        }


        public virtual BoolExpr Flatten()
        {
            if (ReferenceEquals(this, True) || ReferenceEquals(this, False))
                return this;

            var model = EnumVars().First().Model;
            var res = new BoolVar(model);
            model.AddConstr(res == this);
            return res;
        }

        internal virtual IEnumerable<BoolVar> EnumVars()
        {
            yield break;
        }

        public override string ToString()
        {
            if (ReferenceEquals(this, True))
                return "true";
            if (ReferenceEquals(this, False))
                return "false";

            throw new InvalidOperationException("This expression could not be pre-solved to true or false.");
        }

        public static BoolExpr operator !(BoolExpr _v)
        {
            if (_v is null)
                throw new ArgumentNullException();

            if (ReferenceEquals(_v, False))
                return True;

            if (ReferenceEquals(_v, True))
                return False;

            if (_v is NotExpr)
                return ((NotExpr)_v).inner;

            if (_v is BoolVar)
                return ((BoolVar)_v).Negated;

            return NotExpr.Create(_v);
        }


        public static BoolExpr operator |(BoolExpr lhs, BoolExpr rhs) => OrExpr.Create(lhs, rhs);
        public static BoolExpr operator &(BoolExpr lhs, BoolExpr rhs) => AndExpr.Create(lhs, rhs);

        //TODO track equality for CSE
        public static BoolExpr operator ==(BoolExpr lhsS, BoolExpr rhsS)
        {
            if (lhsS is null && rhsS is null)
                return True;
            if (lhsS is null)
                return False;
            if (rhsS is null)
                return False;


            if (ReferenceEquals(lhsS, rhsS))
                return True;

            if (ReferenceEquals(lhsS, True))
                return rhsS;
            if (ReferenceEquals(lhsS, False))
                return !rhsS;

            if (ReferenceEquals(rhsS, True))
                return lhsS;
            if (ReferenceEquals(rhsS, False))
                return !lhsS;

            if (rhsS is AndExpr andExpr && andExpr.elements.Length > 8)
            {
                var other = lhsS.Flatten();
                var ands = new List<BoolExpr>();
                ands.Add(OrExpr.Create(andExpr.elements.Select(e => !e).Append(other)));
                foreach (var e in andExpr.elements)
                    ands.Add(e | !other);
                return AndExpr.Create(ands);
            }
            if (!(rhsS is AndExpr) && lhsS is AndExpr)
                return (rhsS == lhsS);

            if (rhsS is OrExpr orExpr && orExpr.elements.Length > 8)
            {
                var other = lhsS.Flatten();
                var ands = new List<BoolExpr>();
                ands.Add(OrExpr.Create(orExpr.elements.Append(!other)));
                foreach (var e in orExpr.elements)
                    ands.Add(!e | other);
                return AndExpr.Create(ands);
            }
            if (!(rhsS is OrExpr) && lhsS is OrExpr)
                return (rhsS == lhsS);

            return (lhsS | !rhsS) & (rhsS | !lhsS);
        }

        public static BoolExpr operator !=(BoolExpr lhs, BoolExpr rhs) => !(lhs == rhs);

        public static BoolExpr operator >(BoolExpr lhs, BoolExpr rhs) => lhs & !rhs;
        public static BoolExpr operator <(BoolExpr lhs, BoolExpr rhs) => !lhs & rhs;
        public static BoolExpr operator >=(BoolExpr lhs, BoolExpr rhs) => lhs | !rhs;
        public static BoolExpr operator <=(BoolExpr lhs, BoolExpr rhs) => !lhs | rhs;

        public static BoolExpr operator ^(BoolExpr lhsS, BoolExpr rhsS) => !(lhsS == rhsS);

        public static BoolExpr Xor(BoolExpr _lhs, BoolExpr _rhs) => _lhs ^ _rhs;

        public static BoolExpr Max(BoolExpr _lhs, BoolExpr _rhs) => _lhs | _rhs;
        public static BoolExpr Min(BoolExpr _lhs, BoolExpr _rhs) => _lhs & _rhs;

        public static UIntVar operator +(BoolExpr _a, BoolVar _b) => _b + _a;

        public static UIntVar operator +(BoolVar _a, BoolExpr _b)
        {
            if (ReferenceEquals(_b, BoolExpr.False))
                return (UIntVar)_a;

            return new UIntVar(_a.Model, 2, new[] { _a ^ _b, _a & _b });
        }

        public static BoolExpr ITE(BoolExpr _if, BoolExpr _then, BoolExpr _else) => ((!_if | _then) & (_if | _else) & (_then | _else)) | (_then & _else);

        public override int GetHashCode()
        {
            if (ReferenceEquals(this, True))
                return -1830369473;
            else if (ReferenceEquals(this, False))
                return 43589799;
            else
                return 0;
        }

        public override bool Equals(object obj) => ReferenceEquals(this, obj);
    }
}
