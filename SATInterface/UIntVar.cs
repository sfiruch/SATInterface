using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class UIntVar
    {
        public class UIntVarBits
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
        }

        private Model Model;
        internal BoolExpr[] bit;
        internal int UB;

        /// <summary>
        /// Upper bound of variables with more than 30 bits
        /// </summary>
        public const int Unbounded = -1;

        private const int MaxBitsWithUB = 30;

        /// <summary>
        /// Direct access to the bits making up this number. Index 0 is the LSB.
        /// </summary>
        public UIntVarBits Bits => BitsCache ?? (BitsCache = new UIntVarBits(this));
        private UIntVarBits? BitsCache;

        internal UIntVar(Model _model, int _ub, BoolExpr[] _bits, bool _enforceUB = false)
        {
            Model = _model;
            UB = _ub;
            bit = _bits;

            Debug.Assert(UB == Unbounded || _bits.Length <= MaxBitsWithUB);

            if (_bits.Length <= MaxBitsWithUB)
            {
                if (UB == Unbounded)
                    UB = (1 << _bits.Length) - 1;
                else
                    UB = Math.Min(UB, (1 << _bits.Length) - 1);
            }

            Debug.Assert(UB != Unbounded || _bits.Length > MaxBitsWithUB);


            if (UB != Unbounded && _enforceUB && (UB & (UB + 1)) != 0)
            {
                //work around shortcut evaluation of comparison
                UB = Unbounded;
                Model.AddConstr(this <= _ub);
                UB = _ub;
            }
        }

        /// <summary>
        /// Converts this number to an equivalent LinExpr. Only supported for variables
        /// with upper bound less than 2^30.
        /// </summary>
        /// <returns></returns>
        public LinExpr ToLinExpr()
        {
            if (UB == UIntVar.Unbounded)
                throw new ArgumentException($"Only bounded variables supported");

            var le = new LinExpr();
            for (var i = 0; i < Bits.Length; i++)
                le.AddTerm(bit[i], 1 << i);
            return le;
        }

        internal static int RequiredBitsForUB(long _ub) => Log2(_ub) + 1;

        /// <summary>
        /// Creates a new unsigned integer variable
        /// </summary>
        /// <param name="_model">The model containing this variable</param>
        /// <param name="_ub">Upper bound of this variable or UIntVar.Unbounded when >2^30</param>
        /// <param name="_enforceUB">If TRUE, additional constraints enforcing the upper bound will be added to the model</param>
        internal UIntVar(Model _model, int _ub, bool _enforceUB = true)
        {
            if (_ub < 0 && _ub != Unbounded)
                throw new ArgumentException("Invalid upper bound", nameof(_ub));

            Model = _model;
            UB = _ub;

            if (UB == 0)
                bit = new BoolExpr[0];
            else
            {
                bit = new BoolExpr[RequiredBitsForUB(UB)];
                for (var i = 0; i < bit.Length; i++)
                    bit[i] = new BoolVar(_model);

                if (UB != Unbounded && _enforceUB && (UB & (UB + 1)) != 0)
                {
                    //work around shortcut evaluation of comparison
                    UB = Unbounded;
                    Model.AddConstr(this <= _ub);
                    UB = _ub;
                }
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
            return new UIntVar(_then.Model, (_then.UB == Unbounded || _else.UB == Unbounded) ? Unbounded : Math.Max(_then.UB, _else.UB), bits);
        }

        internal static UIntVar Convert(Model _m, BoolExpr _v) => new UIntVar(_m, 1, new[] { _v });
        public static implicit operator LinExpr(UIntVar _v) => _v.ToLinExpr();

        public override bool Equals(object obj)
        {
            var other = obj as UIntVar;
            if (ReferenceEquals(other, null))
                return false;

            return other.UB == UB && ReferenceEquals(other.Model, Model) && other.bit.SequenceEqual(bit);
        }

        public override int GetHashCode() => bit.Select(be => be.GetHashCode()).Aggregate((a, b) => a ^ b) ^ (1 << 26) ^ UB;

        /// <summary>
        /// Allocates a unsigned integer constant. Most operations with such a constant will be short-
        /// circuited by the framework.
        /// </summary>
        /// <param name="_model">The model containing this variable</param>
        /// <param name="_c">The constant value</param>
        /// <returns></returns>
        internal static UIntVar Const(Model _model, int _c)
        {
            if (_c < 0)
                throw new ArgumentException($"Value may not be negative", nameof(_c));

            var bits = new BoolExpr[RequiredBitsForUB(_c)];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = ((_c >> i) & 1) == 1;
            return new UIntVar(_model, _c, bits);
        }

        public static UIntVar operator >>(UIntVar _a, int _shift)
        {
            if (_a.Bits.Length <= _shift)
                return Const(_a.Model, 0);

            var bits = new BoolExpr[_a.UB == Unbounded ? (_a.Bits.Length - _shift) : RequiredBitsForUB(_a.UB >> _shift)];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = _a.Bits[i + _shift];
            return new UIntVar(_a.Model, _a.UB == Unbounded ? Unbounded : (_a.UB >> _shift), bits);
        }

        public static UIntVar operator &(int _mask, UIntVar _v) => (_v & _mask);
        public static UIntVar operator &(UIntVar _v, int _mask)
        {
            var bits = new BoolExpr[(_v.UB == Unbounded) ? _v.Bits.Length : RequiredBitsForUB(Math.Min(_v.UB, _mask))];
            for (var i = 0; i < bits.Length; i++)
                if (((_mask >> i) & 1) != 0)
                    bits[i] = _v.Bits[i];
                else
                    bits[i] = Model.False;
            return new UIntVar(_v.Model, (_v.UB == Unbounded) ? _mask : Math.Min(_v.UB, _mask), bits);
        }


        public static UIntVar operator +(UIntVar _a, int _add) => (_add == 0) ? _a : (_a + Const(_a.Model, _add));
        public static UIntVar operator +(int _add, UIntVar _a) => (_add == 0) ? _a : (_a + Const(_a.Model, _add));

        public static UIntVar operator -(UIntVar _a, int _add) => (_add == 0) ? _a : (_a - Const(_a.Model, _add));
        public static UIntVar operator -(int _add, UIntVar _a) => Const(_a.Model, _add) - _a;

        public static UIntVar operator -(UIntVar _a, UIntVar _b)
        {
            var res = new UIntVar(_a.Model, (_a.UB == Unbounded) ? Unbounded : _a.UB, false);
            _a.Model.AddConstr((_b + res) == _a);
            return res;
        }

        public static UIntVar operator <<(UIntVar _a, int _shift)
        {
            var bits = new BoolExpr[_a.Bits.Length + _shift];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = i >= _shift ? _a.Bits[i - _shift] : false;
            return new UIntVar(_a.Model, (_a.UB == Unbounded || RequiredBitsForUB(_a.UB) + _shift >= MaxBitsWithUB) ? Unbounded : _a.UB << _shift, bits);
        }

        private static int Log2(long _n)
        {
            var res = 0;
            if ((_n >> 32) != 0)
            {
                res += 32;
                _n >>= 32;
            }
            if ((_n >> 16) != 0)
            {
                res += 16;
                _n >>= 16;
            }
            if ((_n >> 8) != 0)
            {
                res += 8;
                _n >>= 8;
            }
            if ((_n >> 4) != 0)
            {
                res += 4;
                _n >>= 4;
            }
            if ((_n >> 2) != 0)
            {
                res += 2;
                _n >>= 2;
            }
            if ((_n >> 1) != 0)
            {
                res += 1;
                _n >>= 1;
            }
            return res;
        }

        /// <summary>
        /// Returns the value of this variable in a SAT instance. Can throw OverflowException
        /// if the value is >2^30 or InvalidOperationException if the model is not SAT.
        /// </summary>
        public int X
        {
            get
            {
                var res = 0;
                for (var i = 0; i < bit.Length; i++)
                    if (bit[i].X)
                    {
                        if (i > MaxBitsWithUB)
                            throw new OverflowException();
                        res |= (1 << i);
                    }
                return res;
            }
        }

        public static BoolExpr operator ==(int _v, UIntVar _a) => _a == _v;
        public static BoolExpr operator ==(UIntVar _a, int _v)
        {
            if (_v < 0)
                return false;
            if (_v > _a.UB && _a.UB != Unbounded)
                return false;

            var res = new BoolExpr[_a.bit.Length];
            for (var i = 0; i < res.Length; i++)
                res[i] = (((_v >> i) & 1) == 1) ? _a.Bits[i] : !_a.Bits[i];

            return AndExpr.Create(res).Flatten();
        }

        public static BoolExpr operator !=(int _v, UIntVar _a) => _a != _v;
        public static BoolExpr operator !=(UIntVar _a, int _v)
        {
            if (_v < 0)
                return true;
            if (_v > _a.UB && _a.UB != Unbounded)
                return true;

            var res = new BoolExpr[_a.bit.Length];
            for (var i = 0; i < res.Length; i++)
                res[i] = (((_v >> i) & 1) == 1) ? !_a.Bits[i] : _a.Bits[i];

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

        public static BoolExpr operator >(int _v, UIntVar _a) => _a < _v;
        public static BoolExpr operator >(UIntVar _a, int _v)
        {
            if (_v < 0)
                return true;
            if (_v == 0)
                return OrExpr.Create(_a.bit);
            if (_a.UB <= _v && _a.UB != Unbounded)
                return false;

            var nonZeroes = Log2(_v) + 1;
            Debug.Assert(nonZeroes <= _a.bit.Length);

            var resAny = new BoolExpr[_a.bit.Length - nonZeroes];
            for (var i = nonZeroes; i < _a.Bits.Length; i++)
                resAny[i - nonZeroes] = _a.Bits[i];
            var leadingZeroes = (resAny.Length != 0) ? OrExpr.Create(resAny) : false;

            var resOr = new List<BoolExpr>();
            for (var i = 0; i < nonZeroes; i++)
                if (((_v >> i) & 1) == 0)
                {
                    var allesDavorEq = AndExpr.Create(Enumerable.Range(i + 1, nonZeroes - i)
                        .Where(j => ((_v >> j) & 1) == 1)
                        .Select(j => _a.Bits[j]));
                    resOr.Add((_a.Bits[i] & allesDavorEq).Flatten());
                }
            var orExpr = (resOr.Count != 0) ? OrExpr.Create(resOr) : false;

            return (leadingZeroes | orExpr).Flatten();
        }

        public static BoolExpr operator <(int _v, UIntVar _a) => _a > _v;
        public static BoolExpr operator <(UIntVar _a, int _v)
        {
            if (_v <= 0)
                return false;
            if (_v == 1)
                return !OrExpr.Create(_a.bit);
            if (_v > _a.UB && _a.UB != Unbounded)
                return true;

            var nonZeroes = Log2(_v - 1) + 1;
            Debug.Assert(nonZeroes <= _a.bit.Length);

            var resAnd = new BoolExpr[_a.bit.Length - nonZeroes];
            for (var i = nonZeroes; i < _a.Bits.Length; i++)
                resAnd[i - nonZeroes] = !_a.Bits[i];
            var leadingZeroes = (resAnd.Length != 0) ? AndExpr.Create(resAnd).Flatten() : true;

            var resOr = new List<BoolExpr>();
            for (var i = 0; i < nonZeroes; i++)
                if (((_v >> i) & 1) == 1)
                {
                    var allesDavorEq = AndExpr.Create(Enumerable.Range(i + 1, nonZeroes - i - 1)
                        .Where(j => ((_v >> j) & 1) == 0)
                        .Select(j => !_a.Bits[j]));
                    resOr.Add((!_a.Bits[i] & allesDavorEq).Flatten());
                }
            var orExpr = (resOr.Count != 0) ? OrExpr.Create(resOr).Flatten() : true;

            return leadingZeroes & orExpr;
        }


        public static BoolExpr operator >(UIntVar _a, UIntVar _b) => _b < _a;
        public static BoolExpr operator <(UIntVar _a, UIntVar _b)
        {
            var res = new BoolExpr[Math.Max(_a.bit.Length, _b.bit.Length)];
            for (var i = 0; i < _a.bit.Length || i < _b.bit.Length; i++)
            {
                var allesDavorEq = AndExpr.Create(Enumerable.Range(i + 1, res.Length - i - 1).Select(j => _a.Bits[j] == _b.Bits[j])).Flatten();
                res[i] = ((_a.Bits[i] < _b.Bits[i]) & allesDavorEq).Flatten();
            }

            return OrExpr.Create(res).Flatten();
        }



        public static BoolExpr operator >=(UIntVar _a, int _v) => _a > (_v - 1);
        public static BoolExpr operator <=(UIntVar _a, int _v) => _a < (_v + 1);

        public static BoolExpr operator >=(int _v, UIntVar _a) => _a < (_v + 1);
        public static BoolExpr operator <=(int _v, UIntVar _a) => _a > (_v - 1);
        public static BoolExpr operator >=(UIntVar _a, UIntVar _b) => (_a > _b) | (_a == _b);
        public static BoolExpr operator <=(UIntVar _a, UIntVar _b) => (_a < _b) | (_a == _b);

        public static UIntVar operator *(BoolExpr _b, UIntVar _a) => _a * _b;
        public static UIntVar operator *(UIntVar _a, BoolExpr _b)
        {
            if (ReferenceEquals(_b, Model.False))
                return Const(_a.Model, 0);
            if (ReferenceEquals(_b, Model.True))
                return _a;

            var bits = new BoolExpr[_a.Bits.Length];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = (_a.Bits[i] & _b).Flatten();
            return new UIntVar(_a.Model, _a.UB, bits);
        }

        public static UIntVar operator *(int _b, UIntVar _a) => _a * _b;
        public static UIntVar operator *(UIntVar _a, int _b)
        {
            if (_b < 0)
                throw new ArgumentException("Only multiplication by positive numbers supported.");
            if (_b == 0)
                return Const(_a.Model, 0);
            if (_b == 1)
                return _a;

            var sum = new List<UIntVar>();
            for (var b = 0; (_b >> b) != 0; b++)
                if ((_b & (1 << b)) != 0)
                    sum.Add(_a << b);

            return _a.Model.Sum(sum);
        }

        public static UIntVar operator *(UIntVar _a, UIntVar _b)
        {
            if (_a.UB == 0 || _b.UB == 0)
                return Const(_a.Model, 0);

            if (_a.Bits.Length > _b.Bits.Length)
                return _b * _a;

            var sum = new List<UIntVar>();
            for (var b = 0; b < _a.bit.Length; b++)
                sum.Add((_b << b) * _a.Bits[b]);

            var res = _a.Model.Sum(sum);
            res.UB = (_a.UB == Unbounded || _b.UB == Unbounded) ? Unbounded : (_a.UB * _b.UB);
            return res;
        }

        public static UIntVar operator +(BoolExpr _b, UIntVar _a) => _a + _b;
        public static UIntVar operator +(UIntVar _a, BoolExpr _b)
        {
            if (ReferenceEquals(_b, Model.False))
                return _a;

            var bits = new BoolExpr[(_a.UB == Unbounded) ? _a.Bits.Length + 1 : RequiredBitsForUB(_a.UB + 1)];

            var carry = _b;
            for (var i = 0; i < bits.Length; i++)
            {
                bits[i] = (_a.Bits[i] ^ carry).Flatten();

                if (i < bits.Length - 1)
                {
                    carry = (_a.Bits[i] & carry).Flatten();

                    //unitprop
                    _a.Model.AddConstr(OrExpr.Create(bits[i], carry, !_a.Bits[i]));
                }
            }

            return new UIntVar(_a.Model, _a.UB == -1 ? -1 : _a.UB + 1, bits);
        }


        public static UIntVar operator ^(UIntVar _a, UIntVar _b)
        {
            var bits = new BoolExpr[Math.Max(_a.Bits.Length, _b.Bits.Length)];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = (_a.Bits[i] ^ _b.Bits[i]).Flatten();
            return new UIntVar(_a.Model, Unbounded, bits);
        }

        public static UIntVar operator |(UIntVar _a, UIntVar _b)
        {
            var bits = new BoolExpr[(_a.UB == Unbounded || _b.UB == Unbounded) ? Math.Max(_a.Bits.Length, _b.Bits.Length) : RequiredBitsForUB(_a.UB | _b.UB)];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = (_a.Bits[i] | _b.Bits[i]).Flatten();
            return new UIntVar(_a.Model, (_a.UB == Unbounded || _b.UB == Unbounded) ? Unbounded : (_a.UB | _b.UB), bits);
        }

        public static UIntVar operator &(UIntVar _a, UIntVar _b)
        {
            int ub;
            if (_a.UB == Unbounded && _b.UB == Unbounded)
                ub = Unbounded;
            else if (_a.UB == Unbounded)
                ub = _b.UB;
            else if (_b.UB == Unbounded)
                ub = _a.UB;
            else
                ub = Math.Min(_a.UB, _b.UB);

            var bits = new BoolExpr[(_a.UB == Unbounded || _b.UB == Unbounded) ? Math.Min(_a.Bits.Length, _b.Bits.Length) : RequiredBitsForUB(ub)];
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

            if (!(flattened is null))
                return flattened;

            var bits = new BoolExpr[RequiredBitsForUB(UB)];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = bit[i].Flatten();
            return flattened = new UIntVar(Model, UB, bits);
        }
        private UIntVar? flattened;

        public static UIntVar operator +(UIntVar _a, UIntVar _b)
        {
            if (_a.UB == 0)
                return _b;
            if (_b.UB == 0)
                return _a;

            var bits = new BoolExpr[(_a.UB == Unbounded || _b.UB == Unbounded) ? (Math.Max(_a.Bits.Length, _b.Bits.Length) + 1) : RequiredBitsForUB(_a.UB + _b.UB)];

            var carry = Model.False;
            for (var i = 0; i < bits.Length; i++)
            {
                bits[i] = (carry ^ _a.Bits[i] ^ _b.Bits[i]).Flatten();

                if (i < bits.Length - 1)
                {
                    var aAndB = (_a.Bits[i] & _b.Bits[i]).Flatten();
                    var aAndCarry = (_a.Bits[i] & carry).Flatten();
                    var bAndCarry = (_b.Bits[i] & carry).Flatten();

                    carry = OrExpr.Create(aAndB, aAndCarry, bAndCarry).Flatten();

                    //unitprop
                    _a.Model.AddConstr(OrExpr.Create(bits[i], carry, (!_a.Bits[i] & !_b.Bits[i])));
                }
            }

            return new UIntVar(_a.Model, (_a.UB == Unbounded || _b.UB == Unbounded || RequiredBitsForUB(_a.UB + (long)_b.UB) >= MaxBitsWithUB) ? Unbounded : (_a.UB + _b.UB), bits);
        }
    }
}
