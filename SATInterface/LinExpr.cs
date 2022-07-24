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
                    var sum = Model.SumUInt(CollectionsMarshal.AsSpan(minWeightVars));
                    if (sum.UB > 0)
                    {
                        for (var i = 1; i < sum.Bits.Length; i++)
                        {
                            Debug.Assert((minWeight << i) > 0);
                            posWeights.Add((minWeight << i, sum.Bits[i]));
                        }

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

            if (_a.Model.LinExprLECache.TryGetValue((_a, _b - 1), out var res1) && _a.Model.LinExprEqCache.TryGetValue((_a, _b), out var res2))
                return res1 | res2;

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

                    return _a.Model.And(_a.Weights
                        .Where(w => Math.Abs(w.Value) > rhs)
                        .Select(w => w.Value > 0 ? !w.Key : w.Key)
                        .Append(vWithout <= rhs));
                }
            }

            if (rhs == minAbsWeight)
            {
                //return AndExpr.Create(_a.Weights.Where(w => Math.Abs(w.Value) > minAbsWeight).Select(w => w.Value > 0 ? !w.Key : w.Key)
                //    .Append(_a.Model.AtMostOneOf(_a.Weights.Where(w => Math.Abs(w.Value) == minAbsWeight).Select(w => w.Value > 0 ? w.Key : !w.Key)))
                //    .ToArray());

                Debug.Assert(_a.Weights.All(w => Math.Abs(w.Value) == rhs));
                return _a.Model.AtMostOneOf(_a.Weights.Select(w => w.Value > 0 ? w.Key : !w.Key));
            }

            Debug.Assert(_a.Weights.All(w => Math.Abs(w.Value) <= rhs));
            var gcd = _a.Weights.Values.Select(w => Math.Abs(w)).Aggregate(GCD);
            if (gcd > 1)
            {
                var vDiv = new LinExpr();
                foreach (var w in _a.Weights)
                    if (w.Value > 0)
                    {
                        Debug.Assert(w.Value % gcd == 0);
                        vDiv.AddTerm(w.Key, w.Value / gcd);
                    }
                    else if (w.Value < 0)
                    {
                        Debug.Assert((-w.Value) % gcd == 0);
                        vDiv.AddTerm(!w.Key, (-w.Value) / gcd);
                    }

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
                }
                else
                {
                    //Debug.WriteLine($"AMO({string.Join(',', vRHS.AsEnumerable())})");
                    return _a.Model.AtMostOneOf(vRHS);
                }
            }

            //if (_a.Weights.Count <= 18)
            //    (_a, rhs) = CanonicalizeLE(_a, rhs);

            if (rhs > ub / 2)
            {
                //Debug.WriteLine($"Swapping to !({-_a} <= {(-_b - 1)})");
                return !(-_a <= (-_b - 1));
            }

            Debug.Assert(_a.Weights.Count > 2);
            Debug.Assert(ub > rhs);

            if (Math.Abs(maxVar.Value) == 1 && _a.Weights.Count <= 18)
            {
                var res = EnumerateLEResolvent(_a, rhs);
                if (res is not null)
                {
                    //Console.WriteLine($"{res.Count}: {_a}<={_b}");
                    return res;
                }
            }
            else if (Math.Abs(maxVar.Value) != minAbsWeight && _a.Weights.Count <= 18)
            {
                var res = EnumerateLEResolventWeightGrouped(_a, rhs);
                if (res is not null)
                {
                    //Console.WriteLine($"WG: {_a}<={_b}");
                    return res;
                }
            }

            if (ub <= 16)
                return !_a.Model.SortPairwise(_a.Weights.SelectMany(w => w.Value > 0 ? Enumerable.Repeat(w.Key, w.Value) : Enumerable.Repeat(!w.Key, -w.Value)).ToArray())[rhs];

            if (Math.Abs(maxVar.Value) > 1 && ub - Math.Abs(maxVar.Value) * 2 <= rhs)
            {
                //Debug.WriteLine($"BDD {_a} <= {_b} on {maxVar.Key}");
                var withoutMaxVar = _a - maxVar.Key * maxVar.Value;
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


        private static BoolExpr? EnumerateLEResolvent(LinExpr _a, int _rhs)
        {
            var m = _a.Model!;
            var limit = m.Configuration.EnumerateLinExprComparisonsLimit;
            if (limit == 0)
                return null;

            var vars = _a.Weights.OrderByDescending(w => Math.Abs(w.Value)).Select(w => w.Value > 0 ? w.Key : !w.Key).ToArray();
            var _weights = _a.Weights.OrderByDescending(w => Math.Abs(w.Value)).Select(w => Math.Abs(w.Value)).ToArray();

            var res = new List<BoolExpr[]>(limit + 1);
            var active = new Stack<BoolExpr>(vars.Length);
            void Visit(int _s)
            {
                if (res.Count > limit)
                    return;

                active.Push(vars[_s]);
                _rhs -= _weights[_s];

                if (_rhs < 0)
                    res.Add(active.ToArray());
                else
                    for (var i = _s + 1; i < _weights.Length; i++)
                        Visit(i);

                active.Pop();
                _rhs += _weights[_s];
            }

            for (var i = 0; i < _weights.Length; i++)
                Visit(i);

            Debug.Assert(active.Count == 0);

            if (res.Count > limit)
                return null;
            else
                return AndExpr.Create(res.Select(r => OrExpr.Create(r.Select(v => !v).ToArray()).Flatten()).ToArray());
        }

        private static BoolExpr? EnumerateLEResolventWeightGrouped(LinExpr _a, int _rhs)
        {
            var m = _a.Model!;
            var limit = m.Configuration.EnumerateLinExprComparisonsLimit;
            if (limit == 0)
                return null;

            var weights = _a.Weights.Values.Select(w => Math.Abs(w)).Distinct().OrderByDescending(w => w).ToArray();
            var max = weights.Select(wi => _a.Weights.Count(w => Math.Abs(w.Value) == wi)).ToArray();
            var count = new int[weights.Length];

            var resolvent = new List<int[]>(limit + 1);
            void Visit(int _s)
            {
                if (resolvent.Count > limit)
                    return;

                if (_s + 1 < weights.Length)
                    Visit(_s + 1);

                var origRHS = _rhs;
                for (count[_s] = 1; count[_s] <= max[_s]; count[_s]++)
                {
                    _rhs -= weights[_s];
                    if (_rhs < 0)
                    {
                        resolvent.Add(count.ToArray());
                        break;
                    }
                    else if (_rhs >= 0 && _s + 1 < weights.Length)
                        Visit(_s + 1);

                }

                _rhs = origRHS;
                count[_s] = 0;
            }

            Visit(0);

            if (resolvent.Count > limit)
                return null;

            var varCnt = weights.Select(wi => m.Sum(_a.Weights.Where(w => Math.Abs(w.Value) == wi)
                .Select(w => w.Value > 0 ? w.Key : !w.Key))).ToArray();

            return AndExpr.Create(resolvent.Select(a => OrExpr.Create(
                    a.Select((cnt, i) => varCnt[i] < cnt).ToArray()
                )).ToArray());
        }


        private static BoolExpr? EnumerateEqAssignments(LinExpr _a, int _rhs)
        {
            var m = _a.Model!;
            var limit = m.Configuration.EnumerateLinExprComparisonsLimit;
            if (limit == 0)
                return null;

            var vars = _a.Weights.OrderByDescending(w => Math.Abs(w.Value)).Select(w => w.Value > 0 ? w.Key : !w.Key).ToArray();
            var _weights = _a.Weights.OrderByDescending(w => Math.Abs(w.Value)).Select(w => Math.Abs(w.Value)).ToArray();

            var res = new List<BoolExpr[]>(limit + 1);
            var active = new Stack<BoolExpr>(vars.Length);
            void Visit(int _s)
            {
                if (res.Count > limit)
                    return;

                active.Push(vars[_s]);
                _rhs -= _weights[_s];

                if (_rhs == 0)
                {
                    for (var i = _s + 1; i < _weights.Length; i++)
                        active.Push(!vars[i]);
                    res.Add(active.ToArray());
                    for (var i = _s + 1; i < _weights.Length; i++)
                        active.Pop();
                }
                else if (_rhs > 0)
                {
                    for (var i = _s + 1; i < _weights.Length; i++)
                    {
                        Visit(i);
                        active.Push(!vars[i]);
                    }
                    for (var i = _s + 1; i < _weights.Length; i++)
                        active.Pop();
                }

                _rhs += _weights[_s];
                active.Pop();
            }

            for (var i = 0; i < _weights.Length; i++)
            {
                Visit(i);
                active.Push(!vars[i]);
            }

            Debug.Assert(active.Count == _weights.Length);

            if (res.Count > limit)
                return null;
            else
                return OrExpr.Create(res.Select(vars => AndExpr.Create(vars).Flatten()).ToArray());
        }

        private static BoolExpr? EnumerateEqAssignmentsWeightGrouped(LinExpr _a, int _rhs)
        {
            var m = _a.Model!;
            var limit = m.Configuration.EnumerateLinExprComparisonsLimit;
            if (limit == 0)
                return null;

            var weights = _a.Weights.Values.Select(w => Math.Abs(w)).Distinct().OrderByDescending(w => w).ToArray();
            var max = weights.Select(wi => _a.Weights.Count(w => Math.Abs(w.Value) == wi)).ToArray();
            var count = new int[weights.Length];

            var validAssignments = new List<int[]>(limit + 1);
            void Visit(int _s, int _cnt)
            {
                if (validAssignments.Count > limit)
                    return;

                count[_s] = _cnt;
                _rhs -= weights[_s] * _cnt;

                if (_rhs == 0)
                    validAssignments.Add(count.ToArray());
                else if (_rhs > 0 && _s + 1 < weights.Length)
                    for (var i = 0; i <= max[_s + 1]; i++)
                        Visit(_s + 1, i);

                _rhs += weights[_s] * _cnt;
                count[_s] = 0;
            }

            for (var i = 0; i <= max[0]; i++)
                Visit(0, i);

            if (validAssignments.Count > limit)
                return null;

            var varCnt = weights.Select(wi => m.Sum(_a.Weights.Where(w => Math.Abs(w.Value) == wi)
                .Select(w => w.Value > 0 ? w.Key : !w.Key))).ToArray();

            return OrExpr.Create(validAssignments.Select(a => AndExpr.Create(
                    a.Select((cnt, i) => varCnt[i] == cnt).ToArray()
                )).ToArray());
        }

        private static (LinExpr LE, int RHS) CanonicalizeLE(LinExpr _a, int _rhs)
        {
            //Wilson, J.M., 1977. A method for reducing coefficients in zero‐one linear
            //inequalities. International Journal of Mathematical Educational in Science
            //and Technology, 8(1), pp.31-35.
            //https://www.tandfonline.com/doi/pdf/10.1080/0020739770080104

            //Onyekwelu, D.C. and Proll, L.G., 1982. On Wilson's method for equivalent
            //inequalities. International Journal of Mathematical Education in Science
            //and Technology, 13(5), pp.551-557.
            //https://www.tandfonline.com/doi/pdf/10.1080/0020739820130505

            var abortEarly = false;

            List<int[]> ComputeResolvent(int[] _weights, int _rhs)
            {
                var res = new List<int[]>();
                var active = new Stack<int>();
                var remaining = _rhs;
                void Visit(int _s)
                {
                    if (abortEarly)
                        return;

                    active.Push(_s);
                    remaining -= _weights[_s];

                    if (remaining < 0)
                    {
                        var r = new int[active.Count];
                        active.CopyTo(r, 0);
                        Array.Reverse(r);

                        //if (ComputeLBUB(_weights, r).LB == _rhs)
                        //  abortEarly = true;

                        res.Add(r);
                    }
                    else
                        for (var i = _s + 1; i < _weights.Length; i++)
                            Visit(i);

                    active.Pop();
                    remaining += _weights[_s];
                }

                for (var i = 0; i < _weights.Length; i++)
                    Visit(i);

                return res;
            }

            (int LB, int UB) ComputeLBUB(int[] _weights, int[] _cj)
            {
                var lb = 0;
                for (var i = 0; i < _cj.Length - 1; i++)
                    lb += _weights[_cj[i]];
                return (lb, lb + _weights[_cj[^1]] - 1);
            }

            bool IsValid(int[] _oldWeights, int _rhs, int[] _newWeights, int _newRHS)
            {
                var remainingOld = _rhs;
                var remainingNew = _newRHS;

                if (remainingOld < 0 ^ remainingNew < 0)
                    return false;

                bool Visit(int _s)
                {
                    remainingOld -= _oldWeights[_s];
                    remainingNew -= _newWeights[_s];

                    if (remainingOld < 0 ^ remainingNew < 0)
                        return false;

                    if (remainingOld >= 0 && remainingNew >= 0)
                        for (var i = _s + 1; i < _oldWeights.Length; i++)
                            if (!Visit(i))
                                return false;

                    remainingOld += _oldWeights[_s];
                    remainingNew += _newWeights[_s];
                    return true;
                }

                for (var i = 0; i < _oldWeights.Length; i++)
                    if (!Visit(i))
                        return false;

                return true;
            }



            var vars = _a.Weights.OrderByDescending(w => Math.Abs(w.Value)).Select(w => w.Value > 0 ? w.Key : !w.Key).ToArray();
            var oldWeights = _a.Weights.OrderByDescending(w => Math.Abs(w.Value)).Select(w => Math.Abs(w.Value)).ToArray();
            var resolvent = ComputeResolvent(oldWeights, _rhs);
            if (abortEarly)
                return (_a, _rhs);

            var largerThan = new bool[vars.Length, vars.Length];
            foreach (var r in resolvent)
                for (var jk = 0; jk < vars.Length; jk++)
                {
                    var idx = Array.IndexOf(r, jk);
                    if (idx == -1)
                        continue;

                    for (var jkt = jk + 1; jkt < oldWeights.Length && (idx == r.Length - 1 || jkt < r[idx + 1]); jkt++)
                    {
                        Debug.Assert(!r.Contains(jkt));

                        var r2 = r.ToArray();
                        r2[idx] = jkt;
                        Array.Sort(r2);

                        if (!resolvent.Any(rnot => Enumerable.SequenceEqual(rnot, r2)))
                        {
                            largerThan[jk, jkt] = true;
                            break;
                        }
                    }
                }


            var newWeights = new int[vars.Length];
            for (var i = 0; i < vars.Length; i++)
                if (resolvent.Any(r => r.Contains(i)))
                    newWeights[i] = 1;

            var increased = new bool[vars.Length];
            void ApplyRules()
            {
                //TODO: in reverse order - things should converge in 1 step?
                for (; ; )
                {
                    var changed = false;
                    for (var i = 0; i < vars.Length; i++)
                        for (var j = i + 1; j < vars.Length; j++)
                            if (oldWeights[i] == oldWeights[j] && newWeights[i] < newWeights[j])
                            {
                                increased[i] = true;
                                newWeights[i] = newWeights[j];
                                changed = true;
                            }
                            else if (largerThan[i, j] && newWeights[i] <= newWeights[j])
                            {
                                increased[i] = true;
                                newWeights[i] = newWeights[j] + 1;
                                changed = true;
                            }

                    if (!changed)
                        break;
                }
            }

            ApplyRules();

            for (; ; )
            {
                var minRHS = 0;
                var maxRHS = int.MaxValue;

                foreach (var r in resolvent)
                {
                    (var lb, var ub) = ComputeLBUB(newWeights, r);
                    if (lb > minRHS)
                        minRHS = lb;
                    if (ub < maxRHS)
                        maxRHS = ub;
                }

                for (var rhs = minRHS; rhs <= maxRHS; rhs++)
                    if (IsValid(oldWeights, _rhs, newWeights, rhs))
                    {
                        var res = new LinExpr();
                        for (var i = 0; i < oldWeights.Length; i++)
                            res.AddTerm(vars[i], newWeights[i]);
                        return (res, rhs);
                    }



                var incI = Enumerable.Range(0, vars.Length).Where(i => !increased[i]).MaxBy(i => newWeights[i]);
                Array.Clear(increased);
                newWeights[incI]++;
                increased[incI] = true;
                ApplyRules();
            }
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
                return AndExpr.Create(_a.Weights.Select(x => x.Value > 0 ? !x.Key : x.Key).ToArray());

            if (_a.Model.LinExprLECache.TryGetValue((_a, _b - 1), out var res1) && _a.Model.LinExprLECache.TryGetValue((_a, _b), out var res2))
                return !res1 & res2;

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

            Debug.Assert(_a.Weights.Count > 2);
            Debug.Assert(ub >= rhs);

            if (Math.Abs(maxVar.Value) == 1 && _a.Weights.Count <= 18)
            {
                var res = EnumerateEqAssignments(_a, rhs);
                if (res is not null)
                {
                    //Console.WriteLine($"{res.Count}: {_a}=={_b}");
                    return res;
                }
            }
            else if (Math.Abs(maxVar.Value) != minAbsWeight && _a.Weights.Count <= 18)
            {
                var res = EnumerateEqAssignmentsWeightGrouped(_a, rhs);
                if (res is not null)
                {
                    //Console.WriteLine($"WG: {_a}=={_b}");
                    return res;
                }
            }

            if (ub <= 16)
                return _a.Model.ExactlyKOf(_a.Weights.SelectMany(w => w.Value > 0 ? Enumerable.Repeat(w.Key, w.Value) : Enumerable.Repeat(!w.Key, -w.Value)), rhs, Model.ExactlyKOfMethod.SortPairwise);

            if (Math.Abs(maxVar.Value) > 1 && ub - Math.Abs(maxVar.Value) * 2 < rhs)
            {
                //Debug.WriteLine($"BDD {_a} == {_b} on {maxVar.Key}");
                var withoutMaxVar = _a - maxVar.Key * maxVar.Value;
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
