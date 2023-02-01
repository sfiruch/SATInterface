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

        private OrExpr(BoolExpr[] _elems, Model _model)
        {
            Debug.Assert(_elems.Length > 0);

            Elements = new int[_elems.Length];
            for (var i = 0; i < _elems.Length; i++)
                if (_elems[i] is NotVar nv)
                    Elements[i] = -nv.inner.Id;
                else if (_elems[i] is BoolVar bv)
                    Elements[i] = bv.Id;
                else
                    throw new Exception();

            Model = _model;
        }

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

            var count = 0;
            foreach (var es in _elems)
                if (ReferenceEquals(es, null))
                    throw new ArgumentNullException();
                else if (ReferenceEquals(es, Model.True))
                    return Model.True;
                else if (es is OrExpr oe)
                    count += oe.FlatCached ? 1 : oe.Elements.Length;
                else if (!ReferenceEquals(es, Model.False))
                    count++;

            if (count == 0)
                return Model.False;

            var res = new BoolExpr[count];
            count = 0;
            foreach (var es in _elems)
                if (es is OrExpr oe)
                {
                    if (oe.FlatCached)
                        res[count++] = oe.Flatten();
                    else
                        foreach (var e in oe.Elements)
                            res[count++] = oe.Model.GetVariable(e);
                }
                else if (es is AndExpr ae)
                    res[count++] = ae.Flatten();
                else if (!ReferenceEquals(es, Model.False))
                    res[count++] = es;

            Debug.Assert(count == res.Length);

            if (res.Length == 1)
                return res[0];

            if (res.Length < 10)
                for (var i = 0; i < res.Length; i++)
                {
                    if (res[i] is NotVar ne && res.Contains(ne.inner))
                        return Model.True;

                    for (var j = i + 1; j < res.Length; j++)
                        if (res[i].Equals(res[j]))
                        {
                            (res[i], res[0]) = (res[0], res[i]);
                            return OrExpr.Create(res[1..]);
                        }
                }
            else
            {
                var posVars = new HashSet<int>(res.Length);
                var negVars = new HashSet<int>(res.Length);
                foreach (var v in res)
                    if (v is NotVar ne)
                    {
                        if (posVars.Contains(ne.inner.Id))
                            return Model.True;
                        negVars.Add(ne.inner.Id);
                    }
                    else if (v is BoolVar bv)
                    {
                        if (negVars.Contains(bv.Id))
                            return Model.True;
                        posVars.Add(bv.Id);
                    }
                    else
                        throw new Exception();

                var distinct = res.Distinct().ToArray();
                if (distinct.Length != res.Length)
                    return OrExpr.Create(distinct);
            }

            var m = res[0].GetModel()!;

            //work around O(n^2)-algorithms in SAT solvers
            if (res.Length > m.Configuration.MaxClauseSize)
            {
                var chunkSize = m.Configuration.MaxClauseSize - 1;
                var l = new BoolExpr[(count + chunkSize - 1) / chunkSize];
                for (var i = 0; i < l.Length; i++)
                    l[i] = OrExpr.Create(res.AsSpan()[(i * chunkSize)..Math.Min(res.Length, (i + 1) * chunkSize)]).Flatten();

                return OrExpr.Create(l);
            }

            return new OrExpr(res, m);
        }

        public override string ToString() => "(" + string.Join(" | ", Elements.Select(e => Model.GetVariable(e).ToString()).ToArray()) + ")";

        internal bool FlatCached => Model.OrCache.ContainsKey(this);

        public override BoolExpr Flatten()
        {
            if (Model.OrCache.TryGetValue(this, out var res))
                return res;

            Model.OrCache[this] = res = (BoolVar)Model.AddVar();

            Span<int> l = stackalloc int[Elements.Length + 1];
            Elements.CopyTo(l);
            l[^1] = -res.Id;
            Model.AddClauseToSolver(l);

            Span<int> param = stackalloc int[2];
            param[0] = res.Id;
            foreach (var e in Elements)
            {
                param[1] = -e;
                Model.AddClauseToSolver(param);
            }

            return res;
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

            //as elements are distinct by construction, one-sided comparison is enough
            foreach (var a in Elements)
                if (!other.Elements.Contains(a))
                    return false;

            return true;
        }

        private int hashCode;
        public override int GetHashCode()
        {
            if (hashCode == 0)
            {
                hashCode = GetType().GetHashCode();

                //deliberatly stupid implementation to produce
                //order-independent hashcodes
                foreach (var e in Elements)
                    hashCode += HashCode.Combine(e);

                if (hashCode == 0)
                    hashCode++;
            }
            return hashCode;
        }
    }
}
