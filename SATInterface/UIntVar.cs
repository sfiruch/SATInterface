using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class UIntVar
    {
        private Model Model;
        internal BoolExpr[] bit;
        internal int UB;

        public const int Unbounded = -1;
        private const int MaxBitsWithUB = 30;

        public int Bits => bit.Length;

        public UIntVar(Model _model, int _ub, BoolExpr[] _bits, bool _enforceUB = false)
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

        public LinExpr ToLinExpr()
        {
            if (UB == UIntVar.Unbounded)
                throw new ArgumentException($"Only bounded variables supported");

            var le = new LinExpr();
            for (var i = 0; i < Bits; i++)
                le.AddTerm(bit[i], 1 << i);
            return le;
        }

        internal static int RequiredBitsForUB(long _ub) => Log2(_ub) + 1;

        public UIntVar(Model _model, int _ub, bool _enforceUB = true)
        {
            if (_ub < 0 && _ub != Unbounded)
                throw new ArgumentException(nameof(_ub));

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

        public static UIntVar ITE(BoolExpr _if, UIntVar _then, UIntVar _else)
        {
            var bits = new BoolExpr[Math.Max(_then.Bits, _else.Bits)];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = BoolExpr.ITE(_if, _then[i], _else[i]).Flatten();
            return new UIntVar(_then.Model, (_then.UB == Unbounded || _else.UB == Unbounded) ? Unbounded : Math.Max(_then.UB, _else.UB), bits);
        }

        public static UIntVar Convert(Model _m, BoolExpr _v) => new UIntVar(_m, 1, new[] { _v });
        public static explicit operator UIntVar(BoolVar _v) => new UIntVar(_v.Model, 1, new[] { _v });
        public static implicit operator LinExpr(UIntVar _v) => _v.ToLinExpr();

        public override bool Equals(object obj)
        {
            var other = obj as UIntVar;
            if (ReferenceEquals(other, null))
                return false;

            return other.UB == UB && ReferenceEquals(other.Model, Model) && other.bit.SequenceEqual(bit);
        }
        public override int GetHashCode() => bit.Select(be => be.GetHashCode()).Aggregate((a, b) => a ^ b) ^ (1 << 26) ^ UB;

        public static UIntVar Const(Model _model, int _c)
        {
            if (_c < 0)
                throw new ArgumentException($"{nameof(_c)} may not be negative");

            var bits = new BoolExpr[RequiredBitsForUB(_c)];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = ((_c >> i) & 1) == 1;
            return new UIntVar(_model, _c, bits);
        }

        private BoolExpr this[int _bitIndex]
        {
            get
            {
                if (_bitIndex >= bit.Length)
                    return BoolExpr.False;

                return bit[_bitIndex];
            }
        }

        public static UIntVar operator >>(UIntVar _a, int _shift)
        {
            if (_a.Bits <= _shift)
                return Const(_a.Model, 0);

            var bits = new BoolExpr[_a.UB == Unbounded ? (_a.Bits - _shift) : RequiredBitsForUB(_a.UB >> _shift)];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = _a[i + _shift];
            return new UIntVar(_a.Model, _a.UB == Unbounded ? Unbounded : (_a.UB >> _shift), bits);
        }

        public static UIntVar operator &(int _mask, UIntVar _v) => (_v & _mask);
        public static UIntVar operator &(UIntVar _v, int _mask)
        {
            var bits = new BoolExpr[(_v.UB == Unbounded) ? _v.Bits : RequiredBitsForUB(Math.Min(_v.UB, _mask))];
            for (var i = 0; i < bits.Length; i++)
                if (((_mask >> i) & 1) != 0)
                    bits[i] = _v[i];
                else
                    bits[i] = BoolExpr.False;
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
            var bits = new BoolExpr[_a.Bits + _shift];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = i >= _shift ? _a[i - _shift] : false;
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
                res[i] = (((_v >> i) & 1) == 1) ? _a[i] : !_a[i];

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
                res[i] = (((_v >> i) & 1) == 1) ? !_a[i] : _a[i];

            return OrExpr.Create(res).Flatten();
        }

        public static BoolExpr operator ==(UIntVar _a, UIntVar _b)
        {
            var res = new BoolExpr[Math.Max(_a.bit.Length, _b.bit.Length)];
            for (var i = 0; i < res.Length; i++)
                res[i] = (_a[i] == _b[i]).Flatten();

            return AndExpr.Create(res).Flatten();
        }

        public static BoolExpr operator !=(UIntVar _a, UIntVar _b)
        {
            var res = new BoolExpr[Math.Max(_a.bit.Length, _b.bit.Length)];
            for (var i = 0; i < res.Length; i++)
                res[i] = (_a[i] != _b[i]).Flatten();

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
            for (var i = nonZeroes; i < _a.bit.Length; i++)
                resAny[i - nonZeroes] = _a[i];
            var leadingZeroes = (resAny.Length != 0) ? OrExpr.Create(resAny) : false;

            var resOr = new List<BoolExpr>();
            for (var i = 0; i < nonZeroes; i++)
                if (((_v >> i) & 1) == 0)
                {
                    var allesDavorEq = AndExpr.Create(Enumerable.Range(i + 1, nonZeroes - i)
                        .Where(j => ((_v >> j) & 1) == 1)
                        .Select(j => _a[j]));
                    resOr.Add((_a[i] & allesDavorEq).Flatten());
                }
            var orExpr = (resOr.Count != 0) ? OrExpr.Create(resOr) : false;

            return leadingZeroes | orExpr;
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
            for (var i = nonZeroes; i < _a.bit.Length; i++)
                resAnd[i - nonZeroes] = !_a[i];
            var leadingZeroes = (resAnd.Length != 0) ? AndExpr.Create(resAnd).Flatten() : true;

            var resOr = new List<BoolExpr>();
            for (var i = 0; i < nonZeroes; i++)
                if (((_v >> i) & 1) == 1)
                {
                    var allesDavorEq = AndExpr.Create(Enumerable.Range(i + 1, nonZeroes - i - 1)
                        .Where(j => ((_v >> j) & 1) == 0)
                        .Select(j => !_a[j]));
                    resOr.Add((!_a[i] & allesDavorEq).Flatten());
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
                var allesDavorEq = AndExpr.Create(Enumerable.Range(i + 1, res.Length - i - 1).Select(j => _a[j] == _b[j])).Flatten();
                res[i] = ((_a[i] < _b[i]) & allesDavorEq).Flatten();
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
            if (ReferenceEquals(_b, BoolExpr.False))
                return Const(_a.Model, 0);
            if (ReferenceEquals(_b, BoolExpr.True))
                return _a;

            var bits = new BoolExpr[_a.Bits];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = (_a[i] & _b).Flatten();
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

            if (_a.Bits > _b.Bits)
                return _b * _a;

            var sum = new List<UIntVar>();
            for (var b = 0; b < _a.bit.Length; b++)
                sum.Add((_b << b) * _a[b]);

            var res = _a.Model.Sum(sum);
            res.UB = (_a.UB == Unbounded || _b.UB == Unbounded) ? Unbounded : (_a.UB * _b.UB);
            return res;
        }

        public static UIntVar operator +(BoolExpr _b, UIntVar _a) => _a + _b;
        public static UIntVar operator +(UIntVar _a, BoolExpr _b)
        {
            if (ReferenceEquals(_b, BoolExpr.False))
                return _a;

            var bits = new BoolExpr[(_a.UB == Unbounded) ? _a.Bits + 1 : RequiredBitsForUB(_a.UB + 1)];

            var carry = _b;
            for (var i = 0; i < bits.Length; i++)
            {
                bits[i] = (_a[i] ^ carry).Flatten();

                if (i < bits.Length - 1)
                {
                    carry = (_a[i] & carry).Flatten();

                    //unitprop
                    _a.Model.AddConstr(OrExpr.Create(bits[i], carry, !_a[i]));
                }
            }

            return new UIntVar(_a.Model, _a.UB == -1 ? -1 : _a.UB + 1, bits);
        }


        public static UIntVar operator ^(UIntVar _a, UIntVar _b)
        {
            var bits = new BoolExpr[Math.Max(_a.Bits, _b.Bits)];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = (_a[i] ^ _b[i]).Flatten();
            return new UIntVar(_a.Model, Unbounded, bits);
        }

        public static UIntVar operator |(UIntVar _a, UIntVar _b)
        {
            var bits = new BoolExpr[(_a.UB == Unbounded || _b.UB == Unbounded) ? Math.Max(_a.Bits, _b.Bits) : RequiredBitsForUB(_a.UB | _b.UB)];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = (_a[i] | _b[i]).Flatten();
            return new UIntVar(_a.Model, (_a.UB == Unbounded || _b.UB == Unbounded) ? Unbounded : (_a.UB | _b.UB), bits);
        }

        public static UIntVar operator &(UIntVar _a, UIntVar _b)
        {
            //TODO improve UB when only _a or _b are unbounded
            var bits = new BoolExpr[(_a.UB == Unbounded || _b.UB == Unbounded) ? Math.Min(_a.Bits, _b.Bits) : RequiredBitsForUB(Math.Min(_a.UB, _b.UB))];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = (_a[i] & _b[i]).Flatten();
            return new UIntVar(_a.Model, (_a.UB == Unbounded || _b.UB == Unbounded) ? Unbounded : Math.Min(_a.UB, _b.UB), bits);
        }

        public UIntVar Flatten()
        {
            if (bit.OfType<BoolVar>().Count() == bit.Length)
                return this;

            var bits = new BoolExpr[RequiredBitsForUB(UB)];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = bit[i].Flatten();
            return new UIntVar(Model, UB, bits);
        }

        public static UIntVar operator +(UIntVar _a, UIntVar _b)
        {
            if (_a.UB == 0)
                return _b;
            if (_b.UB == 0)
                return _a;

            var bits = new BoolExpr[(_a.UB == Unbounded || _b.UB == Unbounded) ? (Math.Max(_a.Bits, _b.Bits) + 1) : RequiredBitsForUB(_a.UB + _b.UB)];

            var carry = BoolExpr.False;
            for (var i = 0; i < bits.Length; i++)
            {
                bits[i] = (carry ^ _a[i] ^ _b[i]).Flatten();

                if (i < bits.Length - 1)
                {
                    var aAndB = (_a[i] & _b[i]).Flatten();
                    var aAndCarry = (_a[i] & carry).Flatten();
                    var bAndCarry = (_b[i] & carry).Flatten();

                    carry = OrExpr.Create(aAndB, aAndCarry, bAndCarry).Flatten();

                    //unitprop
                    _a.Model.AddConstr(OrExpr.Create(bits[i], carry, (!_a[i] & !_b[i])));
                }
            }

            return new UIntVar(_a.Model, (_a.UB == Unbounded || _b.UB == Unbounded || RequiredBitsForUB(_a.UB + (long)_b.UB) >= MaxBitsWithUB) ? Unbounded : (_a.UB + _b.UB), bits);
        }
    }
}
