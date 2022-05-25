using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SATInterface
{
    /// <summary>
    /// A LinExpr is a linear combination of BoolVars with integer weights.
    /// </summary>
    public class LinExpr
    {
        private Dictionary<BoolVar, int> Weights;
        private int Offset;

        private Model? Model;
        private LinExpr? Negated;

        public LinExpr(int _c = 0)
        {
            Weights = new Dictionary<BoolVar, int>();
            Offset = _c;
        }

        internal LinExpr(UIntVar _src)
        {
            if (_src.UB == UIntVar.Unbounded)
                throw new ArgumentException($"Only bounded variables supported");

            Model = _src.Model;

            Weights = new Dictionary<BoolVar, int>();
            Offset = 0;
            for (var i = 0; i < _src.Bits.Length; i++)
                AddTerm(_src.bit[i], 1 << i);
        }

        /// <summary>
        /// Returns the upper bound of this expression.
        /// </summary>
        public int UB
        {
            get
            {
                checked
                {
                    var res = Offset;
                    foreach (var e in Weights)
                        if (e.Value > 0)
                            res += e.Value;
                    return res;
                }
            }
        }

        /// <summary>
        /// Returns the lower bound of this expression.
        /// </summary>
        public int LB
        {
            get
            {
                checked
                {
                    var res = Offset;
                    foreach (var e in Weights)
                        if (e.Value < 0)
                            res += e.Value;
                    return res;
                }
            }
        }

        /// <summary>
        /// Returns the number of variables.
        /// </summary>
        public int Size => Weights.Count;

        /// <summary>
        /// Returns the value of this expression in a SAT model.
        /// </summary>
        public int X
        {
            get
            {
                checked
                {
                    var res = Offset;
                    foreach (var e in Weights)
                        if (e.Key.X)
                            res += e.Value;
                    return res;
                }
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

        public static BoolExpr operator <(int _a, LinExpr _b) => _b > _a;
        public static BoolExpr operator <=(int _a, LinExpr _b) => _b >= _a;
        public static BoolExpr operator !=(int _a, LinExpr _b) => _b != _a;
        public static BoolExpr operator >(int _a, LinExpr _b) => _b < _a;
        public static BoolExpr operator >=(int _a, LinExpr _b) => _b <= _a;
        public static BoolExpr operator ==(int _a, LinExpr _b) => _b == _a;

        public static BoolExpr operator <(LinExpr _a, int _b) => _a <= (_b - 1);

        private UIntVar ToUInt()
        {
            Debug.Assert(Model is not null);

            if (Model.UIntCache.TryGetValue(this, out var res))
                return res;

            //convert to all-positive weights
            var posWeights = new List<(int Weight, BoolExpr Var)>();
            foreach (var e in Weights)
                if (e.Value > 0)
                    posWeights.Add((e.Value, e.Key));
                else
                {
                    Debug.Assert(e.Value < 0);
                    posWeights.Add((-e.Value, !e.Key));
                }

            var toSum = new List<UIntVar>();
            var minWeightVars = new List<BoolExpr>();
            while (posWeights.Any())
            {
                var minWeight = int.MaxValue;
                foreach (var pw in posWeights)
                {
                    if (pw.Weight < minWeight)
                    {
                        minWeight = pw.Weight;
                        minWeightVars.Clear();
                    }
                    if (pw.Weight == minWeight)
                        minWeightVars.Add(pw.Var);
                }

                posWeights.RemoveAll(pw => pw.Weight == minWeight);

                if (minWeightVars.Count == 1)
                    toSum.Add(Model.ITE(minWeightVars.Single(), Model.AddUIntConst(minWeight), Model.AddUIntConst(0)));
                else
                {
                    var sum = Model.SumUInt(minWeightVars);
                    if (sum.UB > 0)
                    {
                        for (var i = 1; i < sum.Bits.Length; i++)
                            posWeights.Add((minWeight << i, sum.Bits[i]));

                        toSum.Add(Model.ITE(sum.Bits[0], Model.AddUIntConst(minWeight), Model.AddUIntConst(0)));
                    }
                }
            }

            return Model.UIntCache[this] = Model.Sum(CollectionsMarshal.AsSpan(toSum));
        }

        private void ClearCached()
        {
            Negated = null;
        }

        private static int GCD(int a, int b) => b == 0 ? a : GCD(b, a % b);

        public static BoolExpr operator <=(LinExpr _a, int _b)
        {
            if (_a.UB <= _b)
                return Model.True;
            if (_a.LB > _b)
                return Model.False;

            Debug.Assert(_a.Model is not null);

            if (!_a.Model.LinExprLECache.TryGetValue((_a, _b), out var res))
                _a.Model.LinExprLECache[(_a, _b)] = res = UncachedLE(_a, _b).Flatten();

            return res;
        }

        private static BoolExpr UncachedLE(LinExpr _a, int _b)
        {
            Debug.Assert(_a.Model is not null);

            var rhs = _b - _a.Offset;
            var ub = 0;
            foreach (var e in _a.Weights)
            {
                ub += Math.Abs(e.Value);
                if (e.Value < 0)
                    rhs -= e.Value;
            }

            Debug.Assert(rhs >= 0); //because of the early Model.False abort above

            if (rhs == 0)
                return AndExpr.Create(_a.Weights.Select(w => w.Value > 0 ? !w.Key : w.Key).ToArray());

            if (_a.Weights.Any(w => Math.Abs(w.Value) > rhs))
            {
                var vWithout = new LinExpr();
                foreach (var w in _a.Weights)
                    if (w.Value > 0 && w.Value <= rhs)
                        vWithout.AddTerm(w.Key, w.Value);
                    else if (w.Value < 0 && -w.Value <= rhs)
                        vWithout.AddTerm(!w.Key, -w.Value);

                return _a.Model.And(_a.Weights
                    .Where(w => Math.Abs(w.Value) > rhs)
                    .Select(w => w.Value > 0 ? !w.Key : w.Key)
                    .Append(vWithout <= rhs));
            }

            var smallest = _a.Weights.Values.Min(v => Math.Abs(v));
            if (rhs == smallest)
                return _a.Model.AtMostOneOf(_a.Weights.Where(w => Math.Abs(w.Value) == smallest).Select(w => w.Value > 0 ? w.Key : !w.Key))
                    & AndExpr.Create(_a.Weights.Where(w => Math.Abs(w.Value) > smallest).Select(w => w.Value > 0 ? !w.Key : w.Key).ToArray());

            //recognize implications
            if (_a.Weights.Values.Count(v => Math.Abs(v) == rhs) == 1 && _a.Weights.Values.Count(v => Math.Abs(v) == 1) == rhs)
            {
                Debug.Assert(_a.Weights.Values.All(v => Math.Abs(v) == 1 || Math.Abs(v) == rhs));

                //a + b + !c + !d + 4e <= 4

                var e = _a.Weights.Single(w => Math.Abs(w.Value) == rhs);
                var others = _a.Weights.Where(w => Math.Abs(w.Value) == 1).Select(w => w.Value > 0 ? w.Key : !w.Key).ToArray();

                return (e.Value > 0 ? !e.Key : e.Key) | !OrExpr.Create(others).Flatten();
            }

            if (rhs == 1 && _a.Weights.Values.All(v => Math.Abs(v) == 1))
                return _a.Model.AtMostOneOf(_a.Weights.Select(w => w.Value > 0 ? w.Key : !w.Key));

            var gcd = _a.Weights.Values.Select(w => Math.Abs(w)).Where(w => w < rhs).Aggregate(GCD);
            if (gcd > 1)
            {
                var vDiv = new LinExpr();
                foreach (var w in _a.Weights)
                    if (w.Value > 0)
                        vDiv.AddTerm(w.Key, Math.Min(rhs, w.Value) / gcd);
                    else if (w.Value < 0)
                        vDiv.AddTerm(!w.Key, Math.Min(rhs, -w.Value) / gcd);
                return vDiv <= rhs / gcd;
            }

            if (_a.Weights.Values.Count(v => Math.Abs(v) == rhs) >= 2)
            {
                var vWithout = new LinExpr();
                foreach (var w in _a.Weights)
                    if (w.Value > 0 && w.Value != rhs)
                        vWithout.AddTerm(w.Key, w.Value);
                    else if (w.Value < 0 && -w.Value != rhs)
                        vWithout.AddTerm(!w.Key, -w.Value);

                var vRHS = _a.Weights.Where(v => Math.Abs(v.Value) == rhs)
                    .Select(v => v.Value > 0 ? v.Key : !v.Key).ToArray();

                if (vWithout.Weights.Any())
                    return _a.Model.And(
                        _a.Model.AtMostOneOf(vRHS),
                        vWithout <= rhs,
                        !_a.Model.Or(vRHS) | vWithout == 0);
                else
                    return _a.Model.AtMostOneOf(vRHS);
            }

            if (rhs > ub / 2)
                return !(-_a <= (-_b - 1));

            Debug.Assert(_a.Weights.Count > 2);

            if (_a.Weights.Count <= _a.Model.Configuration.LinExprBinaryComparisonThreshold)
            {
                var maxVar = _a.Weights.MaxBy(w => Math.Abs(w.Value));
                var withoutMaxVar = _a - maxVar.Key * maxVar.Value;
                return _a.Model.ITE(maxVar.Key,
                    withoutMaxVar <= _b - maxVar.Value,
                    withoutMaxVar <= _b);
            }
            else
                return _a.ToUInt() <= rhs;
        }

        public static BoolExpr operator ==(LinExpr _a, int _b)
        {
            if (_a.UB < _b || _a.LB > _b)
                return Model.False;

            if (_a.LB == _a.UB)
                return _a.LB == _b ? Model.True : Model.False;

            Debug.Assert(_a.Model is not null);

            if (!_a.Model.LinExprEqCache.TryGetValue((_a, _b), out var res))
                _a.Model.LinExprEqCache[(_a, _b)] = res = UncachedEq(_a, _b).Flatten();

            return res;
        }

        private static BoolExpr UncachedEq(LinExpr _a, int _b)
        {
            Debug.Assert(_a.Model is not null);

            var ub = 0;
            var rhs = _b - _a.Offset;
            foreach (var e in _a.Weights)
            {
                ub += Math.Abs(e.Value);
                if (e.Value < 0)
                    rhs -= e.Value;
            }

            Debug.Assert(rhs >= 0);
            Debug.Assert(rhs <= _a.Weights.Sum(x => Math.Abs(x.Value)));

            if (rhs == 0)
                return AndExpr.Create(_a.Weights.Select(x => x.Value > 0 ? !x.Key : x.Key).ToArray());

            if (_a.Weights.Any(w => Math.Abs(w.Value) > rhs))
            {
                var vWithout = new LinExpr();
                foreach (var w in _a.Weights)
                    if (w.Value > 0 && w.Value <= rhs)
                        vWithout.AddTerm(w.Key, w.Value);
                    else if (w.Value < 0 && -w.Value <= rhs)
                        vWithout.AddTerm(!w.Key, -w.Value);

                return _a.Model.And(_a.Weights
                    .Where(w => Math.Abs(w.Value) > rhs)
                    .Select(w => w.Value > 0 ? !w.Key : w.Key)
                    .Append(vWithout == rhs));
            }

            var smallest = _a.Weights.Values.Min(v => Math.Abs(v));
            if (rhs == smallest)
                return _a.Model.ExactlyOneOf(_a.Weights.Where(w => Math.Abs(w.Value) == smallest).Select(w => w.Value > 0 ? w.Key : !w.Key))
                    & AndExpr.Create(_a.Weights.Where(w => Math.Abs(w.Value) != smallest).Select(w => w.Value > 0 ? !w.Key : w.Key).ToArray());

            var gcd = _a.Weights.Values.Select(w => Math.Abs(w)).Aggregate(GCD);
            if (rhs % gcd != 0)
                return Model.False;

            if (gcd > 1)
            {
                var vDiv = new LinExpr();
                foreach (var w in _a.Weights)
                    if (w.Value > 0)
                        vDiv.AddTerm(w.Key, w.Value / gcd);
                    else if (w.Value < 0)
                        vDiv.AddTerm(!w.Key, -w.Value / gcd);
                return vDiv == rhs / gcd;
            }

            if (_a.Weights.Values.Count(v => Math.Abs(v) == rhs) >= 2)
            {
                var vWithout = new LinExpr();
                foreach (var w in _a.Weights)
                    if (w.Value > 0 && w.Value != rhs)
                        vWithout.AddTerm(w.Key, w.Value);
                    else if (w.Value < 0 && -w.Value != rhs)
                        vWithout.AddTerm(!w.Key, -w.Value);

                var vRHS = _a.Weights.Where(v => Math.Abs(v.Value) == rhs)
                    .Select(v => v.Value > 0 ? v.Key : !v.Key).ToArray();

                if (vWithout.Weights.Any())
                    return _a.Model.Or(
                        _a.Model.ExactlyOneOf(vRHS) & vWithout == 0,
                        !_a.Model.Or(vRHS) & vWithout == rhs);
                else
                    return _a.Model.ExactlyOneOf(vRHS);
            }

            if (rhs > ub / 2)
                return -_a == -_b;

            Debug.Assert(_a.Weights.Count > 2);

            if (_a.Weights.Count <= _a.Model.Configuration.LinExprBinaryComparisonThreshold)
            {
                var maxVar = _a.Weights.MaxBy(w => Math.Abs(w.Value));
                var withoutMaxVar = _a - maxVar.Key * maxVar.Value;
                return _a.Model.ITE(maxVar.Key,
                    withoutMaxVar == _b - maxVar.Value,
                    withoutMaxVar == _b);
            }
            else
                return _a.ToUInt() == rhs;
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
        public static BoolExpr operator >=(LinExpr _a, int _b) => !(_a <= (_b - 1));
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
                if (_bv.Model is not null)
                    Model = _bv.Model;

                if (value == 0)
                    Weights.Remove(_bv);
                else
                    Weights[_bv] = value;
            }
        }

        public void AddTerm(LinExpr _le, int _weight = 1)
        {
            if (_weight == 0)
                return;

            ClearCached();

            Offset += _le.Offset * _weight;

            foreach (var e in _le.Weights)
                this[e.Key] += e.Value * _weight;
        }

        public void AddTerm(BoolExpr _be, int _weight = 1)
        {
            ClearCached();

            var be = _be.Flatten();
            if (ReferenceEquals(be, Model.True))
                Offset += _weight;
            else if (ReferenceEquals(be, Model.False))
            {
                //do nothing
            }
            else if (be is BoolVar bv)
            {
                if (bv.Model is not null)
                    Model = bv.Model;
                this[bv] += _weight;
            }
            else if (be is NotExpr ne)
            {
                if (ne.inner.Model is not null)
                    Model = ne.inner.Model;

                Offset += _weight;
                this[ne.inner] -= _weight;
            }
            else
                throw new Exception();
        }

        public override int GetHashCode()
        {
            var hc = new HashCode();

            hc.Add(Offset);
            foreach (var e in Weights.OrderBy(e => e.Key.Id))
            {
                hc.Add(e.Key.Id);
                hc.Add(e.Value);
            }

            return hc.ToHashCode();
        }

        public override bool Equals(object? _o)
        {
            if (_o is not LinExpr le)
                return false;

            if (Offset != le.Offset)
                return false;

            return Weights.OrderBy(e => e.Key.Id).SequenceEqual(le.Weights.OrderBy(e => e.Key.Id));
        }
    }
}
