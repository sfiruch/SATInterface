using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SATInterface
{
    /// <summary>
    /// A BoolExpr is an arbitrary boolean expression in CNF
    /// </summary>
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
    public abstract class BoolExpr
#pragma warning restore CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
#pragma warning restore CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        internal BoolExpr()
        {
        }

        //public static bool operator true(BoolExpr _be) => ReferenceEquals(Model.True, _be);
        //public static bool operator false(BoolExpr _be) => ReferenceEquals(Model.False, _be);

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

                throw new InvalidOperationException();
            }
        }

        public static LinExpr operator *(int _a, BoolExpr _b) => (LinExpr)_b * _a;
        public static LinExpr operator *(BoolExpr _b, int _a) => (LinExpr)_b * _a;

        /// <summary>
        /// Returns the Teytsin-encoded equivalent expression
        /// </summary>
        /// <returns></returns>
        public abstract BoolExpr Flatten();

        internal virtual IEnumerable<BoolVar> EnumVars()
        {
            yield break;
        }

        internal abstract Model GetModel();

        public override string ToString()
        {
            if (ReferenceEquals(this, Model.True))
                return "true";
            if (ReferenceEquals(this, Model.False))
                return "false";

            throw new InvalidOperationException();
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

        public abstract int VarCount { get; }

        public static BoolExpr operator |(BoolExpr lhs, BoolExpr rhs) => OrExpr.Create(lhs, rhs);
        public static BoolExpr operator &(BoolExpr lhs, BoolExpr rhs) => AndExpr.Create(lhs, rhs);

        public static BoolExpr operator ==(BoolExpr lhsS, BoolExpr rhsS)
        {
            if (lhsS is null || rhsS is null)
                throw new NullReferenceException();

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

            //if (rhsS is AndExpr andExpr)
            //{
            //    var other = lhsS.Flatten();
            //    var ands = new List<BoolExpr>(andExpr.Elements.Length + 1);
            //    ands.Add(OrExpr.Create(andExpr.Elements.Select(e => !e).Append(other).ToArray()).Flatten());
            //    foreach (var e in andExpr.Elements)
            //        ands.Add(e | !other);
            //    return AndExpr.Create(ands.ToArray());
            //}
            //if (rhsS is OrExpr orExpr)
            //{
            //    var other = lhsS.Flatten();
            //    var ands = new List<BoolExpr>(orExpr.Elements.Length + 1);
            //    ands.Add(OrExpr.Create(orExpr.Elements.Append(!other).ToArray()).Flatten());
            //    foreach (var e in orExpr.Elements)
            //        ands.Add(!e | other);
            //    return AndExpr.Create(ands.ToArray());
            //}
            //if (!(rhsS is AndExpr) && lhsS is AndExpr)
            //    return (rhsS == lhsS);
            //if (!(rhsS is OrExpr) && lhsS is OrExpr)
            //    return (rhsS == lhsS);

            lhsS = lhsS.Flatten();
            rhsS = rhsS.Flatten();

            return lhsS.GetModel().ITE(lhsS, rhsS, !rhsS);

            //var m = lhsS.GetModel();
            //var r = m.AddVar();
            //m.AddConstr(OrExpr.Create(lhsS, rhsS, r));
            //m.AddConstr(OrExpr.Create(lhsS, !rhsS, !r));
            //m.AddConstr(OrExpr.Create(!lhsS, rhsS, !r));
            //m.AddConstr(OrExpr.Create(!lhsS, !rhsS, r));
            //return r;
        }

        public static BoolExpr operator !=(BoolExpr lhs, BoolExpr rhs) => !(lhs == rhs);
        public static BoolExpr operator ^(BoolExpr lhs, BoolExpr rhs) => !(lhs == rhs);

        public static BoolExpr operator >(BoolExpr lhs, BoolExpr rhs) => lhs & !rhs;
        public static BoolExpr operator <(BoolExpr lhs, BoolExpr rhs) => !lhs & rhs;
        public static BoolExpr operator >=(BoolExpr lhs, BoolExpr rhs) => lhs | !rhs;
        public static BoolExpr operator <=(BoolExpr lhs, BoolExpr rhs) => !lhs | rhs;

        public UIntVar ToUIntVar(Model _m) => new UIntVar(_m, 1, new[] { this });
    }
}
