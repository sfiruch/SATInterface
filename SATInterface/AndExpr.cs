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
    internal class AndExpr : BoolExpr
    {
        internal readonly BoolExpr[] Elements;

        private AndExpr(BoolExpr[] _elems)
        {
            Elements = _elems;
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

        internal static BoolExpr Create(ReadOnlySpan<BoolExpr> _elems)
        {
            if (_elems.Length == 0)
                return Model.True;
            if (_elems.Length == 1)
                return _elems[0];

            var count = 0;
            foreach (var es in _elems)
                if (ReferenceEquals(es, null))
                    throw new ArgumentNullException();
                else if (ReferenceEquals(es, Model.False))
                    return Model.False;
                else if (es is AndExpr ae)
                    count += ae.Elements.Length;
                else if (!ReferenceEquals(es, Model.True))
                    count++;

            if (count == 0)
                return Model.True;

            var res = new BoolExpr[count];
            count = 0;
            foreach (var es in _elems)
                if (es is AndExpr ae)
                    foreach (var e in ae.Elements)
                        res[count++] = e;
                else if (!ReferenceEquals(es, Model.True))
                    res[count++] = es;

            Debug.Assert(count == res.Length);

            if (count == 1)
                return res[0];

            for (var i = 0; i < res.Length; i++)
                if (res[i] is NotExpr ne && res.Contains(ne.inner))
                    return Model.False;

            if (res.Length < 10)
            {
                for (var i = 0; i < res.Length; i++)
                    for (var j = i + 1; j < res.Length; j++)
                        if (ReferenceEquals(res[j], res[i]))
                            return new AndExpr(res.Distinct().ToArray());
                return new AndExpr(res);
            }
            else
                return new AndExpr(res.Distinct().ToArray());
        }

        public override BoolExpr Flatten()
        {
            var be = ArrayPool<BoolExpr>.Shared.Rent(Elements.Length);
            for (var i = 0; i < Elements.Length; i++)
                be[i] = !Elements[i];
            var res = !(OrExpr.Create(be.AsSpan().Slice(0, Elements.Length)).Flatten());
            ArrayPool<BoolExpr>.Shared.Return(be);
            return res;

            //var model = GetModel();
            //if (model.AndCache.TryGetValue(this, out var res))
            //    return res;

            //model.AndCache[this] = res = model.AddVar();

            //var l = ArrayPool<BoolExpr>.Shared.Rent(Elements.Length + 1);
            //for (var i = 0; i < Elements.Length; i++)
            //    l[i] = !Elements[i];
            //l[Elements.Length] = res;
            //model.AddConstr(OrExpr.Create(l.AsSpan().Slice(0, Elements.Length + 1)));
            //ArrayPool<BoolExpr>.Shared.Return(l);

            //foreach (var e in Elements)
            //    model.AddConstr(e | !res);

            //return res;
        }

        internal override IEnumerable<BoolVar> EnumVars()
        {
            foreach (var e in Elements)
                foreach (var v in e.EnumVars())
                    yield return v;
        }

        internal override Model GetModel()
        {
            foreach (var e in Elements)
                if (e.GetModel() is Model m)
                    return m;

            throw new InvalidOperationException();
        }

        public override string ToString() => "(" + string.Join(" & ", Elements.Select(e => e.ToString()).ToArray()) + ")";

        public override bool X => Elements.All(e => e.X);

        public override int VarCount => Elements.Length;

        public override bool Equals(object? _obj)
        {
            var other = _obj as AndExpr;
            if (ReferenceEquals(other, null))
                return false;

            if (Elements.Length != other.Elements.Length)
                return false;

            //as elements are distinct by construction, one-sided comparison is enough
            foreach (var a in Elements)
            {
                var found = false;
                foreach (var b in other.Elements)
                    if (ReferenceEquals(a, b))
                    {
                        found = true;
                        break;
                    }
                if (!found)
                    return false;
            }
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
