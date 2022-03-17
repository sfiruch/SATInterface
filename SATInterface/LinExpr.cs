using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SATInterface
{
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()

    /// <summary>
    /// A LinExpr is a linear combination of BoolVars with integer weights.
    /// </summary>
    public class LinExpr
    {
        //TODO: tune this threshold
        private const int BinaryComparisonThreshold = 30;

        private Dictionary<BoolVar, int> Weights;
        private int Offset;

        private LinExpr? Negated;
        private List<BoolExpr[]>? SequentialCache;
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
                    if (posVar.Count != 0)
                    {
                        var vars = new BoolExpr[posVar.Count];
                        vars[0] = j < posVar[0].Weight ? posVar[0].be : Model.False;
                        for (var i = 1; i < posVar.Count; i++)
                            vars[i] = ((j >= posVar[i].Weight ? (SequentialCache[j - posVar[i].Weight][i - 1] & posVar[i].be) : posVar[i].be) | vars[i - 1]).Flatten();
                        SequentialCache.Add(vars);
                    }
                    else
                        SequentialCache.Add(new[] { Model.False });
                }
            }

            return Enumerable.Range(0, _limit).Select(i => SequentialCache[i].Last());
        }

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
            while(posWeights.Any())
            {
                var minWeight = posWeights.Min(pw => pw.Weight);
                var minWeightVars = posWeights.Where(pw => pw.Weight == minWeight).Select(pw=>pw.Var).ToArray();
                posWeights.RemoveAll(pw => pw.Weight==minWeight);

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
            return (UIntCache, UIntCacheOffset) = (model.Sum(toSum), offset);


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
                return Model.False;
            if (rhs >= _a.Weights.Sum(x => Math.Abs(x.Value)))
                return Model.True;

            if (rhs > BinaryComparisonThreshold)
            {
                var aui = _a.ToUInt();
                return aui.Var <= rhs; // - aui.Offset; offset is already included in RHS computation
            }

            return !_a.ComputeSequential(rhs + 1).Last();
        }

        public static BoolExpr operator ==(LinExpr _a, int _b)
        {
            var rhs = _b - _a.Offset;
            foreach (var e in _a.Weights)
                if (e.Value < 0)
                    rhs -= e.Value;

            if (rhs < 0)
                return Model.False;
            if (rhs > _a.Weights.Sum(x => Math.Abs(x.Value)))
                return Model.False;

            if (rhs == 0)
                return AndExpr.Create(_a.Weights.Select(x => x.Value > 0 ? !x.Key : x.Key));

            if (rhs > BinaryComparisonThreshold)
            {
                var aui = _a.ToUInt();
                return aui.Var == rhs; // - aui.Offset; offset is already included in RHS computation
            }

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
    }
}
