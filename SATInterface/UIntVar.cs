using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SATInterface
{
    public class UIntVar
    {
        private Model Model;
        private BoolExpr[] bit;
        private int UB;

        public UIntVar(Model _model, int _ub, bool _enforceUB = true)
        {
            Model = _model;
            UB = _ub;

            if (UB == 0)
                bit = new BoolExpr[0];
            else
            {
                bit = new BoolExpr[Log2(UB) + 1];
                for (var i = 0; i < bit.Length; i++)
                    bit[i] = new BoolVar(_model);

                if (_enforceUB && (UB & (UB + 1)) != 0)
                    Model.AddConstr(this <= UB);
            }
        }

        public static explicit operator UIntVar(BoolVar _v)
        {
            var res = new UIntVar(_v.Model, 1, false);
            res.bit[0] = _v;
            return res;
        }

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
            var res = new UIntVar(_model, _c, false);
            for (var i = 0; i < res.bit.Length; i++)
                res.bit[i] = ((_c >> i) & 1) == 1;

            return res;
        }

        private BoolExpr this[int _bitIndex]
        {
            get
            {
                if (_bitIndex >= bit.Length)
                    return BoolExpr.FALSE;

                return bit[_bitIndex];
            }
        }

        public static UIntVar operator >>(UIntVar _a, int _shift)
        {
            var res = new UIntVar(_a.Model, _a.UB >> _shift, false);
            for (var i = 0; i < res.bit.Length; i++)
                res.bit[i] = _a.bit[i + _shift];
            return res;
        }

        public static UIntVar operator +(UIntVar _a, int _add) => _a + Const(_a.Model, _add);
        public static UIntVar operator +(int _add, UIntVar _a) => _a + Const(_a.Model, _add);

        public static UIntVar operator -(UIntVar _a, int _add) => _a - Const(_a.Model, _add);
        public static UIntVar operator -(int _add, UIntVar _a) => Const(_a.Model, _add) - _a;

        public static UIntVar operator -(UIntVar _a, UIntVar _b)
        {
            var res = new UIntVar(_a.Model, _a.UB, false);
            _a.Model.AddConstr(_b + res == _a);
            return res;
        }

        public static UIntVar operator <<(UIntVar _a, int _shift)
        {
            var res = new UIntVar(_a.Model, _a.UB << _shift, false);
            for (var i = 0; i < res.bit.Length; i++)
                res.bit[i] = i >= _shift ? _a.bit[i - _shift] : false;
            return res;
        }

        private static int Log2(int _n)
        {
            var res = 0;
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
                        res |= (1 << i);
                return res;
            }
        }

        public static BoolExpr operator ==(int _v, UIntVar _a) => _a == _v;
        public static BoolExpr operator ==(UIntVar _a, int _v)
        {
            if (_v < 0)
                return false;
            if (_v > _a.UB)
                return false;

            var res = new BoolExpr[_a.bit.Length];
            for (var i = 0; i < res.Length; i++)
                res[i] = (((_v >> i) & 1) == 1) ? _a.bit[i] : !_a.bit[i];

            return AndExpr.Create(res);
        }

        public static BoolExpr operator !=(int _v, UIntVar _a) => _a != _v;
        public static BoolExpr operator !=(UIntVar _a, int _v)
        {
            if (_v < 0)
                return true;
            if (_v > _a.UB)
                return true;

            var res = new BoolExpr[_a.bit.Length];
            for (var i = 0; i < res.Length; i++)
                res[i] = (((_v >> i) & 1) == 1) ? !_a.bit[i] : _a.bit[i];

            return OrExpr.Create(res);
        }

        public static BoolExpr operator ==(UIntVar _a, UIntVar _b)
        {
            var res = new BoolExpr[Math.Max(_a.bit.Length, _b.bit.Length)];
            for (var i = 0; i < res.Length; i++)
                res[i] = _a[i] == _b[i];

            return AndExpr.Create(res);
        }

        public static BoolExpr operator !=(UIntVar _a, UIntVar _b)
        {
            var res = new BoolExpr[Math.Max(_a.bit.Length, _b.bit.Length)];
            for (var i = 0; i < res.Length; i++)
                res[i] = _a[i] != _b[i];

            return OrExpr.Create(res);
        }

        public static BoolExpr operator >(int _v, UIntVar _a) => _a < _v;
        public static BoolExpr operator >(UIntVar _a, int _v)
        {
            if (_v < 0)
                return true;
            if (_v >= _a.UB)
                return false;

            var res = new BoolExpr[_a.bit.Length];
            for (var i = 0; i < res.Length; i++)
            {
                var allesDavorEq = AndExpr.Create(Enumerable.Range(i + 1, res.Length - i - 1).Select(j => (((_v >> j) & 1) == 1) ? _a.bit[j] : !_a.bit[j]));
                res[i] = (_a.bit[i] > (((_v >> i) & 1) == 1)) & allesDavorEq;
            }

            return OrExpr.Create(res);
        }

        public static BoolExpr operator <(int _v, UIntVar _a) => _a > _v;
        public static BoolExpr operator <(UIntVar _a, int _v)
        {
            if (_v <= 0)
                return false;
            if (_v > _a.UB)
                return true;

            var res = new BoolExpr[_a.bit.Length];
            for (var i = 0; i < res.Length; i++)
            {
                var allesDavorEq = AndExpr.Create(Enumerable.Range(i + 1, res.Length - i - 1).Select(j => (((_v >> j) & 1) == 1) ? _a.bit[j] : !_a.bit[j]));
                res[i] = (_a.bit[i] < (((_v >> i) & 1) == 1)) & allesDavorEq;
            }

            return OrExpr.Create(res);
        }




        public static BoolExpr operator >(UIntVar _a, UIntVar _b) => _b < _a;
        public static BoolExpr operator <(UIntVar _a, UIntVar _b)
        {
            var res = new BoolExpr[Math.Max(_a.bit.Length, _b.bit.Length)];
            for (var i = 0; i < _a.bit.Length && i < _b.bit.Length; i++)
            {
                var allesDavorEq = AndExpr.Create(Enumerable.Range(i + 1, res.Length - i - 1).Select(j => _a[j] == _b[j]));
                res[i] = (_a.bit[i] < _b.bit[i]) & allesDavorEq;
            }

            return OrExpr.Create(res);
        }



        public static BoolExpr operator >=(UIntVar _a, int _v) => _a > (_v - 1);
        public static BoolExpr operator <=(UIntVar _a, int _v) => _a < (_v + 1);

        public static BoolExpr operator >=(int _v, UIntVar _a) => _a < (_v + 1);
        public static BoolExpr operator <=(int _v, UIntVar _a) => _a > (_v - 1);
        public static BoolExpr operator >=(UIntVar _a, UIntVar _b) => (_a > _b) | (_a == _b);
        public static BoolExpr operator <=(UIntVar _a, UIntVar _b) => (_a < _b) | (_a == _b);

        public static UIntVar operator +(BoolExpr _b, UIntVar _a) => _a + _b;
        public static UIntVar operator +(UIntVar _a, BoolExpr _b)
        {
            if (ReferenceEquals(_b, BoolExpr.FALSE))
                return _a;

            var res = new UIntVar(_a.Model, _a.UB + 1, _enforceUB: false);

            var carry = _b;
            for (var i = 0; i < res.bit.Length; i++)
            {
                res.Model.AddConstr(res.bit[i] == (_a[i] ^ carry));

                if (i < res.bit.Length - 1)
                {
                    var nc = new BoolVar(res.Model);
                    res.Model.AddConstr(nc == (_a[i] & carry));
                    carry = nc;

                    //unitprop
                    res.Model.AddConstr(OrExpr.Create(res.bit[i], carry, !_a[i]));
                }
            }

            return res;
        }


        public static UIntVar operator *(UIntVar _a, BoolExpr _b)
        {
            var res = new UIntVar(_a.Model, _a.UB, false);

            for (var i = 0; i < res.bit.Length; i++)
                res.bit[i] = _a.bit[i] & _b;

            return res;
        }


        public static UIntVar operator |(UIntVar _a, UIntVar _b)
        {
            var res = new UIntVar(_a.Model, Math.Max(_a.UB, _b.UB), false);
            for (var i = 0; i < res.bit.Length; i++)
                res.bit[i] = _a[i] | _b[i];
            return res;
        }

        public static UIntVar operator &(UIntVar _a, UIntVar _b)
        {
            var res = new UIntVar(_a.Model, Math.Min(_a.UB, _b.UB), false);
            for (var i = 0; i < res.bit.Length; i++)
                res.bit[i] = _a[i] & _b[i];
            return res;
        }

        public UIntVar Flatten()
        {
            if (bit.OfType<BoolVar>().Count() == bit.Length)
                return this;

            var res = new UIntVar(Model, UB, false);
            for (var i = 0; i < res.bit.Length; i++)
                if ((bit[i] is BoolVar) || (bit[i] is NotExpr) || ReferenceEquals(bit[i],BoolExpr.FALSE) || ReferenceEquals(bit[i],BoolExpr.TRUE))
                    res.bit[i] = bit[i];
                else
                {
                    res.bit[i] = new BoolVar(Model);
                    Model.AddConstr(res.bit[i] == bit[i]);
                }

            return res;
        }

        public static UIntVar operator +(UIntVar _a, UIntVar _b)
        {
            if (_a.UB == 0)
                return _b;
            if (_b.UB == 0)
                return _a;

            var res = new UIntVar(_a.Model, _a.UB + _b.UB, _enforceUB: false);

            var carry = BoolExpr.FALSE;
            for (var i = 0; i < res.bit.Length; i++)
            {
                res.Model.AddConstr(res.bit[i] == (_a[i] ^ _b[i] ^ carry));

                if (i < res.bit.Length - 1)
                {
                    var aAndB = new BoolVar(res.Model);
                    res.Model.AddConstr(aAndB == (_a[i] & _b[i]));

                    var aAndCarry = new BoolVar(res.Model);
                    res.Model.AddConstr(aAndCarry == (_a[i] & carry));

                    var bAndCarry = new BoolVar(res.Model);
                    res.Model.AddConstr(bAndCarry == (_b[i] & carry));

                    var notAAndNotB = new BoolVar(res.Model);
                    res.Model.AddConstr(notAAndNotB == (!_a[i] & !_b[i]));

                    var nc = new BoolVar(res.Model);
                    res.Model.AddConstr(nc == OrExpr.Create(aAndB, aAndCarry, bAndCarry));
                    carry = nc;

                    //unitprop
                    res.Model.AddConstr(OrExpr.Create(res.bit[i], carry, notAAndNotB));
                }
            }

            return res;
        }
    }
}
