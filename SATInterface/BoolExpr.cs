using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SATInterface
{
    /// <summary>
    /// A BoolExpr is an arbitrary boolean expression in CNF
    /// </summary>
    public abstract class BoolExpr
    {
        internal BoolExpr()
        {
        }

        public static bool operator true(BoolExpr _be) => ReferenceEquals(Model.True, _be);
        public static bool operator false(BoolExpr _be) => ReferenceEquals(Model.False, _be);

        public static implicit operator BoolExpr(bool _v) => _v ? Model.True : Model.False;

        /// <summary>
        /// Returns the value of this expression. Only works in a SAT model.
        /// </summary>
        public virtual bool X
        {
            get
            {
                if (ReferenceEquals(this, Model.True))
                    return true;
                if (ReferenceEquals(this, Model.False))
                    return false;

                throw new InvalidOperationException("This expression could not be pre-solved to true or false.");
            }
        }

        public static LinExpr operator *(int _a, BoolExpr _b) => (LinExpr)_b * _a;
        public static LinExpr operator *(BoolExpr _b, int _a) => (LinExpr)_b * _a;

        /// <summary>
        /// Returns the Teytsin-encoded equivalent expression
        /// </summary>
        /// <returns></returns>
        public virtual BoolExpr Flatten()
        {
            //TODO: track equality for CSE
            if (ReferenceEquals(this, Model.True) || ReferenceEquals(this, Model.False))
                return this;

            if (!(flattened is null))
                return flattened;

            var model = EnumVars().First().Model;
            var res = new BoolVar(model);
            model.AddConstr(res == this);
            return flattened = res;
        }
        private BoolVar? flattened;

        internal virtual IEnumerable<BoolVar> EnumVars()
        {
            yield break;
        }

        public override string ToString()
        {
            if (ReferenceEquals(this, Model.True))
                return "true";
            if (ReferenceEquals(this, Model.False))
                return "false";

            throw new InvalidOperationException("This expression could not be pre-solved to true or false.");
        }

        public static BoolExpr operator !(BoolExpr _v)
        {
            if (_v is null)
                throw new ArgumentNullException();

            if (ReferenceEquals(_v, Model.False))
                return Model.True;

            if (ReferenceEquals(_v, Model.True))
                return Model.False;

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
                return Model.True;
            if (lhsS is null)
                return Model.False;
            if (rhsS is null)
                return Model.False;

            if (ReferenceEquals(lhsS, rhsS))
                return Model.True;

            if (ReferenceEquals(lhsS, Model.True))
                return rhsS;
            if (ReferenceEquals(lhsS, Model.False))
                return !rhsS;

            if (ReferenceEquals(rhsS, Model.True))
                return lhsS;
            if (ReferenceEquals(rhsS, Model.False))
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

        /// <summary>
        /// If-Then-Else to pick one of two values. If _if is TRUE, _then will be picked, _else otherwise.
        /// </summary>
        /// <param name="_if"></param>
        /// <param name="_then"></param>
        /// <param name="_else"></param>
        /// <returns></returns>
        internal static BoolExpr ITE(BoolExpr _if, BoolExpr _then, BoolExpr _else) => ((!_if | _then) & (_if | _else) & (_then | _else)) | (_then & _else);

        public override bool Equals(object obj) => ReferenceEquals(this, obj);
    }
}
