using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SATInterface
{
	public class UIntVar //<T> where T : struct, IBinaryInteger<T>
	{
		public class UIntVarBits : IEnumerable<BoolExpr>
		{
			private readonly UIntVar Parent;

			internal UIntVarBits(UIntVar _parent)
			{
				Parent = _parent;
			}

			/// <summary>
			/// The number of bits making up this number. Bits at higher indices are always false.
			/// </summary>
			public int Length => Parent.bit.Length;

			public BoolExpr this[int _bitIndex]
			{
				get
				{
					if (_bitIndex >= Parent.bit.Length)
						return Model.False;

					return Parent.bit[_bitIndex];
				}
			}

			public IEnumerator<BoolExpr> GetEnumerator() => Parent.bit.AsEnumerable().GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => Parent.bit.GetEnumerator();
		}

		internal Model Model;
		internal BoolExpr[] bit;
		internal T UB;

		/// <summary>
		/// Direct access to the bits making up this number. Index 0 is the LSB.
		/// </summary>
		public UIntVarBits Bits => (UIntVar.UIntVarBits)(BitsCache ??= new UIntVarBits(this));
		private UIntVarBits? BitsCache;

		internal UIntVar(Model _model, T _ub, BoolExpr[] _bits, bool _enforceUB = false)
		{
			if (T.IsNegative(_ub))
				throw new ArgumentException("Invalid upper bound", nameof(_ub));

			Model = _model;
			bit = _bits;

			UB = T.Min(_ub, (T.One << _bits.Length) - T.One);
			if (_enforceUB && !T.IsPow2(UB))
			{
				UB++; //work around shortcut evaluation of comparison
				Model.AddConstr(this <= UB);
				UB--;
			}
		}

		/// <summary>
		/// Converts this number to an equivalent LinExpr.
		/// </summary>
		/// <returns></returns>
		public LinExpr ToLinExpr() => new(this);

		/// <summary>
		/// Creates a new unsigned integer variable
		/// </summary>
		/// <param name="_model">The model containing this variable</param>
		/// <param name="_ub">Upper bound of this variable or UIntVar.Unbounded when >2^30</param>
		/// <param name="_enforceUB">If TRUE, additional constraints enforcing the upper bound will be added to the model</param>
		internal UIntVar(Model _model, T _ub, bool _enforceUB = true)
		{
			try
			{
				if (_ub > T.One)
					_model.StartStatistics("UInt", (int)_ub.GetBitLength());

				if (T.IsNegative(_ub))
					throw new ArgumentException("Invalid upper bound", nameof(_ub));

				Model = _model;
				UB = _ub;

				if (UB == T.Zero)
					bit = Array.Empty<BoolExpr>();
				else
				{
					bit = new BoolExpr[UB.GetBitLength()];
					for (var i = 0; i < bit.Length; i++)
						bit[i] = _model.AddVar();

					if (_enforceUB && !T.IsPow2(UB + 1))
					{
						//do a UB dance to work around shortcut evaluation of comparisons
						UB++;
						Model.AddConstr(this <= (UB - 1));
						UB--;
					}
				}
			}
			finally
			{
				if (_ub > T.One)
					_model.StopStatistics("UInt");
			}
		}

		/// <summary>
		/// If-Then-Else to pick one of two values. If _if is TRUE, _then will be picked, _else otherwise.
		/// </summary>
		/// <param name="_if"></param>
		/// <param name="_then"></param>
		/// <param name="_else"></param>
		/// <returns></returns>
		internal static UIntVar ITE(BoolExpr _if, UIntVar _then, UIntVar _else)
		{
			var bits = new BoolExpr[Math.Max(_then.Bits.Length, _else.Bits.Length)];
			for (var i = 0; i < bits.Length; i++)
				bits[i] = _then.Model.ITE(_if, _then.Bits[i], _else.Bits[i]).Flatten();
			return new UIntVar(_then.Model, T.Max(_then.UB, _else.UB), bits);
		}

		internal static UIntVar Convert(Model _m, BoolExpr _v) => new(_m, T.One, new[] { _v });
		public static implicit operator LinExpr(UIntVar _v) => _v.ToLinExpr();

		/// <summary>
		/// Returns a value indicating whether this instance is equal to a specified UIntVar.
		/// </summary>
		public override bool Equals(object? obj)
		{
			if (obj is not UIntVar other)
				return false;

			return other.UB == UB && ReferenceEquals(other.Model, Model) && other.bit.SequenceEqual(bit);
		}

		/// <summary>
		/// Returns the hash code for this instance.
		/// </summary>
		public override int GetHashCode()
		{
			var hc = new HashCode();
			hc.Add(UB);
			foreach (var be in bit)
				hc.Add(be.GetHashCode());
			return hc.ToHashCode();
		}

		/// <summary>
		/// Returns a new, constant UIntVar which is equal to the provided value.
		/// </summary>
		internal static UIntVar Const(Model _model, T _c)
		{
			if (T.IsNegative(_c))
				throw new ArgumentException($"Value may not be negative", nameof(_c));

			var bits = new BoolExpr[_c.GetBitLength()];
			for (var i = 0; i < bits.Length; i++)
				bits[i] = !(_c >> i).IsEven;
			return new UIntVar(_model, _c, bits);
		}

		/// <summary>
		/// Returns a new UIntVar representing the first parameter shifted left by the given number of bits.
		/// </summary>
		public static UIntVar operator >>(UIntVar _a, int _shift)
		{
			if (_a.Bits.Length <= _shift)
				return _a.Model.AddUIntConst(T.Zero);

			var bits = new BoolExpr[(_a.UB >> _shift).GetBitLength()];
			for (var i = 0; i < bits.Length; i++)
				bits[i] = _a.Bits[i + _shift];
			return new UIntVar(_a.Model, _a.UB >> _shift, bits);
		}

		/// <summary>
		/// Returns a new UIntVar representing the binary AND operation of the first and second parameter.
		/// </summary>
		public static UIntVar operator &(T _mask, UIntVar _v) => (_v & _mask);

		/// <summary>
		/// Returns a new UIntVar representing the binary AND operation of the first and second parameter.
		/// </summary>
		public static UIntVar operator &(UIntVar _v, T _mask)
		{
			var bits = new BoolExpr[T.Min(_v.UB, _mask).GetBitLength()];
			for (var i = 0; i < bits.Length; i++)
				if (!(_mask >> i).IsEven)
					bits[i] = _v.Bits[i];
				else
					bits[i] = Model.False;
			return new UIntVar(_v.Model, T.Min(_v.UB, _mask), bits);
		}

		/// <summary>
		/// Returns a LinExpr representing the sum of the first and second parameter.
		/// </summary>
		public static LinExpr operator +(UIntVar _a, T _add) => _a.ToLinExpr() + _add;

		/// <summary>
		/// Returns a LinExpr representing the sum of the first and second parameter.
		/// </summary>
		public static LinExpr operator +(T _add, UIntVar _a) => _a.ToLinExpr() + _add;

		/// <summary>
		/// Returns a LinExpr representing the difference of the first and second parameter.
		/// </summary>
		public static LinExpr operator -(UIntVar _a, T _add) => _a.ToLinExpr() - _add;

		/// <summary>
		/// Returns a LinExpr representing the difference of the first and second parameter.
		/// </summary>
		public static LinExpr operator -(T _add, UIntVar _a) => _add - _a.ToLinExpr();

		/// <summary>
		/// Returns a LinExpr representing the difference of the first and second parameter.
		/// </summary>
		public static LinExpr operator -(UIntVar _a, UIntVar _b) => _a.ToLinExpr() - _b.ToLinExpr();

		/// <summary>
		/// Returns a new UIntVar representing the difference of the first and second parameter.
		/// Please note that this also implies that the first number must be greater than or equal
		/// to the second number.
		/// </summary>
		public static UIntVar SubtractUInt(UIntVar _a, UIntVar _b)
		{
			var res = new UIntVar(_a.Model, _a.UB, false);
			_a.Model.AddConstr((_b + res) == _a);
			return res;
		}

		public static UIntVar operator <<(UIntVar _a, int _shift)
		{
			var bits = new BoolExpr[_a.Bits.Length + _shift];
			for (var i = 0; i < bits.Length; i++)
				bits[i] = i >= _shift ? _a.Bits[i - _shift] : false;
			return new UIntVar(_a.Model, _a.UB << _shift, bits);
		}

		/// <summary>
		/// Returns the value of this variable in a SAT instance. Can throw OverflowException
		/// if the value is >2^30 or InvalidOperationException if the model is not SAT.
		/// </summary>
		public T X
		{
			get
			{
				var res = T.Zero;
				for (var i = 0; i < bit.Length; i++)
					if (bit[i].X)
						res |= (T.One << i);
				return res;
			}
		}

		/// <summary>
		/// Returns `true` iff the first parameter is equal to the second parameter.
		/// </summary>
		public static BoolExpr operator ==(T _v, UIntVar _a) => _a == _v;

		/// <summary>
		/// Returns `true` iff the first parameter is equal to the second parameter.
		/// </summary>
		public static BoolExpr operator ==(UIntVar _a, T _v)
		{
			if (_v < T.Zero)
				return Model.False;
			if (_v > _a.UB)
				return Model.False;

			var res = new BoolExpr[_a.bit.Length];
			for (var i = 0; i < res.Length; i++)
				res[i] = !(_v >> i).IsEven ? _a.Bits[i] : !_a.Bits[i];

			return AndExpr.Create(res).Flatten();
		}

		/// <summary>
		/// Returns `true` iff the first parameter is not equal to the second parameter.
		/// </summary>
		public static BoolExpr operator !=(T _v, UIntVar _a) => _a != _v;

		/// <summary>
		/// Returns `true` iff the first parameter is not equal to the second parameter.
		/// </summary>
		public static BoolExpr operator !=(UIntVar _a, T _v)
		{
			if (_v < T.Zero)
				return Model.True;
			if (_v > _a.UB)
				return Model.True;

			var res = new BoolExpr[_a.bit.Length];
			for (var i = 0; i < res.Length; i++)
				res[i] = !(_v >> i).IsEven ? !_a.Bits[i] : _a.Bits[i];

			return OrExpr.Create(res).Flatten();
		}

		public static BoolExpr operator ==(UIntVar _a, UIntVar _b)
		{
			var res = new BoolExpr[Math.Max(_a.bit.Length, _b.bit.Length)];
			for (var i = 0; i < res.Length; i++)
				res[i] = (_a.Bits[i] == _b.Bits[i]).Flatten();

			return AndExpr.Create(res).Flatten();
		}

		public static BoolExpr operator !=(UIntVar _a, UIntVar _b)
		{
			var res = new BoolExpr[Math.Max(_a.bit.Length, _b.bit.Length)];
			for (var i = 0; i < res.Length; i++)
				res[i] = (_a.Bits[i] != _b.Bits[i]).Flatten();

			return OrExpr.Create(res).Flatten();
		}

		/// <summary>
		/// Returns `true` iff the first parameter is greater than the second parameter.
		/// </summary>
		public static BoolExpr operator >(T _v, UIntVar _a) => _a < _v;

		/// <summary>
		/// Returns `true` iff the first parameter is greater than the second parameter.
		/// </summary>
		public static BoolExpr operator >(UIntVar _a, T _v)
		{
			if (_v < T.Zero)
				return Model.True;
			if (_v == T.Zero)
				return OrExpr.Create(_a.bit);
			if (_a.UB <= _v)
				return Model.False;
			if (_a.Bits.Length < _v.GetBitLength())
				return Model.False;

			return _a > _a.Model.AddUIntConst(_v);
		}

		/// <summary>
		/// Returns `true` iff the first parameter is less than the second parameter.
		/// </summary>
		public static BoolExpr operator <(T _v, UIntVar _a) => _a > _v;

		/// <summary>
		/// Returns `true` iff the first parameter is less than the second parameter.
		/// </summary>
		public static BoolExpr operator <(UIntVar _a, T _v)
		{
			if (_v <= T.Zero)
				return Model.False;
			if (_v == T.One)
				return !OrExpr.Create(_a.bit);
			if (_v > _a.UB)
				return Model.True;

			return _a < _a.Model.AddUIntConst(_v);
		}


		/// <summary>
		/// Returns `true` iff the first parameter is greater than the second parameter.
		/// </summary>
		public static BoolExpr operator >(UIntVar _a, UIntVar _b) => _b < _a;

		/// <summary>
		/// Returns `true` iff the first parameter is less than the second parameter.
		/// </summary>
		public static BoolExpr operator <(UIntVar _a, UIntVar _b)
		{
			try
			{
				_a.Model.StartStatistics("UInt <", Math.Max(_a.bit.Length, _b.bit.Length));

				var res = Model.False;
				for (var i = 0; i < Math.Max(_a.bit.Length, _b.bit.Length); i++)
					res = _a.Model.ITE(_b.Bits[i], !_a.Bits[i] | res, !_a.Bits[i] & res);

				return res;
			}
			finally
			{
				_a.Model.StopStatistics("UInt <");
			}
		}

		/// <summary>
		/// Returns `true` iff the first parameter is greater than or equal to the second parameter.
		/// </summary>
		public static BoolExpr operator >=(UIntVar _a, T _v) => _a > (_v - T.One);

		/// <summary>
		/// Returns `true` iff the first parameter is less than or equal to the second parameter.
		/// </summary>
		public static BoolExpr operator <=(UIntVar _a, T _v) => _a < (_v + T.One);

		/// <summary>
		/// Returns `true` iff the first parameter is greater than or equal to the second parameter.
		/// </summary>
		public static BoolExpr operator >=(T _v, UIntVar _a) => _a < (_v + T.One);

		/// <summary>
		/// Returns `true` iff the first parameter is less than or equal to the second parameter.
		/// </summary>
		public static BoolExpr operator <=(T _v, UIntVar _a) => _a > (_v - T.One);

		/// <summary>
		/// Returns `true` iff the first parameter is greater than or equal to the second parameter.
		/// </summary>
		public static BoolExpr operator >=(UIntVar _a, UIntVar _b) => !(_a < _b);

		/// <summary>
		/// Returns `true` iff the first parameter is less than or equal to the second parameter.
		/// </summary>
		public static BoolExpr operator <=(UIntVar _a, UIntVar _b) => !(_b < _a);

		/// <summary>
		/// Returns a new UIntVar which is equal to the second parameter, if the first
		/// parameter is `true`, otherwise zero.
		/// </summary>
		public static UIntVar operator *(BoolExpr _b, UIntVar _a) => _a * _b;

		/// <summary>
		/// Returns a new UIntVar which is equal to the first parameter, if the second
		/// parameter is `true`, otherwise zero.
		/// </summary>
		public static UIntVar operator *(UIntVar _a, BoolExpr _b)
		{
			if (ReferenceEquals(_b, Model.False))
				return _a.Model.AddUIntConst(T.Zero);
			if (ReferenceEquals(_b, Model.True))
				return _a;

			var bits = new BoolExpr[_a.Bits.Length];
			for (var i = 0; i < bits.Length; i++)
				bits[i] = (_a.Bits[i] & _b).Flatten();
			return new UIntVar(_a.Model, _a.UB, bits);
		}

		/// <summary>
		/// Multiplication by a positive constant
		/// </summary>
		/// <exception cref="ArgumentException">Thrown in case of negative constants</exception>
		public static UIntVar operator *(T _b, UIntVar _a) => _a * _b;

		/// <summary>
		/// Multiplication by a positive constant
		/// </summary>
		/// <exception cref="ArgumentException">Thrown in case of negative constants</exception>
		public static UIntVar operator *(UIntVar _a, T _b)
		{
			if (_b < T.Zero)
				throw new ArgumentException("Only multiplication by positive numbers supported.");
			if (_b == T.Zero)
				return _a.Model.AddUIntConst(T.Zero);
			if (_b == T.One)
				return _a;
			if (T.IsPow2(_b))
				return _a << int.CreateChecked(T.Log2(_b));

			var sum = new List<UIntVar>();
			for (var b = 0; (_b >> b) != T.Zero; b++)
				if ((_b & (T.One << b)) != T.Zero)
					sum.Add(_a << b);

			return _a.Model.Sum(CollectionsMarshal.AsSpan(sum));
		}

		/// <summary>
		/// Returns a new UIntVar which is equal to the multiplication of both values. 
		/// </summary>
		public static UIntVar operator *(UIntVar _a, UIntVar _b)
		{
			if (_a.UB == T.Zero || _b.UB == T.Zero)
				return _a.Model.AddUIntConst(T.Zero);

			if (_a.Bits.Length > _b.Bits.Length)
				return _b * _a;

			var sum = new UIntVar[_a.bit.Length];
			for (var b = 0; b < _a.bit.Length; b++)
				sum[b] = (_b << b) * _a.Bits[b];

			var res = _a.Model.Sum(sum);
			res.UB = checked(_a.UB * _b.UB);
			return res;
		}

		/// <summary>
		/// Returns the sum of _a and _b, where `false` corresponts to 0, and `true`
		/// corresponds to 1.
		/// </summary>
		public static UIntVar operator +(BoolExpr _b, UIntVar _a) => _a + _b;

		/// <summary>
		/// Returns the sum of _a and _b, where `false` corresponts to 0, and `true`
		/// corresponds to 1.
		/// </summary>
		public static UIntVar operator +(UIntVar _a, BoolExpr _b)
		{
			if (ReferenceEquals(_b, Model.False))
				return _a;

			try
			{
				_a.Model.StartStatistics("UInt + Bit", _a.bit.Length);

				var bits = new BoolExpr[(_a.UB + T.One).GetBitLength()];
				var m = _a.Model;

				var carry = _b;
				for (var i = 0; i < bits.Length; i++)
				{
					bits[i] = (_a.Bits[i] ^ carry).Flatten();

					if (i < bits.Length - 1)
					{
						var oldCarry = carry;
						carry = (_a.Bits[i] & carry).Flatten();

						//unitprop
						if (m.Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.PartialArith))
						{
							m.AddConstr(OrExpr.Create(carry, bits[i], !(i == 0 ? _b : Model.False)));
							m.AddConstr(OrExpr.Create(!carry, !bits[i], i == 0 ? _b : Model.False));
						}
						if (m.Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.FullArith))
						{
							m.AddConstr(OrExpr.Create(carry, bits[i], !_a.Bits[i]));
							m.AddConstr(OrExpr.Create(carry, bits[i], !oldCarry));
							m.AddConstr(OrExpr.Create(!carry, !bits[i], _a.Bits[i]));
							m.AddConstr(OrExpr.Create(!carry, !bits[i], oldCarry));
						}
					}
				}

				return new UIntVar(_a.Model, _a.UB + T.One, bits);
			}
			finally
			{
				_a.Model.StopStatistics("UInt + Bit");
			}
		}

		/// <summary>
		/// Binary XOR
		/// </summary>
		public static UIntVar operator ^(UIntVar _a, UIntVar _b)
		{
			var bits = new BoolExpr[Math.Max(_a.Bits.Length, _b.Bits.Length)];
			for (var i = 0; i < bits.Length; i++)
				bits[i] = (_a.Bits[i] ^ _b.Bits[i]).Flatten();
			return new UIntVar(_a.Model, (T.One << bits.Length) - T.One, bits);
		}

		/// <summary>
		/// Binary OR
		/// </summary>
		public static UIntVar operator |(UIntVar _a, UIntVar _b)
		{
			var bits = new BoolExpr[(_a.UB | _b.UB).GetBitLength()];
			for (var i = 0; i < bits.Length; i++)
				bits[i] = (_a.Bits[i] | _b.Bits[i]).Flatten();
			return new UIntVar(_a.Model, _a.UB | _b.UB, bits);
		}

		/// <summary>
		/// Binary AND operation
		/// </summary>
		public static UIntVar operator &(UIntVar _a, UIntVar _b)
		{
			var ub = T.Min(_a.UB, _b.UB);
			var bits = new BoolExpr[ub.GetBitLength()];
			for (var i = 0; i < bits.Length; i++)
				bits[i] = (_a.Bits[i] & _b.Bits[i]).Flatten();
			return new UIntVar(_a.Model, ub, bits);
		}

		/// <summary>
		/// Returns an equivalent Tseytin-encoded variable
		/// </summary>
		/// <returns></returns>
		public UIntVar Flatten()
		{
			if (bit.OfType<BoolVar>().Count() == bit.Length)
				return this;

			if (flattened is not null)
				return flattened;

			var bits = new BoolExpr[UB.GetBitLength()];
			for (var i = 0; i < bits.Length; i++)
				bits[i] = bit[i].Flatten();
			return flattened = new UIntVar(Model, UB, bits);
		}
		private UIntVar? flattened;

		/// <summary>
		/// Addition of two numbers
		/// </summary>
		public static UIntVar operator +(UIntVar _a, UIntVar _b)
		{
			if (_a.UB == T.Zero)
				return _b;
			if (_b.UB == T.Zero)
				return _a;

			try
			{
				_a.Model.StartStatistics("UInt + UInt", Math.Max(_a.bit.Length, _b.bit.Length));

				var m = _a.Model;

				if (m.UIntSumCache.TryGetValue((_a, _b), out var res))
					return res;
				if (m.UIntSumCache.TryGetValue((_b, _a), out res))
					return res;

				var bits = new BoolExpr[checked(_a.UB + _b.UB).GetBitLength()];

				var carry = Model.False;
				for (var i = 0; i < bits.Length; i++)
				{
					bits[i] = (carry ^ _a.Bits[i] ^ _b.Bits[i]).Flatten();
					if (i < bits.Length - 1)
					{
						var oldCarry = carry;
						carry = OrExpr.Create(carry & _a.Bits[i], carry & _b.Bits[i], _a.Bits[i] & _b.Bits[i]).Flatten();

						//unitprop
						if (m.Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.FullArith))
						{
							m.AddConstr(OrExpr.Create(!carry, !bits[i], _a.Bits[i]));
							m.AddConstr(OrExpr.Create(!carry, !bits[i], _b.Bits[i]));
							m.AddConstr(OrExpr.Create(!carry, !bits[i], oldCarry));
							m.AddConstr(OrExpr.Create(carry, bits[i], !_b.Bits[i]));
							m.AddConstr(OrExpr.Create(carry, bits[i], !_a.Bits[i]));
							m.AddConstr(OrExpr.Create(carry, bits[i], !oldCarry));
						}
						else if (m.Configuration.AddArcConstistencyClauses.HasFlag(ArcConstistencyClauses.PartialArith))
						{
							if (ReferenceEquals(_a.Bits[i], Model.False) |
								ReferenceEquals(_b.Bits[i], Model.False) |
								ReferenceEquals(oldCarry, Model.False))
								m.AddConstr(OrExpr.Create(!carry, !bits[i]));

							if (ReferenceEquals(_a.Bits[i], Model.True) |
								ReferenceEquals(_b.Bits[i], Model.True) |
								ReferenceEquals(oldCarry, Model.True))
								m.AddConstr(OrExpr.Create(carry, bits[i]));
						}
					}
				}

				return m.UIntSumCache[(_a, _b)] = new UIntVar(m, checked(_a.UB + _b.UB), bits);
			}
			finally
			{
				_a.Model.StopStatistics("UInt + UInt");
			}
		}
	}
}
