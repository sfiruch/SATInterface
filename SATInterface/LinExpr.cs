using System;
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

        private LinExpr? Negated;
        private Dictionary<int, BoolExpr>[]? HasValueXCache;
        private Dictionary<int, BoolExpr>[]? HasValueAtLeastXCache;
        private UIntVar? UIntCache;
        private int UIntCacheOffset; //offset has to be added to UIntCache value, may be negative

        public LinExpr(int _c = 0)
        {
            Weights = new Dictionary<BoolVar, int>();
            Offset = _c;
        }

        internal LinExpr(UIntVar _src)
        {
            if (_src.UB == UIntVar.Unbounded)
                throw new ArgumentException($"Only bounded variables supported");

            Weights = new Dictionary<BoolVar, int>();
            Offset = 0;
            for (var i = 0; i < _src.Bits.Length; i++)
                AddTerm(_src.bit[i], 1 << i);
            UIntCache = _src;
        }

        /// <summary>
        /// Returns the upper bound of this expression.
        /// </summary>
        public int UB
        {
            get
            {
                if (!(UIntCache is null) && UIntCache.UB != UIntVar.Unbounded)
                    return UIntCache.UB;

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

        public static BoolExpr operator <(int _a, LinExpr _b) => (_a - _b) < 0;
        public static BoolExpr operator <=(int _a, LinExpr _b) => (_a - _b) <= 0;
        public static BoolExpr operator !=(int _a, LinExpr _b) => (_a - _b) != 0;
        public static BoolExpr operator >(int _a, LinExpr _b) => (_a - _b) > 0;
        public static BoolExpr operator >=(int _a, LinExpr _b) => (_a - _b) >= 0;
        public static BoolExpr operator ==(int _a, LinExpr _b) => (_a - _b) == 0;

        public static BoolExpr operator <(LinExpr _a, int _b) => _a <= (_b - 1);

        private (UIntVar Var, int Offset) ToUInt()
        {
            if (!(UIntCache is null))
                return (UIntCache, UIntCacheOffset);

            //convert to all-positive weights
            var posWeights = new List<(int Weight, BoolExpr Var)>();
            var offset = Offset;
            var model = Weights.First().Key.Model;
            foreach (var e in Weights)
                if (e.Value > 0)
                    posWeights.Add((e.Value, e.Key));
                else
                {
                    Debug.Assert(e.Value < 0);
                    posWeights.Add((-e.Value, !e.Key));
                    offset += e.Value;
                }


            var toSum = new List<UIntVar>();
            while (posWeights.Any())
            {
                var minWeight = posWeights.Min(pw => pw.Weight);
                var minWeightVars = posWeights.Where(pw => pw.Weight == minWeight).Select(pw => pw.Var).ToArray();
                posWeights.RemoveAll(pw => pw.Weight == minWeight);

                if (minWeightVars.Length == 1)
                    toSum.Add(model.ITE(minWeightVars.Single(), model.AddUIntConst(minWeight), model.AddUIntConst(0)));
                else
                {
                    var sum = model.SumUInt(minWeightVars);
                    if (sum.UB > 0)
                    {
                        for (var i = 1; i < sum.Bits.Length; i++)
                            posWeights.Add((minWeight << i, sum.Bits[i]));

                        toSum.Add(model.ITE(sum.Bits[0], model.AddUIntConst(minWeight), model.AddUIntConst(0)));
                    }
                }
            }
            return (UIntCache, UIntCacheOffset) = (model.Sum(CollectionsMarshal.AsSpan(toSum)), offset);


            //return (UIntCache, UIntCacheOffset) = (model.Sum(posWeights
            //    .GroupBy(vw => vw.Weight)
            //    .Select(g => g.Count() == 1 ?
            //        model.ITE(g.Single().Var, model.AddUIntConst(g.Key), model.AddUIntConst(0))
            //        : (model.SumUInt(g.Select(e => e.Var)) * g.Key))
            //    ), offset);

            //return (UIntCache, UIntCacheOffset) = (model.Sum(posWeights
            //    .Select(pw => model.ITE(pw.Var, model.AddUIntConst(pw.Weight), model.AddUIntConst(0)))
            //    ), offset);
        }

        private void ClearCached()
        {
            Negated = null;
            HasValueXCache = null;
            HasValueAtLeastXCache = null;
            UIntCache = null;
        }

        private BoolExpr HasValueAtLeastX(int _x)
        {
            if (_x <= 0)
                return Model.True;

            if (Weights.Count == 0)
                return Model.False;

            var w = Weights.Select(w => (Var: w.Value > 0 ? w.Key : !w.Key, Weight: Math.Abs(w.Value))).OrderByDescending(e => e.Weight).ToArray();
            return HasValueAtLeastX(_x, 0, w.Sum(e => e.Weight), w, Weights.First().Key.Model);
        }

        private BoolExpr HasValueAtLeastX(int _x, int _i, int _ub, (BoolExpr Var, int Weight)[] _weights, Model _m)
        {
            Debug.Assert(_ub >= 0);

            if (_x <= 0)
                return Model.True;

            if (_x > _ub || _i >= _weights.Length)
                return Model.False;

            if (HasValueAtLeastXCache is null)
            {
                HasValueAtLeastXCache = new Dictionary<int, BoolExpr>[_weights.Length];
                for (var i = 0; i < _weights.Length; i++)
                    HasValueAtLeastXCache[i] = new();
            }

            if (!HasValueAtLeastXCache[_i].TryGetValue(_x, out var res))
                HasValueAtLeastXCache[_i][_x] = res = _m.ITE(_weights[_i].Var,
                    HasValueAtLeastX(_x - _weights[_i].Weight, _i + 1, _ub - _weights[_i].Weight, _weights, _m),
                    HasValueAtLeastX(_x, _i + 1, _ub - _weights[_i].Weight, _weights, _m));

            return res;
        }




        private BoolExpr HasValueX(int _x)
        {
            if (_x < 0)
                return Model.False;

            if (Weights.Count == 0)
                return _x == 0 ? Model.True : Model.False;

            var w = Weights.Select(w => (Var: w.Value > 0 ? w.Key : !w.Key, Weight: Math.Abs(w.Value))).OrderByDescending(e => e.Weight).ToArray();
            return HasValueX(_x, 0, w.Sum(e => e.Weight), w, Weights.First().Key.Model);
        }

        private BoolExpr HasValueX(int _x, int _i, int _ub, (BoolExpr Var, int Weight)[] _weights, Model _m)
        {
            Debug.Assert(_ub >= 0);

            if (_x < 0 || _x > _ub)
                return Model.False;

            if (_i >= _weights.Length)
                return _x == 0 ? Model.True : Model.False;

            if (HasValueXCache is null)
            {
                HasValueXCache = new Dictionary<int, BoolExpr>[_weights.Length];
                for (var i = 0; i < _weights.Length; i++)
                    HasValueXCache[i] = new();
            }

            if (!HasValueXCache[_i].TryGetValue(_x, out var res))
                HasValueXCache[_i][_x] = res = _m.ITE(_weights[_i].Var,
                    HasValueX(_x - _weights[_i].Weight, _i + 1, _ub - _weights[_i].Weight, _weights, _m),
                    HasValueX(_x, _i + 1, _ub - _weights[_i].Weight, _weights, _m));

            return res;
        }


        public static BoolExpr operator <=(LinExpr _a, int _b)
        {
            //TODO: catch cases when UB==_b-smallest or LB==_b or LB==_b+smallest
            if (_a.UB <= _b)
                return Model.True;
            if (_a.LB > _b)
                return Model.False;

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

            var m = _a.Weights.Keys.First().Model;

            var smallest = _a.Weights.Values.Min(v => Math.Abs(v));
            if (rhs == smallest)
                return m.AtMostOneOf(_a.Weights.Where(w => Math.Abs(w.Value) == smallest).Select(w => w.Value > 0 ? w.Key : !w.Key))
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
                return m.AtMostOneOf(_a.Weights.Select(w => w.Value > 0 ? w.Key : !w.Key));

            if (rhs > ub / 2)
                return !(-_a <= (-_b - 1));

            if (_a.Weights.Count <= m.Configuration.LinExprBinaryComparisonThreshold)
                return !_a.HasValueAtLeastX(rhs + 1);
            else
                return _a.ToUInt().Var <= rhs;
        }

        public static BoolExpr operator ==(LinExpr _a, int _b)
        {
            if (_a.UB < _b || _a.LB > _b)
                return Model.False;

            if (_a.LB == _a.UB)
                return _a.LB == _b ? Model.True : Model.False;

            var rhs = _b - _a.Offset;
            foreach (var e in _a.Weights)
                if (e.Value < 0)
                    rhs -= e.Value;

            Debug.Assert(rhs >= 0);
            Debug.Assert(rhs <= _a.Weights.Sum(x => Math.Abs(x.Value)));

            if (rhs == 0)
                return AndExpr.Create(_a.Weights.Select(x => x.Value > 0 ? !x.Key : x.Key).ToArray());

            var m = _a.Weights.First().Key.Model;

            var smallest = _a.Weights.Values.OrderBy(v => Math.Abs(v)).First();
            if (rhs == smallest)
                return m.ExactlyOneOf(_a.Weights.Where(w => Math.Abs(w.Value) == smallest).Select(w => w.Value > 0 ? w.Key : !w.Key))
                    & AndExpr.Create(_a.Weights.Where(w => Math.Abs(w.Value) != smallest).Select(w => w.Value > 0 ? !w.Key : w.Key).ToArray());

            if (_a.Weights.Count <= m.Configuration.LinExprBinaryComparisonThreshold)
                return _a.HasValueX(rhs);
            else
                return _a.ToUInt().Var == rhs;
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
        public static BoolExpr operator >=(LinExpr _a, int _b)
        {
            //TODO: catch cases when UB==_b or LB==_b
            /*if (_a.LB >= _b)
                return Model.True;
            if (_a.UB < _b)
                return Model.False;

            if (_a.Weights.Values.All(v => v < 0))
            {
                var largest = _a.Weights.Values.Max();
                if (_b - _a.Offset == largest)
                {
                    var m = _a.Weights.Keys.First().Model;
                    return m.AtMostOneOf(_a.Weights.Where(w => w.Value == largest).Select(w => w.Key))
                        & AndExpr.Create(_a.Weights.Where(w => w.Value < largest).Select(w => !w.Key).ToArray());
                }
            }

            var rhs = _b - _a.Offset;
            foreach (var e in _a.Weights)
                if (e.Value < 0)
                    rhs -= e.Value;

            Debug.Assert(rhs >= 1); //because of the early Model.False abort above

            if (rhs == 1)
                return OrExpr.Create(_a.Weights.Select(w => w.Value > 0 ? w.Key : !w.Key).ToArray());
            
            return (-_a) <= -_b;*/

            return !(_a <= (_b - 1));
        }


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
                this[bv] += _weight;
            else if (be is NotExpr ne)
            {
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
