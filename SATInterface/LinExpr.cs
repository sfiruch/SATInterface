using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SATInterface
{
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
    public class LinExpr
    {
        //TODO: tune this threshold
        private const int BinaryComparisonThreshold = 8;

        private Dictionary<BoolVar, int> Weights;
        private int Offset;

        private LinExpr? Negated;
        private List<BoolExpr[]>? SequentialCache;
        private UIntVar? UIntCache;

        public LinExpr()
        {
            Weights = new Dictionary<BoolVar, int>();
        }

        public int UB
        {
            get
            {
                var res = Offset;
                foreach (var e in Weights)
                    if (e.Value > 0)
                        res += e.Value;
                return res;
            }
        }

        public int X
        {
            get
            {
                var res = Offset;
                foreach (var e in Weights)
                    if (e.Key.X)
                        res += e.Value;
                return res;
            }
        }

        public static LinExpr operator *(int _a, LinExpr _b) => _b * _a;
        public static LinExpr operator *(LinExpr _a, int _b)
        {
            var res = new LinExpr();
            if (_b == 0)
                return res;

            res.Offset = _a.Offset * _b;
            foreach (var w in _a.Weights)
                res[w.Key] += w.Value * _b;
            return res;
        }

        public static LinExpr operator +(int _a, LinExpr _b) => _b + _a;
        public static LinExpr operator +(LinExpr _a, int _b)
        {
            if (_b == 0)
                return _a;

            var res = new LinExpr();
            res.Offset = _a.Offset + _b;
            foreach (var w in _a.Weights)
                res[w.Key] += w.Value;
            return res;
        }

        public static LinExpr operator +(LinExpr _a, LinExpr _b)
        {
            var res = new LinExpr();
            res.Offset = _a.Offset + _b.Offset;
            foreach (var w in _a.Weights)
                res[w.Key] += w.Value;
            foreach (var w in _b.Weights)
                res[w.Key] += w.Value;
            return res;
        }

        public static LinExpr operator -(LinExpr _a, LinExpr _b)
        {
            var res = new LinExpr();
            res.Offset = _a.Offset - _b.Offset;
            foreach (var w in _a.Weights)
                res[w.Key] += w.Value;
            foreach (var w in _b.Weights)
                res[w.Key] -= w.Value;
            return res;
        }

        public static LinExpr operator -(LinExpr _a, int _b)
        {
            if (_b == 0)
                return _a;

            var res = new LinExpr();
            res.Offset = _a.Offset - _b;
            foreach (var w in _a.Weights)
                res[w.Key] += w.Value;
            return res;
        }

        public static LinExpr operator -(LinExpr _a)
        {
            if (!(_a.Negated is null))
                return _a.Negated;

            var res = new LinExpr();
            res.Offset = -_a.Offset;
            foreach (var w in _a.Weights)
                res[w.Key] -= w.Value;
            return _a.Negated = res;
        }

        public static LinExpr operator -(int _a, LinExpr _b)
        {
            if (_a == 0)
                return -_b;

            var res = new LinExpr();
            res.Offset = _a - _b.Offset;
            foreach (var w in _b.Weights)
                res[w.Key] -= w.Value;
            return res;
        }

        public static BoolExpr operator <(LinExpr _a, LinExpr _b) => (_a - _b) < 0;
        public static BoolExpr operator <=(LinExpr _a, LinExpr _b) => (_a - _b) <= 0;
        public static BoolExpr operator !=(LinExpr _a, LinExpr _b) => (_a - _b) != 0;
        public static BoolExpr operator >(LinExpr _a, LinExpr _b) => (_a - _b) > 0;
        public static BoolExpr operator >=(LinExpr _a, LinExpr _b) => (_a - _b) >= 0;
        public static BoolExpr operator ==(LinExpr _a, LinExpr _b) => (_a - _b) == 0;

        public static BoolExpr operator <(int _a, LinExpr _b) => (_a - _b) < 0;
        public static BoolExpr operator <=(int _a, LinExpr _b) => (_a - _b) <= 0;
        public static BoolExpr operator !=(int _a, LinExpr _b) => (_a - _b) != 0;
        public static BoolExpr operator >(int _a, LinExpr _b) => (_a - _b) > 0;
        public static BoolExpr operator >=(int _a, LinExpr _b) => (_a - _b) >= 0;
        public static BoolExpr operator ==(int _a, LinExpr _b) => (_a - _b) == 0;

        public static BoolExpr operator <(LinExpr _a, int _b) => _a <= (_b - 1);

        //Hölldobler, Steffen, Norbert Manthey, and Peter Steinke. "A compact encoding
        //of pseudo-Boolean constraints into SAT." Annual Conference on Artificial
        //Intelligence. Springer, Berlin, Heidelberg, 2012.
        //-- http://www.wv.inf.tu-dresden.de/Publications/2012/report12-03.pdf

        private IEnumerable<BoolExpr> ComputeSequential(int _limit)
        {
            if (SequentialCache is null)
                SequentialCache = new List<BoolExpr[]>();

            if (SequentialCache.Count < _limit)
            {
                //convert to all-positive weights
                var posVar = new List<(BoolExpr be, int Weight)>();
                foreach (var e in Weights)
                    if (e.Value > 0)
                        posVar.Add((be: e.Key, Weight: e.Value));
                    else
                        posVar.Add((be: !e.Key, Weight: -e.Value));

                for (var j = SequentialCache.Count; j < _limit; j++)
                {
                    if (posVar.Any())
                    {
                        var vars = new BoolExpr[posVar.Count];
                        vars[0] = j < posVar[0].Weight ? posVar[0].be : BoolExpr.False;
                        for (var i = 1; i < posVar.Count; i++)
                            vars[i] = ((j >= posVar[i].Weight ? (SequentialCache[j - posVar[i].Weight][i - 1] & posVar[i].be) : posVar[i].be) | vars[i - 1]).Flatten();
                        SequentialCache.Add(vars);
                    }
                    else
                        SequentialCache.Add(new[] { BoolExpr.False });
                }
            }

            return Enumerable.Range(0, _limit).Select(i => SequentialCache[i].Last());
        }

        private UIntVar ComputeUInt()
        {
            if (!(UIntCache is null))
                return UIntCache;

            //convert to all-positive weights
            var posVar = new List<UIntVar>();
            var singleVar = new List<BoolExpr>();
            var offset = Offset;
            var model = Weights.First().Key.Model;
            foreach (var e in Weights)
                if (e.Value == 1)
                {
                    singleVar.Add(e.Key);
                }
                else if (e.Value > 0)
                    posVar.Add((UIntVar)e.Key * e.Value);
                else
                {
                    posVar.Add(UIntVar.ITE(!e.Key, UIntVar.Const(model, -e.Value), UIntVar.Const(model, 0)));
                    offset += e.Value;
                }

            if (singleVar.Any() && posVar.Any())
                UIntCache = model.SumUInt(singleVar) + model.Sum(posVar);
            else if (singleVar.Any() && !posVar.Any())
                UIntCache = model.SumUInt(singleVar);
            else if (!singleVar.Any() && posVar.Any())
                UIntCache = model.Sum(posVar);
            else if (!singleVar.Any() && !posVar.Any())
                UIntCache = UIntVar.Const(model, 0);
            else
                throw new Exception();

            return UIntCache;
        }

        private void ClearCached()
        {
            Negated = null;
            SequentialCache = null;
            UIntCache = null;
        }

        public static BoolExpr operator <=(LinExpr _a, int _b)
        {
            var rhs = _b - _a.Offset;
            foreach (var e in _a.Weights)
                if (e.Value < 0)
                    rhs -= e.Value;

            if (rhs < 0)
                return BoolExpr.False;
            if (rhs >= _a.Weights.Sum(x => Math.Abs(x.Value)))
                return BoolExpr.True;

            if (rhs > BinaryComparisonThreshold)
                return _a.ComputeUInt() <= rhs;

            return !_a.ComputeSequential(rhs + 1).Last();
        }

        public static BoolExpr operator ==(LinExpr _a, int _b)
        {
            var rhs = _b - _a.Offset;
            foreach (var e in _a.Weights)
                if (e.Value < 0)
                    rhs -= e.Value;

            if (rhs < 0)
                return BoolExpr.False;
            if (rhs > _a.Weights.Sum(x => Math.Abs(x.Value)))
                return BoolExpr.False;

            if (rhs > BinaryComparisonThreshold)
                return _a.ComputeUInt() == rhs;

            var v = _a.ComputeSequential(rhs + 1).ToArray();

            if (rhs == 0)
                return !v[rhs];
            else
                return v[rhs - 1] & !v[rhs];
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var e in Weights)
            {
                if (sb.Length == 0)
                {
                    if (e.Value == -1)
                        sb.Append("-");
                    else if (e.Value != 1)
                        sb.Append(e.Value + "*");
                }
                else
                {
                    if (e.Value == -1)
                        sb.Append(" - ");
                    else if (e.Value < 0)
                        sb.Append($" - {-e.Value}*");
                    else if (e.Value == 1)
                        sb.Append(" + ");
                    else if (e.Value > 1)
                        sb.Append($" + {e.Value}*");
                    else
                        throw new Exception();
                }
                sb.Append(e.Key);
            }
            return sb.ToString();
        }

        public static BoolExpr operator !=(LinExpr _a, int _b) => !(_a == _b);
        public static BoolExpr operator >(LinExpr _a, int _b) => -_a < -_b;
        public static BoolExpr operator >=(LinExpr _a, int _b) => -_a <= -_b;


        public static explicit operator LinExpr(int _const) => new LinExpr() { Offset = _const };

        public static explicit operator LinExpr(BoolExpr _be)
        {
            var le = new LinExpr();
            le.AddTerm(_be);
            return le;
        }

        private int this[BoolVar _bv]
        {
            get => Weights.TryGetValue(_bv, out var weight) ? weight : 0;
            set
            {
                if (value == 0)
                    Weights.Remove(_bv);
                else
                    Weights[_bv] = value;
            }
        }

        public void AddTerm(BoolExpr _be, int _weight = 1)
        {
            ClearCached();

            var be = _be.Flatten();
            if (ReferenceEquals(be, BoolExpr.True))
                Offset += _weight;
            else if (ReferenceEquals(be, BoolExpr.False))
            {
                //do nothing
            }
            else if (be is BoolVar bv)
                this[bv] += _weight;
            else if (be is NotExpr ne)
            {
                Offset += _weight;
                this[ne.inner] -= _weight;
            }
            else
                throw new Exception();
        }
    }
}
