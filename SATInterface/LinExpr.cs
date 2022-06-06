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
        public int Offset { get; private set; }

        private Model? Model;
        private LinExpr? Negated;

        public IEnumerable<(BoolExpr Var, int Weight)> Terms => Weights.Select(w => ((BoolExpr)w.Key, w.Value));

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
            var allOneWeights = true;
            var posWeights = new List<(int Weight, BoolExpr Var)>();
            foreach (var e in Weights)
            {
                if (e.Value > 0)
                    posWeights.Add((e.Value, e.Key));
                else
                {
                    Debug.Assert(e.Value < 0);
                    posWeights.Add((-e.Value, !e.Key));
                }

                if (Math.Abs(e.Value) != 1)
                    allOneWeights = false;
            }

            if (allOneWeights)
                return Model.UIntCache[this] = Model.SumUInt(posWeights.Select(w => w.Var));

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
                    toSum.Add(Model.AddUIntConst(minWeight) * minWeightVars.Single());
                else
                {
                    var sum = Model.SumUInt(minWeightVars);
                    if (sum.UB > 0)
                    {
                        for (var i = 1; i < sum.Bits.Length; i++)
                            posWeights.Add((minWeight << i, sum.Bits[i]));

                        toSum.Add(Model.AddUIntConst(minWeight) * sum.Bits[0]);
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
            {
                //Debug.WriteLine($"{_a} <= {_b}");
                //Debug.Indent();
                _a.Model.LinExprLECache[(_a, _b)] = res = UncachedLE(_a, _b);
                //Debug.Unindent();
            }
            else
            {
                //Debug.WriteLine($"{_a} <= {_b}");
            }

            return res;
        }

        private static BoolExpr UncachedLE(LinExpr _a, int _b)
        {
            Debug.Assert(_a.Model is not null);

            var rhs = _b - _a.Offset;
            var ub = 0L;
            foreach (var e in _a.Weights)
                checked
                {
                    ub += Math.Abs(e.Value);
                    if (e.Value < 0)
                        rhs -= e.Value;
                }

            if (_a.Model.UIntCache.TryGetValue(_a, out var uintV))
                return uintV <= rhs;

            Debug.Assert(rhs >= 0); //because of the early Model.False abort above

            if (rhs == 0)
            {
                //Debug.WriteLine($"And({string.Join(',', _a.Weights.Select(w => w.Value > 0 ? !w.Key : w.Key))})");
                return AndExpr.Create(_a.Weights.Select(w => w.Value > 0 ? !w.Key : w.Key).ToArray());
            }

            var absEqRHSCnt = 0;
            var maxVar = new KeyValuePair<BoolVar, int>((BoolVar)Model.False, 0);
            var maxVarCnt = 0;
            var minAbsWeight = int.MaxValue;
            foreach (var w in _a.Weights)
            {
                var absWeight = Math.Abs(w.Value);
                if (absWeight == rhs)
                    absEqRHSCnt++;
                if (absWeight > Math.Abs(maxVar.Value))
                {
                    maxVar = w;
                    maxVarCnt = 0;
                }
                if (absWeight == Math.Abs(maxVar.Value))
                    maxVarCnt++;
                if (absWeight < minAbsWeight)
                    minAbsWeight = absWeight;
                if (absWeight > rhs)
                {
                    var vWithout = new LinExpr();
                    foreach (var wi in _a.Weights)
                        if (wi.Value > 0 && wi.Value <= rhs)
                            vWithout.AddTerm(wi.Key, wi.Value);
                        else if (wi.Value < 0 && -wi.Value <= rhs)
                            vWithout.AddTerm(!wi.Key, -wi.Value);

                    //Debug.WriteLine($"And({string.Join(',', _a.Weights.Where(w => Math.Abs(w.Value) > rhs).Select(w => w.Value > 0 ? !w.Key : w.Key))}, ...");
                    //Debug.WriteLine($"..., {vWithout} <= {rhs})");

                    return _a.Model.And(_a.Weights
                        .Where(w => Math.Abs(w.Value) > rhs)
                        .Select(w => w.Value > 0 ? !w.Key : w.Key)
                        .Append(vWithout <= rhs));
                }
            }

            if (rhs == minAbsWeight)
            {
                //Debug.WriteLine($"AMO({string.Join(',', _a.Weights.Where(w => Math.Abs(w.Value) == minAbsWeight).Select(w => w.Value > 0 ? w.Key : !w.Key))}) & ...");
                //Debug.WriteLine($"... & And({string.Join(',', _a.Weights.Where(w => Math.Abs(w.Value) > minAbsWeight).Select(w => w.Value > 0 ? !w.Key : w.Key))})");

                return AndExpr.Create(_a.Weights.Where(w => Math.Abs(w.Value) > minAbsWeight).Select(w => w.Value > 0 ? !w.Key : w.Key)
                    .Append(_a.Model.AtMostOneOf(_a.Weights.Where(w => Math.Abs(w.Value) == minAbsWeight).Select(w => w.Value > 0 ? w.Key : !w.Key)))
                    .ToArray());
            }

            var gcd = _a.Weights.Values.Select(w => Math.Abs(w)).Where(w => w < rhs).Aggregate(GCD);
            if (gcd > 1)
            {
                var vDiv = new LinExpr();
                foreach (var w in _a.Weights)
                    if (w.Value > 0)
                        vDiv.AddTerm(w.Key, Math.Min(rhs, w.Value) / gcd);
                    else if (w.Value < 0)
                        vDiv.AddTerm(!w.Key, Math.Min(rhs, -w.Value) / gcd);

                //Debug.WriteLine($"GCD={gcd}, {vDiv} <= {rhs / gcd}");
                return vDiv <= rhs / gcd;
            }

            if (absEqRHSCnt >= 1)
            {
                var lWithout = new List<BoolExpr>();
                var vWithout = new LinExpr(0);
                foreach (var w in _a.Weights)
                    if (Math.Abs(w.Value) != rhs)
                    {
                        vWithout.AddTerm(w.Value > 0 ? w.Key : !w.Key, Math.Abs(w.Value));
                        lWithout.Add(w.Value > 0 ? w.Key : !w.Key);
                    }

                var vRHS = _a.Weights.Where(v => Math.Abs(v.Value) == rhs).Select(v => v.Value > 0 ? v.Key : !v.Key).ToArray();

                if (vWithout.Weights.Count >= 0)
                {
                    //Debug.WriteLine($"ITE {vWithout} <= {rhs}, AMO({string.Join(',', vRHS.AsEnumerable())})");

                    return _a.Model.ITE(_a.Model.Or(lWithout),
                        vWithout <= rhs & !_a.Model.Or(vRHS).Flatten(),
                        _a.Model.AtMostOneOf(vRHS));

                    //return _a.Model.ITE(_a.Model.Or(vRHS),
                    //    _a.Model.ExactlyOneOf(vRHS) & !_a.Model.Or(lWithout),
                    //    vWithout <= rhs);
                }
                else
                {
                    //Debug.WriteLine($"AMO({string.Join(',', vRHS.AsEnumerable())})");
                    return _a.Model.AtMostOneOf(vRHS);
                }
            }

            if (rhs > ub / 2)
            {
                //Debug.WriteLine($"Swapping to !({-_a} <= {(-_b - 1)})");
                return !(-_a <= (-_b - 1));
            }

            //if (_a.Weights.Count < 6 && _a.Weights.Values.All(v => Math.Abs(v) == 1) && rhs == 2)
            //    return _a.Model.AtMostTwoOfSequential(_a.Weights.Select(w => w.Value > 0 ? w.Key : !w.Key));

            Debug.Assert(_a.Weights.Count > 2);

            if (ub <= 16)
                return !_a.Model.SortPairwise(_a.Weights.SelectMany(w => w.Value > 0 ? Enumerable.Repeat(w.Key, w.Value) : Enumerable.Repeat(!w.Key, -w.Value)).ToArray())[rhs];

            //Console.WriteLine($"{_a} <= {_b}");
            //if (_a.Weights.Count <= _a.Model.Configuration.LinExprBinaryComparisonThreshold && !allOneWeights)

            Debug.Assert(ub > rhs);
            if (Math.Abs(maxVar.Value) > 1 && ub - Math.Abs(maxVar.Value) * 2 <= rhs)
            {
                var withoutMaxVar = _a - maxVar.Key * maxVar.Value;
                //Debug.WriteLine($"BDD {_a} <= {_b} on {maxVar.Key}");
                return _a.Model.ITE(maxVar.Key,
                    withoutMaxVar <= _b - maxVar.Value,
                    withoutMaxVar <= _b);
            }
            else
            {
                //Debug.WriteLine($"Binary {_a} <= {_b}");
                return _a.ToUInt() <= rhs;
            }
        }

        public static BoolExpr operator ==(LinExpr _a, int _b)
        {
            if (_a.UB < _b || _a.LB > _b)
                return Model.False;

            if (_a.LB == _a.UB)
                return _a.LB == _b ? Model.True : Model.False;

            Debug.Assert(_a.Model is not null);

            if (!_a.Model.LinExprEqCache.TryGetValue((_a, _b), out var res))
            {
                //Debug.WriteLine($"{_a} == {_b}:");
                //Debug.Indent();
                _a.Model.LinExprEqCache[(_a, _b)] = res = UncachedEq(_a, _b);
                //Debug.Unindent();
            }
            else
            {
                //Debug.WriteLine($"{_a} == {_b}: Cached");
            }

            return res;
        }

        private static BoolExpr UncachedEq(LinExpr _a, int _b)
        {
            Debug.Assert(_a.Model is not null);

            var ub = 0L;
            var rhs = _b - _a.Offset;
            foreach (var e in _a.Weights)
            {
                ub += Math.Abs(e.Value);
                if (e.Value < 0)
                    rhs -= e.Value;
            }

            if (_a.Model.UIntCache.TryGetValue(_a, out var uintV))
                return uintV == rhs;

            Debug.Assert(rhs >= 0);
            Debug.Assert(rhs <= _a.Weights.Sum(x => Math.Abs(x.Value)));

            if (rhs == 0)
            {
                //Debug.WriteLine($"And({string.Join(',', _a.Weights.Select(x => x.Value > 0 ? !x.Key : x.Key))})");
                return AndExpr.Create(_a.Weights.Select(x => x.Value > 0 ? !x.Key : x.Key).ToArray());
            }


            var absEqRHSCnt = 0;
            var maxVar = new KeyValuePair<BoolVar, int>((BoolVar)Model.False, 0);
            var maxVarCnt = 0;
            var minAbsWeight = int.MaxValue;
            foreach (var w in _a.Weights)
            {
                var absWeight = Math.Abs(w.Value);
                if (absWeight == rhs)
                    absEqRHSCnt++;
                if (absWeight > Math.Abs(maxVar.Value))
                {
                    maxVar = w;
                    maxVarCnt = 0;
                }
                if (absWeight == Math.Abs(maxVar.Value))
                    maxVarCnt++;
                if (absWeight < minAbsWeight)
                    minAbsWeight = absWeight;
                if (absWeight > rhs)
                {
                    var vWithout = new LinExpr();
                    foreach (var wi in _a.Weights)
                        if (wi.Value > 0 && wi.Value <= rhs)
                            vWithout.AddTerm(wi.Key, wi.Value);
                        else if (wi.Value < 0 && -w.Value <= rhs)
                            vWithout.AddTerm(!wi.Key, -wi.Value);

                    //Debug.WriteLine($"And({string.Join(',', _a.Weights.Where(w => Math.Abs(w.Value) > rhs).Select(w => w.Value > 0 ? !w.Key : w.Key))} & ...");
                    //Debug.WriteLine($"... & {vWithout == rhs})");

                    return _a.Model.And(_a.Weights
                        .Where(w => Math.Abs(w.Value) > rhs)
                        .Select(w => w.Value > 0 ? !w.Key : w.Key)
                        .Append(vWithout == rhs));
                }
            }

            if (rhs == minAbsWeight)
            {
                //Debug.WriteLine($"And({string.Join(',', _a.Weights.Where(w => Math.Abs(w.Value) != minAbsWeight).Select(w => w.Value > 0 ? !w.Key : w.Key))}, ...");
                //Debug.WriteLine($"..., EOO({string.Join(',', _a.Model.ExactlyOneOf(_a.Weights.Where(w => Math.Abs(w.Value) == minAbsWeight).Select(w => w.Value > 0 ? w.Key : !w.Key)))}))");
                return AndExpr.Create(_a.Weights.Where(w => Math.Abs(w.Value) != minAbsWeight).Select(w => w.Value > 0 ? !w.Key : w.Key)
                    .Append(_a.Model.ExactlyOneOf(_a.Weights.Where(w => Math.Abs(w.Value) == minAbsWeight).Select(w => w.Value > 0 ? w.Key : !w.Key)))
                    .ToArray());
            }

            var gcd = _a.Weights.Values.Select(w => Math.Abs(w)).Aggregate(GCD);
            if (rhs % gcd != 0)
            {
                //Debug.WriteLine($"RHS GCD is nonzero");
                return Model.False;
            }

            if (gcd > 1)
            {
                var vDiv = new LinExpr();
                foreach (var w in _a.Weights)
                    if (w.Value > 0)
                        vDiv.AddTerm(w.Key, w.Value / gcd);
                    else if (w.Value < 0)
                        vDiv.AddTerm(!w.Key, -w.Value / gcd);

                //Debug.WriteLine($"GCD: {vDiv} == {rhs / gcd}");
                return vDiv == rhs / gcd;
            }

            if (absEqRHSCnt >= 1)
            {
                var lWithout = new List<BoolExpr>();
                var vWithout = new LinExpr();
                foreach (var w in _a.Weights)
                    if (Math.Abs(w.Value) != rhs)
                    {
                        vWithout.AddTerm(w.Value > 0 ? w.Key : !w.Key, Math.Abs(w.Value));
                        lWithout.Add(w.Value > 0 ? w.Key : !w.Key);
                    }

                var vRHS = _a.Weights.Where(v => Math.Abs(v.Value) == rhs).Select(v => v.Value > 0 ? v.Key : !v.Key).ToArray();

                if (vWithout.Weights.Count >= 0)
                {
                    //Debug.WriteLine($"ITE {vWithout} == {rhs}, EOO({string.Join(',', vRHS.AsEnumerable())})");

                    return _a.Model.ITE(_a.Model.Or(lWithout),
                        vWithout == rhs & !_a.Model.Or(vRHS).Flatten(),
                        _a.Model.ExactlyOneOf(vRHS));

                    //return _a.Model.ITE(_a.Model.Or(vRHS),
                    //    _a.Model.ExactlyOneOf(vRHS) & !_a.Model.Or(lWithout),
                    //    vWithout == rhs);
                }
                else
                    return _a.Model.ExactlyOneOf(vRHS);
            }

            if (rhs > ub / 2)
            {
                //Debug.WriteLine($"Swapping to {-_a} == {-_b}");
                return -_a == -_b;
            }

            if (_a.Weights.Sum(w => Math.Abs(w.Value)) <= 16)
                return _a.Model.ExactlyKOf(_a.Weights.SelectMany(w => w.Value > 0 ? Enumerable.Repeat(w.Key, w.Value) : Enumerable.Repeat(!w.Key, -w.Value)), rhs, Model.ExactlyKOfMethod.SortPairwise);

            Debug.Assert(_a.Weights.Count > 2);

            //if (_a.Weights.Count <= _a.Model.Configuration.LinExprBinaryComparisonThreshold && Math.Abs(maxVar.Value) >= 1)
            Debug.Assert(ub >= rhs);
            if (Math.Abs(maxVar.Value) > 1 && ub - Math.Abs(maxVar.Value) * 2 < rhs)
            {
                var withoutMaxVar = _a - maxVar.Key * maxVar.Value;
                //Debug.WriteLine($"BDD {_a} == {_b} on {maxVar.Key}");
                return _a.Model.ITE(maxVar.Key,
                    withoutMaxVar == _b - maxVar.Value,
                    withoutMaxVar == _b);
            }
            else
            {
                //Debug.WriteLine($"Binary {_a} == {_b}");
                return _a.ToUInt() == rhs;
            }
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

            if (Offset > 0)
                sb.Append($" + {Offset}");
            else if (Offset < 0)
                sb.Append($" - {-Offset}");

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
