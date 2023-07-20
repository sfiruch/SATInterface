using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SATInterface
{
    internal class OrExpr : BoolExpr
    {
        internal readonly int[] Elements;
        internal readonly Model Model;

        private OrExpr(int[] _elems, Model _model)
        {
            Debug.Assert(_elems.Length > 0);

            Elements = _elems;
            Model = _model;
        }

        internal static BoolExpr Create(BoolExpr _a, BoolExpr _b)
        {
            var arr = ArrayPool<BoolExpr>.Shared.Rent(2);
            arr[0] = _a;
            arr[1] = _b;
            var result = Create(arr.AsSpan().Slice(0, 2));
            ArrayPool<BoolExpr>.Shared.Return(arr);
            return result;
        }

        internal static BoolExpr Create(BoolExpr _a, BoolExpr _b, BoolExpr _c)
        {
            var arr = ArrayPool<BoolExpr>.Shared.Rent(3);
            arr[0] = _a;
            arr[1] = _b;
            arr[2] = _c;
            var result = Create(arr.AsSpan().Slice(0, 3));
            ArrayPool<BoolExpr>.Shared.Return(arr);
            return result;
        }

        internal static BoolExpr Create(BoolExpr _a, BoolExpr _b, BoolExpr _c, BoolExpr _d)
        {
            var arr = ArrayPool<BoolExpr>.Shared.Rent(4);
            arr[0] = _a;
            arr[1] = _b;
            arr[2] = _c;
            arr[3] = _d;
            var result = Create(arr.AsSpan().Slice(0, 4));
            ArrayPool<BoolExpr>.Shared.Return(arr);
            return result;
        }

        internal static BoolExpr Create(ReadOnlySpan<BoolExpr> _elems)
        {
            if (_elems.Length == 0)
                return Model.False;
            if (_elems.Length == 1)
                return _elems[0];

            var model = (Model?)null;
            var count = 0;
            foreach (var es in _elems)
            {
                if (ReferenceEquals(es, null))
                    throw new ArgumentNullException();
                else if (ReferenceEquals(es, Model.True))
                    return Model.True;
                else if (es is OrExpr oe)
                    count += oe.FlatCached ? 1 : oe.Elements.Length;
                else if (!ReferenceEquals(es, Model.False))
                    count++;

                model ??= es.GetModel();
            }

            if (count == 0)
                return Model.False;

            Debug.Assert(model is not null);

            var res = new int[count];
            count = 0;
            foreach (var es in _elems)
                if (es is OrExpr oe)
                {
                    if (oe.FlatCached)
                        res[count++] = ((BoolVar)oe.Flatten()).Id;
                    else
                        foreach (var e in oe.Elements)
                            res[count++] = ((BoolVar)oe.Model.GetVariable(e)).Id;
                }
                else if (es is AndExpr ae)
                    res[count++] = ((BoolVar)ae.Flatten()).Id;
                else if (es is BoolVar bv)
                {
                    if (!ReferenceEquals(es, Model.False))
                        res[count++] = ((BoolVar)es).Id;
                }
                else
                    throw new ArgumentException();

            Debug.Assert(count == res.Length);

            if (res.Length == 1)
                return model.GetVariable(res[0]);

            Array.Sort(res);

            for (var i = 1; i < res.Length; i++)
                if (res[i - 1] == res[i])
                    return OrExpr.Create(new HashSet<int>(res).Select(v => model.GetVariable(v)).ToArray());

            var negIndex = 0;
            var posIndex = res.Length - 1;
            if (res[negIndex] < 0 && res[posIndex] > 0)
                for (; ; )
                {
                    Debug.Assert(res[negIndex] < 0);
                    Debug.Assert(res[posIndex] > 0);

                    if (-res[negIndex] >= res[posIndex])
                    {
                        negIndex++;
                        if (res[negIndex] > 0)
                            break;
                    }
                    else if (-res[negIndex] <= res[posIndex])
                    {
                        posIndex--;
                        if (res[posIndex] < 0)
                            break;
                    }
                    else
                        return Model.True;
                }

            //work around O(n^2)-algorithms in certain SAT solvers
            if (res.Length > model.Configuration.MaxClauseSize)
            {
                var chunkSize = model.Configuration.MaxClauseSize - 1;
                var l = new BoolExpr[(count + chunkSize - 1) / chunkSize];
                for (var i = 0; i < l.Length; i++)
                    l[i] = new OrExpr(res.AsSpan()[(i * chunkSize)..Math.Min(res.Length, (i + 1) * chunkSize)].ToArray(), model).Flatten();

                return OrExpr.Create(l);
            }

            return new OrExpr(res, model);
        }

        public override string ToString() => "(" + string.Join(" | ", Elements.Select(e => Model.GetVariable(e).ToString()).ToArray()) + ")";

        internal bool FlatCached => Model.OrCache.ContainsKey(this);

        public override BoolExpr Flatten()
        {
            if (Model.OrCache.TryGetValue(this, out var res))
                return new BoolVar(Model, res);

            Model.OrCache[this] = res = Model.AllocateVar();

            Span<int> l = stackalloc int[Elements.Length + 1];
            Elements.CopyTo(l);
            l[^1] = -res;
            Model.AddClauseToSolver(l);

            Span<int> param = stackalloc int[2];
            param[0] = res;
            foreach (var e in Elements)
            {
                param[1] = -e;
                Model.AddClauseToSolver(param);
            }

            return new BoolVar(Model, res);
        }

        internal override Model? GetModel() => Model;

        public override bool X => Elements.Any(e => Model.GetAssignment(e));

        public override int VarCount => Elements.Length;

        public override bool Equals(object? _obj)
        {
            if (_obj is not OrExpr other)
                return false;

            if (Elements.Length != other.Elements.Length)
                return false;

            for (var i = 0; i < Elements.Length; i++)
                if (Elements[i] != other.Elements[i])
                    return false;

            return true;
        }

        private int hashCode;
        public override int GetHashCode()
        {
            if (hashCode == 0)
            {
                var hc = new HashCode();
                foreach (var e in Elements)
                    hc.Add(e);

                hashCode = hc.ToHashCode();
                if (hashCode == 0)
                    hashCode = 1;
            }
            return hashCode;
        }
    }
}
