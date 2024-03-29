﻿using System;
using System.Buffers;
using System.Collections;
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
	public abstract class BoolExpr //<T> where T : struct, IBinaryInteger<T>
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

		public static LinExpr operator +(BoolExpr _a, BoolExpr _b)
		{
			var le = new LinExpr();
			le.AddTerm(_a);
			le.AddTerm(_b);
			return le;
		}

		public static LinExpr operator +(T _a, BoolExpr _b) => (LinExpr)_b + _a;
		public static LinExpr operator +(BoolExpr _b, T _a) => (LinExpr)_b + _a;

		public static LinExpr operator *(T _a, BoolExpr _b) => (LinExpr)_b * _a;
		public static LinExpr operator *(BoolExpr _b, T _a) => (LinExpr)_b * _a;

		/// <summary>
		/// Returns the Teytsin-encoded equivalent expression
		/// </summary>
		/// <returns></returns>
		public abstract BoolExpr Flatten();

		internal abstract Model? GetModel();

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
			ArgumentNullException.ThrowIfNull(nameof(_v));

			if (ReferenceEquals(_v, Model.False))
				return Model.True;

			if (ReferenceEquals(_v, Model.True))
				return Model.False;

			if (_v is BoolVar bv)
				return bv.Negated;

			if (_v is AndExpr ae)
			{
				var be = new BoolExpr[ae.Elements.Length];
				for (var i = 0; i < be.Length; i++)
					be[i] = !ae.Elements[i];
				return OrExpr.Create(be);
			}

			if (_v is OrExpr oe)
			{
				var be = new BoolExpr[oe.Elements.Length];
				for (var i = 0; i < oe.Elements.Length; i++)
					be[i] = oe.Model.GetVariable(-oe.Elements[i]);
				return AndExpr.Create(be);
			}

			throw new NotImplementedException();
		}

		public abstract int VarCount { get; }

		public static BoolExpr operator |(BoolExpr lhs, BoolExpr rhs) => OrExpr.Create(lhs, rhs);
		public static BoolExpr operator &(BoolExpr lhs, BoolExpr rhs) => AndExpr.Create(lhs, rhs);

		public static BoolExpr operator ==(BoolExpr lhsS, BoolExpr rhsS)
		{
			ArgumentNullException.ThrowIfNull(lhsS, nameof(lhsS));
			ArgumentNullException.ThrowIfNull(rhsS, nameof(rhsS));

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

			lhsS = lhsS.Flatten();
			rhsS = rhsS.Flatten();

			return ((lhsS & rhsS).Flatten() | (!lhsS & !rhsS).Flatten()).Flatten();
		}

		public static BoolExpr operator !=(BoolExpr lhs, BoolExpr rhs) => !(lhs == rhs);
		public static BoolExpr operator ^(BoolExpr lhs, BoolExpr rhs) => !(lhs == rhs);

		public static BoolExpr operator >(BoolExpr lhs, BoolExpr rhs) => lhs & !rhs;
		public static BoolExpr operator <(BoolExpr lhs, BoolExpr rhs) => !lhs & rhs;
		public static BoolExpr operator >=(BoolExpr lhs, BoolExpr rhs) => lhs | !rhs;
		public static BoolExpr operator <=(BoolExpr lhs, BoolExpr rhs) => !lhs | rhs;

		public UIntVar ToUIntVar(Model _m) => new(_m, T.One, new[] { this });
	}
}
